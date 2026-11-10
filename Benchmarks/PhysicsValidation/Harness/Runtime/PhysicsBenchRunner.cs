#if PHYSICS_BENCH
// 물리 검증 실행기. 시작 인수 -physicsBench가 있을 때만 동작한다.
// trial마다 원본 씬을 다시 불러 초기화하고, 실제 PlayerInput → PlayerMovement 경로에 가상 입력만 넣는다.
// 매 물리 스텝의 위치·속도·접촉·지지·침투를 원시 기록으로 남기고 판정은 하지 않는다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PhysicsBench
{
    public class PhysicsBenchRunner : MonoBehaviour
    {
        private const string STREAMING_DIR = "PhysicsBench";
        private const float SETTLE_CLEARANCE = 0.0005f;
        private const float YAW_TOLERANCE = 0.01f;
        private const int YAW_MAX_ITERATIONS = 8;
        private const float TAP_SEC = 0.1f;
        private const float RELEASE_SEC = 0.1f;
        private const float STALL_REALTIME_SEC = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!HasArg("-physicsBench"))
                return;
            var go = new GameObject("PhysicsBenchRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<PhysicsBenchRunner>();
        }

        // --- 실행 설정 ---
        private string _runId;
        private string _outDir;
        private string _casesPath;
        private string _casesSha;
        private BenchCaseFile _caseFile;
        private BenchProfile _profile;
        private int _seed;
        private int _attempt;
        private int _targetFps;
        private float _dtOverride = -1f;
        private string _ccdOverride;
        private string _caseFilter;
        private bool _capture;

        // --- trial 상태 ---
        private PhysicsBenchInput _input;
        private BenchStatus _status;
        private BenchCase _case;
        private BenchTrial _trial;
        private PlayerMovement _player;
        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private PlayerInput _playerInput;
        // H1 개미는 주 캡슐 외에 자식 body Rigidbody와 다리·머리 콜라이더를 가진다. 모든 Rigidbody의 접촉을 수집한다.
        private readonly List<PhysicsBenchContactRecorder> _recorders = new();
        private Rigidbody[] _bodies;
        private Collider[] _playerColliders;   // 주 캡슐을 제외한 플레이어 비트리거 콜라이더
        // 첫 물리 스텝 전 자식 Rigidbody의 로컬 자세. 정렬 중 어긋난 자세를 재배치 때 되돌려 출발 상태를 같게 한다.
        private Vector3[] _childLocalPos;
        private Quaternion[] _childLocalRot;
        private readonly StringBuilder _trace = new();
        private readonly List<BenchPhaseMark> _phases = new();
        private readonly Dictionary<int, BenchColliderInfo> _colliders = new();
        private readonly List<string> _controls = new();
        private readonly List<string> _logErrors = new();
        private readonly Collider[] _overlap = new Collider[32];
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private string _phase = "none";
        private int _step;
        private bool _sceneReady;
        private bool _rolling;
        private bool _wallContact;
        private float _defaultFixedDeltaTime;
        private float _trialRealStart;
        private float _trialBudgetReal;
        private float _lastStepRealtime;
        private int _frames;
        private float _maxFrameMs;
        private string _abortReason;
        private bool _userStop;
        private Coroutine _trialCo;

        private void Start()
        {
            Application.runInBackground = true;
            // Development 빌드 콘솔이 원본 씬의 MeshCollider 오류로 화면을 덮어 캡처를 가린다. 로그 파일에는 그대로 남는다.
            Debug.developerConsoleEnabled = false;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived += OnLog;
            StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            if (!LoadConfig(out string error))
            {
                Fail(error);
                yield break;
            }

            _defaultFixedDeltaTime = Time.fixedDeltaTime;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _targetFps;
            WriteRunManifest();
            WriteEnvironment();

            var order = Shuffle(_caseFile.cases.Where(c => string.IsNullOrEmpty(_caseFilter) || c.id.StartsWith(_caseFilter)).ToList(), _seed);
            _status = new BenchStatus { runId = _runId, state = "running", planned = order.Count };
            WriteStatus();
            File.WriteAllText(Path.Combine(_outDir, "order.json"), JsonUtility.ToJson(new OrderList { caseIds = order.Select(c => c.id).ToArray(), seed = _seed }, true));

            _input = new PhysicsBenchInput();
            _input.MapKeys(new[]
            {
                (KeyOf(_profile.keyForward), "Move"), (KeyOf(_profile.keyBack), "Move"),
                (KeyOf(_profile.keyLeft), "Move"), (KeyOf(_profile.keyRight), "Move"),
                (KeyOf(_profile.keySprint), "Sprint"), (KeyOf(_profile.keyJump), "Jump"),
                (KeyOf(_profile.keyRoll), "Roll"),
            });
            SceneManager.sceneLoaded += OnSceneLoaded;

            for (int i = 0; i < order.Count && !_userStop; i++)
            {
                _case = order[i];
                _status.currentCase = _case.id;
                WriteStatus();
                _abortReason = null;
                _trialCo = StartCoroutine(RunTrial(i));
                while (_trialCo != null && _abortReason == null)
                    yield return null;
                if (_abortReason != null)
                {
                    if (_trialCo != null)
                        StopCoroutine(_trialCo);
                    _trialCo = null;
                    FinishTrial("harness_error", _abortReason);
                }
                _status.finished++;
                WriteStatus();
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _input.Dispose();
            _status.state = _userStop ? "interrupted" : "completed";
            _status.currentCase = null;
            WriteStatus();
            Application.Quit(_userStop ? 2 : 0);
        }

        private IEnumerator RunTrial(int order)
        {
            _trial = new BenchTrial
            {
                runId = _runId,
                experimentId = _caseFile.experimentId,
                versionId = _profile.versionId,
                caseId = _case.id,
                attempt = _attempt,
                order = order,
                scenario = _case.scenario,
                environment = _case.environment,
                moveMode = _case.moveMode,
                expect = _case.expect,
                sabotage = _case.sabotage,
                minAllowedY = _case.minAllowedY,
                wallNormal = _case.wallNormal,
                wallPoint = _case.wallPoint,
                wallTopY = _case.wallTopY,
                wallPlaneTolerance = _case.wallPlaneTolerance,
                wallTargets = _case.wallTargets,
                start = _case.start,
                startYaw = _case.yaw,
            };
            _trace.Clear();
            _phases.Clear();
            _colliders.Clear();
            _controls.Clear();
            _logErrors.Clear();
            _input.SetDropped(Array.Empty<Key>());
            _input.ReleaseAll("Reset");
            _input.ResetLog();
            _step = 0;
            _frames = 0;
            _maxFrameMs = 0f;
            _rolling = false;
            _phase = "load";
            _trialRealStart = Time.realtimeSinceStartup;
            _lastStepRealtime = _trialRealStart;
            _trialBudgetReal = EstimateSeconds(_case) * 4f + 30f;

            if (Time.timeScale != 1f)
                _controls.Add("reset:timeScale_was=" + Time.timeScale.ToString("R"));
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
            _sceneReady = false;
            SceneManager.LoadScene(_profile.sceneName, LoadSceneMode.Single);
            while (!_sceneReady)
                yield return null;
            if (_player == null)
            {
                _abortReason = "player_missing";
                yield break;
            }

            // Start가 끝난 뒤 페어링한다. PlayerInput.OnEnable이 기본 장치를 잡은 상태를 가상 장치로 교체한다.
            yield return null;
            yield return null;
            _input.Pair(_playerInput, _profile.controlScheme, _profile.actionMap, false);
            _input.Observe(_playerInput, "Move", "Look", "Sprint", "Jump", "Roll");
            PhysicsBenchPlayerApi.SubscribeRoll(_player, OnRollActive);

            _phase = "align";
            yield return AlignYaw(_case.yaw);
            if (_abortReason != null)
                yield break;
            _trial.yawAfterAlign = PhysicsBenchPlayerApi.Yaw(_player);
            _lastStepRealtime = Time.realtimeSinceStartup;

            // 판정기 검증용: Sprint 키만 장치로 보내지 않는다(Move는 전달). 부분 전달을 잡아내야 한다.
            if (_case.sabotage == "partial_input")
                _input.SetDropped(new[] { KeyOf(_profile.keySprint) });
            if (_case.sabotage == "no_input")
                _input.Pair(_playerInput, _profile.controlScheme, _profile.actionMap, true);

            // 정렬하는 동안 움직였을 수 있으므로 같은 출발 상태로 다시 놓는다.
            PlaceAtStart();
            MeasureInitialPenetration();
            foreach (var rec in _recorders)
                rec.Clear();
            _input.ResetLog();
            _trial.fixedDeltaTime = Time.fixedDeltaTime;
            _trial.collisionDetection = _rb.collisionDetectionMode.ToString();
            _trial.interpolation = _rb.interpolation.ToString();
            _trial.contactOffset = Physics.defaultContactOffset;
            CapsuleWorld(out _, out _, out float r, out float halfH);
            _trial.capsuleRadiusWorld = r;
            _trial.capsuleHalfHeightWorld = halfH;

            yield return Steps("settle", _case.settleSec);
            // 판정기 검증용: watchdog 경로가 실제로 trial을 중단하고 다음 trial이 정상 진행되는지 확인한다.
            if (_case.sabotage == "time_scale_zero")
                Time.timeScale = 0f;
            else if (_case.sabotage == "hang")
                while (true)
                    yield return null;
            switch (_case.scenario)
            {
                case "hold":
                    yield return Steps("hold", _case.observeSec);
                    break;
                case "walk":
                    yield return Walk(false);
                    break;
                case "jump":
                    yield return Walk(true);
                    break;
                case "curb":
                    yield return Curb();
                    break;
                default:
                    _abortReason = "unknown_scenario:" + _case.scenario;
                    yield break;
            }

            _input.ReleaseAll();
            if (_capture)
            {
                string dir = Path.Combine(_outDir, "captures");
                Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, _case.id + "_a" + _attempt + ".png"));
                yield return new WaitForEndOfFrame();
                yield return null;
            }
            FinishTrial("completed", null);
            _trialCo = null;
        }

        // --- 시나리오 ---

        private IEnumerator Walk(bool jump)
        {
            StartMode();
            var keys = _case.moveMode == "Stand" ? new Key[0] : MoveKeys(KeyOf(_profile.keyForward));
            if (_case.moveMode != "Stand")
                _input.SetKeys(MoveActionName(), keys);
            float t0 = Time.fixedTime;
            bool jumped = false;
            Mark("move");
            while (Time.fixedTime - t0 < _case.moveSec - 1e-5f)
            {
                if (jump && !jumped && Time.fixedTime - t0 >= _case.jumpAtSec)
                {
                    jumped = true;
                    _input.SetKeys(MoveActionName() + "+Jump", keys.Append(KeyOf(_profile.keyJump)).ToArray());
                    yield return Steps("jump_press", TAP_SEC);
                    // 점프 키만 떼므로 수신 확인 대상은 Jump(canceled)다.
                    if (_case.moveMode != "Stand")
                        _input.SetKeys("Jump", keys);
                    else
                        _input.ReleaseAll();
                    Mark("move");
                    continue;
                }
                yield return OneStep();
            }
            _input.ReleaseAll();
            yield return Steps("observe", _case.observeSec);
        }

        private IEnumerator Curb()
        {
            StartMode();
            var forward = MoveKeys(KeyOf(_profile.keyForward));
            _input.SetKeys(MoveActionName(), forward);
            Mark("approach");
            float t0 = Time.fixedTime;
            float lastSpeed = 0f;
            while (Time.fixedTime - t0 < _case.approachTimeoutSec - 1e-5f)
            {
                yield return OneStep();
                if (_wallContact)
                {
                    _trial.firstWallContactTime = Time.fixedTime;
                    _trial.preContactSpeed = lastSpeed;
                    break;
                }
                lastSpeed = HorizontalSpeed();
            }
            // 접촉하지 않았어도 같은 시간 예산으로 다음 단계를 수행한다. 분석기가 미접촉으로 기록한다.
            yield return Steps("push", _case.pushAfterContactSec);
            _input.ReleaseAll();
            yield return Steps("release", RELEASE_SEC);

            _input.SetKeys("Move", KeyOf(_profile.keyBack));
            yield return Steps("reverse", _case.reverseSec);
            _input.ReleaseAll();
            yield return Steps("release", RELEASE_SEC);

            foreach (char side in _case.sideOrder)
            {
                var key = side == 'L' ? KeyOf(_profile.keyLeft) : KeyOf(_profile.keyRight);
                _input.SetKeys("Move", key);
                yield return Steps(side == 'L' ? "side_left" : "side_right", _case.sideSec);
                _input.ReleaseAll();
                yield return Steps("release", RELEASE_SEC);
            }

            if (_case.rescueJump)
            {
                _input.SetKeys("Jump", KeyOf(_profile.keyJump));
                yield return Steps("rescue_jump_press", TAP_SEC);
                _input.SetKeys("Move", KeyOf(_profile.keyBack));
                yield return Steps("rescue_jump", _case.rescueSec);
                _input.ReleaseAll();
                yield return Steps("release", RELEASE_SEC);
            }
            if (_case.rescueRoll)
            {
                _input.SetKeys("Move+Roll", KeyOf(_profile.keyBack), KeyOf(_profile.keyRoll));
                yield return Steps("rescue_roll_press", TAP_SEC);
                _input.SetKeys("Roll", KeyOf(_profile.keyBack));
                yield return Steps("rescue_roll", _case.rescueSec);
                _input.ReleaseAll();
            }
            yield return Steps("observe", _case.observeSec);
        }

        private void StartMode()
        {
            // 해당 버전에 없는 이동 모드는 다른 모드로 바꿔 실행하지 않고 하네스 오류로 끝낸다.
            if ((_case.moveMode == "Boost" || _case.moveMode == "BoostSprint") && !PhysicsBenchPlayerApi.SetSpeedBoost(_player, _case.boostSpeed, 600f))
                _abortReason = "unsupported_mode:" + _case.moveMode;
        }

        private string MoveActionName()
        {
            switch (_case.moveMode)
            {
                case "Sprint":
                case "BoostSprint":
                    return "Move+Sprint";
                case "Roll":
                    return "Move+Roll";
                default:
                    return "Move";
            }
        }

        private Key[] MoveKeys(Key direction)
        {
            switch (_case.moveMode)
            {
                case "Sprint":
                case "BoostSprint":
                    return new[] { direction, KeyOf(_profile.keySprint) };
                case "Roll":
                    return new[] { direction, KeyOf(_profile.keyRoll) };
                default:
                    return new[] { direction };
            }
        }

        // --- 스텝·관측 ---

        private IEnumerator Steps(string phase, float sec)
        {
            Mark(phase);
            float t0 = Time.fixedTime;
            while (Time.fixedTime - t0 < sec - 1e-5f)
                yield return OneStep();
        }

        private IEnumerator OneStep()
        {
            yield return new WaitForFixedUpdate();
            _lastStepRealtime = Time.realtimeSinceStartup;
            Record();
            _step++;
        }

        private void Mark(string phase)
        {
            _phase = phase;
            _phases.Add(new BenchPhaseMark { phase = phase, step = _step, fixedTime = Time.fixedTime, keys = HeldKeys() });
        }

        private string HeldKeys()
        {
            var k = _input.Keyboard;
            return string.Join("+", k.allKeys.Where(x => x.isPressed).Select(x => x.keyCode.ToString()));
        }

        private void Record()
        {
            CapsuleWorld(out Vector3 p0, out Vector3 p1, out float r, out float halfH);
            Vector3 center = (p0 + p1) * 0.5f;
            float bottomY = Mathf.Min(p0.y, p1.y) - r;
            var row = new BenchTraceRow
            {
                caseId = _case.id,
                attempt = _attempt,
                step = _step,
                t = Time.fixedTime,
                phase = _phase,
                p = V(_rb.position),
                v = V(_rb.linearVelocity),
                bottomY = bottomY,
                input = new[] { PhysicsBenchPlayerApi.InputDir(_player).x, PhysicsBenchPlayerApi.InputDir(_player).y },
                yaw = PhysicsBenchPlayerApi.Yaw(_player),
                grounded = PhysicsBenchPlayerApi.IsGrounded(_player),
                rolling = _rolling,
            };

            var fwd = Forward();
            var all = new List<PhysicsBenchContactRecorder.ContactSample>();
            foreach (var rec in _recorders)
                rec.Drain(all);
            row.selfContacts = all.Count(c => c.self);
            var cs = all.Where(c => !c.self).ToList();
            row.contactIds = cs.Select(c => Register(c.collider)).ToArray();
            row.contactBody = cs.Select(c => c.body).ToArray();
            row.contactNormalY = cs.Select(c => c.normal.y).ToArray();
            row.contactNormalDot = cs.Select(c => Vector3.Dot(c.normal, fwd)).ToArray();
            if (HasWallPlane(out Vector3 wp, out Vector3 wn))
                row.contactWallDist = cs.Select(c => Vector3.Dot(c.point - wp, -wn)).ToArray();
            _wallContact = false;
            foreach (var c in cs)
            {
                if (Mathf.Abs(c.normal.y) >= 0.7f)
                    continue;
                if (IsTargetWall(c))
                    _wallContact = true;
                else
                    row.otherWallContacts++;
            }
            row.targetWallContact = _wallContact;
            RecordChildren(row);

            // 발 아래 짧은 검사: 캡슐 바닥에서 반지름 2배 이내의 지지면만 본다.
            float probeRadius = r * 0.8f;
            Vector3 origin = new Vector3(center.x, bottomY + r * 2f, center.z);
            int n = Physics.SphereCastNonAlloc(origin, probeRadius, Vector3.down, _hits, r * 4f, ~0, QueryTriggerInteraction.Ignore);
            if (n >= _hits.Length)
                row.overlapSaturated = true;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (IsPlayer(h.collider) || h.distance <= 0f)
                    continue;
                float gap = bottomY - h.point.y;
                if (h.distance < best)
                {
                    best = h.distance;
                    row.probeId = Register(h.collider);
                    row.probeDist = gap;
                    row.probeNormalY = h.normal.y;
                }
            }

            int m = Physics.OverlapCapsuleNonAlloc(p0, p1, r + 0.002f, _overlap, ~0, QueryTriggerInteraction.Ignore);
            // 버퍼가 가득 차면 일부 충돌체를 못 본 것이다. 결과를 버리지 않고 포화 사실을 남긴다.
            if (m >= _overlap.Length)
                row.overlapSaturated = true;
            for (int i = 0; i < m; i++)
            {
                var c = _overlap[i];
                if (IsPlayer(c))
                    continue;
                if (!Physics.ComputePenetration(_capsule, _rb.position, _rb.rotation,
                        c, c.transform.position, c.transform.rotation, out Vector3 dir, out float depth))
                    continue;
                if (depth > row.penDepth)
                {
                    row.penDepth = depth;
                    row.penId = Register(c);
                    row.penDirY = dir.y;
                }
                // 바닥 접촉의 미세 침투가 벽 침투를 가리지 않도록 수평 방향 침투를 따로 기록한다.
                if (Mathf.Abs(dir.y) < 0.7f && depth > row.penWallDepth)
                {
                    row.penWallDepth = depth;
                    row.penWallId = Register(c);
                }
            }

            _trace.Append(JsonUtility.ToJson(row)).Append('\n');
        }

        private bool HasWallPlane(out Vector3 point, out Vector3 normal)
        {
            var wn = _case.wallNormal;
            var wp = _case.wallPoint;
            point = normal = Vector3.zero;
            if (wn == null || wn.Length != 3 || (wn[0] == 0f && wn[1] == 0f && wn[2] == 0f))
                return false;
            normal = new Vector3(wn[0], wn[1], wn[2]).normalized;
            point = wp != null && wp.Length == 3 ? new Vector3(wp[0], wp[1], wp[2]) : Vector3.zero;
            return wp != null && wp.Length == 3;
        }

        // 대상 벽 접점: 법선이 기준 벽 법선과 같은 방향이고, 접점이 기준 벽면 가까이 있으며,
        // 대상 collider 목록이 있으면 그 collider여야 한다. 인접 장애물 접촉은 여기서 빠진다.
        private bool IsTargetWall(PhysicsBenchContactRecorder.ContactSample c)
        {
            if (_case.wallTargets != null && _case.wallTargets.Length > 0)
            {
                string path = PathOf(c.collider.transform);
                if (!_case.wallTargets.Any(t => !string.IsNullOrEmpty(t) && path.StartsWith(t, StringComparison.Ordinal)))
                    return false;
            }
            if (!HasWallPlane(out Vector3 wp, out Vector3 wn))
                return Vector3.Dot(c.normal, Forward()) < -0.5f;
            if (Vector3.Dot(c.normal, wn) <= 0.7f)
                return false;
            float tol = _case.wallPlaneTolerance > 0f ? _case.wallPlaneTolerance : 2f * _trial.capsuleRadiusWorld;
            return Mathf.Abs(Vector3.Dot(c.point - wp, wn)) <= tol;
        }

        private void RecordChildren(BenchTraceRow row)
        {
            var rel = new List<float>();
            for (int i = 1; i < _bodies.Length; i++)
            {
                Vector3 d = _bodies[i].position - _rb.position;
                rel.Add(d.x);
                rel.Add(d.y);
                rel.Add(d.z);
            }
            row.bodyRel = rel.ToArray();

            foreach (var pc in _playerColliders)
            {
                if (Physics.ComputePenetration(_capsule, _rb.position, _rb.rotation,
                        pc, pc.transform.position, pc.transform.rotation, out _, out float selfDepth))
                    row.selfPenDepth = Mathf.Max(row.selfPenDepth, selfDepth);

                var b = pc.bounds;
                int m = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.002f, _overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (m >= _overlap.Length)
                    row.overlapSaturated = true;
                for (int i = 0; i < m; i++)
                {
                    var c = _overlap[i];
                    if (IsPlayer(c))
                        continue;
                    if (!Physics.ComputePenetration(pc, pc.transform.position, pc.transform.rotation,
                            c, c.transform.position, c.transform.rotation, out Vector3 dir, out float depth))
                        continue;
                    if (depth > row.childPenDepth)
                    {
                        row.childPenDepth = depth;
                        row.childPenId = Register(c);
                        row.childPenDirY = dir.y;
                    }
                    if (Mathf.Abs(dir.y) < 0.7f)
                        row.childPenWallDepth = Mathf.Max(row.childPenWallDepth, depth);
                }
            }
        }

        private void Update()
        {
            // 첫 씬 로드의 오류로 이미 열린 콘솔도 닫는다.
            if (Debug.developerConsoleVisible)
                Debug.developerConsoleVisible = false;
            _frames++;
            _maxFrameMs = Mathf.Max(_maxFrameMs, Time.unscaledDeltaTime * 1000f);
            if (_trialCo == null || _abortReason != null)
                return;
            float now = Time.realtimeSinceStartup;
            if (Time.timeScale == 0f)
                _abortReason = "time_scale_zero";
            else if (now - _trialRealStart > _trialBudgetReal)
                _abortReason = "trial_realtime_budget_exceeded";
            else if (_sceneReady && now - _lastStepRealtime > STALL_REALTIME_SEC)
                _abortReason = "fixed_step_stalled";
        }

        private void OnGUI()
        {
            if (_status == null)
                return;
            GUI.Box(new Rect(10, 10, 420, 80), "");
            GUI.Label(new Rect(20, 15, 400, 20), $"Physics bench {_runId}  {_status.finished}/{_status.planned}");
            GUI.Label(new Rect(20, 35, 400, 20), $"case {_status.currentCase}  phase {_phase}");
            if (GUI.Button(new Rect(20, 58, 160, 24), "Stop (save & quit)"))
                _userStop = true;
        }

        private void OnApplicationQuit()
        {
            if (_status != null && _status.state == "running")
            {
                _status.state = "interrupted";
                _status.message = "application quit during run";
                WriteStatus();
            }
        }

        // --- 씬 준비 ---

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Awake·OnEnable 이후, Start와 첫 물리 스텝 이전에 호출된다.
            _player = FindFirstObjectByType<PlayerMovement>();
            if (_player == null)
            {
                _sceneReady = true;
                return;
            }
            _rb = _player.GetComponent<Rigidbody>();
            _capsule = _player.GetComponent<CapsuleCollider>();
            _playerInput = _player.GetComponent<PlayerInput>();
            _bodies = _player.GetComponentsInChildren<Rigidbody>(true);
            _playerColliders = _player.GetComponentsInChildren<Collider>(true).Where(c => c != _capsule && !c.isTrigger).ToArray();
            _childLocalPos = _bodies.Select(b => b.transform.localPosition).ToArray();
            _childLocalRot = _bodies.Select(b => b.transform.localRotation).ToArray();
            _recorders.Clear();
            for (int i = 0; i < _bodies.Length; i++)
            {
                var rec = _bodies[i].gameObject.AddComponent<PhysicsBenchContactRecorder>();
                rec.Init(_player.transform, i);
                _recorders.Add(rec);
            }

            ApplyControls();
            if (_dtOverride > 0f)
            {
                Time.fixedDeltaTime = _dtOverride;
                _controls.Add("override:fixedDeltaTime=" + _dtOverride.ToString("R"));
            }
            if (!string.IsNullOrEmpty(_ccdOverride))
            {
                _rb.collisionDetectionMode = (CollisionDetectionMode)Enum.Parse(typeof(CollisionDetectionMode), _ccdOverride);
                _controls.Add("override:collisionDetectionMode=" + _ccdOverride);
            }

            if (_case.environment == "fixture")
            {
                var notes = new List<string>();
                var root = PhysicsBenchFixtures.Build(_case.fixture, notes);
                _controls.AddRange(notes);
                if (root == null)
                    _abortReason = "fixture_build_failed:" + _case.fixture;
            }
            PlaceAtStart();
            // 판정기 검증용: 느린 PC의 긴 동기 씬 로드를 흉내 낸다. stall 감시가 정상 trial을 끊지 않아야 한다.
            if (_case.sabotage == "slow_load")
                System.Threading.Thread.Sleep(20000);
            // 씬 로드 시간은 물리 정지로 보지 않는다. 정지 감시 기준을 로드 완료 시점으로 옮긴다.
            _lastStepRealtime = Time.realtimeSinceStartup;
            _sceneReady = true;
        }

        private void ApplyControls()
        {
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                string type = mb.GetType().Name;
                if (_profile.deactivateObjectsWith != null && Array.IndexOf(_profile.deactivateObjectsWith, type) >= 0 && mb.gameObject.activeSelf)
                {
                    mb.gameObject.SetActive(false);
                    _controls.Add("deactivate:" + type + ":" + PathOf(mb.transform));
                }
                else if (_profile.disableComponents != null && Array.IndexOf(_profile.disableComponents, type) >= 0 && mb.enabled)
                {
                    mb.enabled = false;
                    _controls.Add("disable:" + type + ":" + PathOf(mb.transform));
                }
            }
            if (_profile.deactivatePaths != null && _profile.deactivatePaths.Length > 0)
            {
                var byPath = new Dictionary<string, GameObject>();
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    byPath[PathOf(t)] = t.gameObject;
                foreach (var path in _profile.deactivatePaths)
                {
                    if (byPath.TryGetValue(path, out var go))
                    {
                        go.SetActive(false);
                        _controls.Add("deactivatePath:" + path);
                    }
                    else
                    {
                        // 지정한 통제를 적용하지 못하면 결과를 그 조건으로 볼 수 없다.
                        _abortReason = "deactivate_path_missing:" + path;
                    }
                }
            }
            if (!string.IsNullOrEmpty(_profile.playerConstraints))
            {
                var before = _rb.constraints;
                _rb.constraints = (RigidbodyConstraints)int.Parse(_profile.playerConstraints, System.Globalization.CultureInfo.InvariantCulture);
                _controls.Add("override:constraints=" + (int)before + "->" + (int)_rb.constraints);
            }
            if (_profile.invincibleSec > 0f)
                _controls.Add(PhysicsBenchPlayerApi.SetInvincible(_player, _profile.invincibleSec)
                    ? "invincible:" + _profile.invincibleSec
                    : "invincible:unsupported(no PlayerHealth)");
            _controls.Add("adapter:" + PhysicsBenchPlayerApi.ADAPTER);
            string adapterError = PhysicsBenchPlayerApi.Check();
            if (adapterError != null)
                _abortReason = adapterError;
        }

        private void PlaceAtStart()
        {
            var s = _case.start;
            var rot = Quaternion.Euler(0f, _case.yaw, 0f);
            _player.transform.rotation = rot;
            _rb.rotation = rot;
            Physics.SyncTransforms();
            CapsuleWorld(out Vector3 p0, out Vector3 p1, out float r, out _);
            Vector3 center = (p0 + p1) * 0.5f;
            float bottomY = Mathf.Min(p0.y, p1.y) - r;
            Vector3 offset = center - _rb.position;
            float centerAboveBottom = center.y - bottomY;
            Vector3 target = new Vector3(s[0], s[1] + centerAboveBottom + SETTLE_CLEARANCE, s[2]) - offset;
            _player.transform.position = target;
            _rb.position = target;
            // 자식 body는 독립 Rigidbody라 정렬 중 주 캡슐과 상대 위치가 달라진다. 씬 로드 직후 자세로 되돌리고 모두 정지시킨다.
            for (int i = 1; i < _bodies.Length; i++)
            {
                var bt = _bodies[i].transform;
                bt.localPosition = _childLocalPos[i];
                bt.localRotation = _childLocalRot[i];
            }
            Physics.SyncTransforms();
            foreach (var b in _bodies)
            {
                b.linearVelocity = Vector3.zero;
                b.angularVelocity = Vector3.zero;
            }
        }

        // 첫 물리 스텝 전에 해소되는 초기 침투를 놓치지 않도록 배치 직후 값을 남긴다.
        private void MeasureInitialPenetration()
        {
            CapsuleWorld(out Vector3 p0, out Vector3 p1, out float r, out _);
            int m = Physics.OverlapCapsuleNonAlloc(p0, p1, r + 0.002f, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < m; i++)
            {
                var c = _overlap[i];
                if (IsPlayer(c))
                    continue;
                if (!Physics.ComputePenetration(_capsule, _rb.position, _rb.rotation,
                        c, c.transform.position, c.transform.rotation, out Vector3 dir, out float depth))
                    continue;
                _trial.initialPenDepth = Mathf.Max(_trial.initialPenDepth, depth);
                if (Mathf.Abs(dir.y) < 0.7f)
                    _trial.initialPenWallDepth = Mathf.Max(_trial.initialPenWallDepth, depth);
            }
        }

        private IEnumerator AlignYaw(float target)
        {
            // 실제 Look 경로(가상 마우스 delta)로 Yaw를 맞춘다. 감도는 관측된 변화량으로 추정한다.
            // Look이 New Input System에 없는 버전(Hm1)은 어댑터가 준비 단계에서 카메라 yaw를 직접 맞춘다.
            if (!PhysicsBenchPlayerApi.HAS_LOOK)
            {
                if (!PhysicsBenchPlayerApi.PrepareYaw(_player, target))
                {
                    _abortReason = "yaw_prepare_failed";
                    yield break;
                }
                _controls.Add("yaw:camera_field_set");
                // LateUpdate가 플레이어 회전에 반영할 때까지 기다린 뒤 확인한다.
                yield return null;
                yield return null;
                if (Mathf.Abs(Mathf.DeltaAngle(PhysicsBenchPlayerApi.Yaw(_player), target)) >= YAW_TOLERANCE)
                    _abortReason = "yaw_align_failed";
                yield break;
            }
            float scale = 0.15f;
            for (int i = 0; i < YAW_MAX_ITERATIONS; i++)
            {
                float before = PhysicsBenchPlayerApi.Yaw(_player);
                float diff = Mathf.DeltaAngle(before, target);
                if (Mathf.Abs(diff) < YAW_TOLERANCE)
                    yield break;
                float dx = diff / scale;
                _input.LookDelta(dx);
                yield return null;
                _input.LookZero();
                yield return null;
                yield return null;
                float moved = Mathf.DeltaAngle(before, PhysicsBenchPlayerApi.Yaw(_player));
                if (Mathf.Abs(moved) > 1e-4f)
                    scale = moved / dx;
            }
            if (Mathf.Abs(Mathf.DeltaAngle(PhysicsBenchPlayerApi.Yaw(_player), target)) >= YAW_TOLERANCE)
                _abortReason = "yaw_align_failed";
        }

        private void OnRollActive(bool active) => _rolling = active;

        // --- 기록 ---

        private void FinishTrial(string status, string error)
        {
            if (_trial == null)
                return;
            if (_player != null)
                PhysicsBenchPlayerApi.UnsubscribeRoll(_player, OnRollActive);
            _trial.status = status;
            _trial.harnessError = error;
            _trial.endRealtime = Time.realtimeSinceStartup;
            _trial.startRealtime = _trialRealStart;
            _trial.steps = _step;
            _trial.phases = _phases.ToArray();
            _trial.inputs = _input.Events.ToArray();
            _trial.colliders = _colliders.Values.ToArray();
            _trial.controls = _controls.ToArray();
            _trial.logErrors = _logErrors.Take(50).ToArray();
            _trial.frames = _frames;
            _trial.maxFrameMs = _maxFrameMs;
            File.AppendAllText(Path.Combine(_outDir, "trace.jsonl"), _trace.ToString());
            File.AppendAllText(Path.Combine(_outDir, "trials.jsonl"), JsonUtility.ToJson(_trial) + "\n");
            if (status != "completed")
                _status.harnessErrors++;
            _trace.Clear();
            _trial = null;
        }

        private int Register(Collider c)
        {
            int id = c.GetInstanceID();
            if (!_colliders.ContainsKey(id))
            {
                _colliders[id] = new BenchColliderInfo
                {
                    id = id,
                    path = PathOf(c.transform),
                    type = c.GetType().Name,
                    tag = c.tag,
                    layer = c.gameObject.layer,
                    trigger = c.isTrigger,
                    convex = c is MeshCollider mc && mc.convex,
                };
            }
            return id;
        }

        private bool IsPlayer(Collider c) => c.transform == _player.transform || c.transform.IsChildOf(_player.transform);

        private void CapsuleWorld(out Vector3 p0, out Vector3 p1, out float r, out float halfH)
        {
            // 보간 중인 transform 대신 물리 스텝 결과인 Rigidbody 포즈를 쓴다. 캡슐은 Rigidbody 루트에 붙어 있다.
            Vector3 s = _capsule.transform.lossyScale;
            Quaternion rot = _rb.rotation;
            int dir = _capsule.direction;
            float axisScale = Mathf.Abs(dir == 0 ? s.x : dir == 1 ? s.y : s.z);
            float radialScale = dir == 0 ? Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))
                : dir == 1 ? Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z))
                : Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            r = _capsule.radius * radialScale;
            halfH = Mathf.Max(_capsule.height * 0.5f * axisScale, r);
            Vector3 axis = rot * (dir == 0 ? Vector3.right : dir == 1 ? Vector3.up : Vector3.forward);
            Vector3 c = _rb.position + rot * Vector3.Scale(_capsule.center, s);
            float seg = halfH - r;
            p0 = c + axis * seg;
            p1 = c - axis * seg;
        }

        private Vector3 Forward() => Quaternion.Euler(0f, _case.yaw, 0f) * Vector3.forward;

        private float HorizontalSpeed() => new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;

        private static Key KeyOf(string name) => (Key)Enum.Parse(typeof(Key), name);

        private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _logErrors.Add(type + ": " + condition);
        }

        // --- 설정·manifest ---

        private bool LoadConfig(out string error)
        {
            error = null;
            string streaming = Path.Combine(Application.streamingAssetsPath, STREAMING_DIR);
            _casesPath = Arg("-benchCases") ?? Path.Combine(streaming, "cases.json");
            string profilePath = Arg("-benchProfile") ?? Path.Combine(streaming, "profile.json");
            if (!File.Exists(_casesPath))
            {
                error = "cases_missing:" + _casesPath;
                return false;
            }
            if (!File.Exists(profilePath))
            {
                error = "profile_missing:" + profilePath;
                return false;
            }
            byte[] caseBytes = File.ReadAllBytes(_casesPath);
            _casesSha = Sha256(caseBytes);
            _caseFile = JsonUtility.FromJson<BenchCaseFile>(Encoding.UTF8.GetString(caseBytes));
            _profile = JsonUtility.FromJson<BenchProfile>(File.ReadAllText(profilePath));
            if (_caseFile?.cases == null || _caseFile.cases.Length == 0)
            {
                error = "cases_empty";
                return false;
            }

            _runId = Arg("-benchRunId") ?? DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            _seed = int.TryParse(Arg("-benchSeed"), out int seed) ? seed : 1;
            _attempt = int.TryParse(Arg("-benchAttempt"), out int attempt) ? attempt : 1;
            _targetFps = int.TryParse(Arg("-benchFps"), out int fps) ? fps : 60;
            if (float.TryParse(Arg("-benchDt"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dt))
                _dtOverride = dt;
            _ccdOverride = Arg("-benchCcd");
            _caseFilter = Arg("-benchCaseFilter");
            // 화면 캡처는 trial 종료 시점 1장. 정식 실행 용량을 고려해 기본은 끈다.
            _capture = Arg("-benchCapture") == "end";

            string outRoot = Arg("-benchOut") ?? Path.Combine(Application.persistentDataPath, "PhysicsBench");
            _outDir = Path.Combine(outRoot, _runId);
            if (Directory.Exists(_outDir) && Directory.EnumerateFileSystemEntries(_outDir).Any())
            {
                // 기존 결과를 덮어쓰지 않는다. 새 run ID로 다시 실행해야 한다.
                _outDir = null;
                error = "run_dir_exists:" + _runId;
                return false;
            }
            Directory.CreateDirectory(_outDir);
            return true;
        }

        private void WriteRunManifest()
        {
            string buildInfoPath = Path.Combine(Application.streamingAssetsPath, STREAMING_DIR, "build-info.json");
            var m = new RunManifest
            {
                runId = _runId,
                experimentId = _caseFile.experimentId,
                caseSetRevision = _caseFile.caseSetRevision,
                casesPath = _casesPath,
                casesSha256 = _casesSha,
                profileJson = JsonUtility.ToJson(_profile),
                buildInfoJson = File.Exists(buildInfoPath) ? File.ReadAllText(buildInfoPath) : null,
                seed = _seed,
                attempt = _attempt,
                targetFps = _targetFps,
                dtOverride = _dtOverride,
                ccdOverride = _ccdOverride,
                caseFilter = _caseFilter,
                capture = _capture,
                commandLine = Environment.GetCommandLineArgs(),
                startedUtc = DateTime.UtcNow.ToString("O"),
                playerLogPath = Application.consoleLogPath,
            };
            File.WriteAllText(Path.Combine(_outDir, "manifest.json"), JsonUtility.ToJson(m, true));
        }

        private void WriteEnvironment()
        {
            var e = new EnvironmentInfo
            {
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                appVersion = Application.version,
                isEditor = Application.isEditor,
                debugBuild = Debug.isDebugBuild,
                os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType,
                cpuCount = SystemInfo.processorCount,
                memoryMB = SystemInfo.systemMemorySize,
                gpu = SystemInfo.graphicsDeviceName,
                gpuVendor = SystemInfo.graphicsDeviceVendor,
                gpuDriver = SystemInfo.graphicsDeviceVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                screen = Screen.width + "x" + Screen.height,
                fullScreenMode = Screen.fullScreenMode.ToString(),
                refreshRate = Screen.currentResolution.refreshRateRatio.value.ToString("R"),
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                qualityLevel = QualitySettings.GetQualityLevel(),
                fixedDeltaTime = Time.fixedDeltaTime,
                maximumDeltaTime = Time.maximumDeltaTime,
                runInBackground = Application.runInBackground,
                focused = Application.isFocused,
                inputUpdateMode = InputSystem.settings.updateMode.ToString(),
                inputBackgroundBehavior = InputSystem.settings.backgroundBehavior.ToString(),
                physicsSimulationMode = Physics.simulationMode.ToString(),
                defaultContactOffset = Physics.defaultContactOffset,
                defaultSolverIterations = Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                gravityY = Physics.gravity.y,
            };
            File.WriteAllText(Path.Combine(_outDir, "environment.json"), JsonUtility.ToJson(e, true));
        }

        private void WriteStatus()
        {
            if (_outDir == null || _status == null)
                return;
            _status.updatedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(_outDir, "status.json"), JsonUtility.ToJson(_status, true));
        }

        private void Fail(string error)
        {
            Debug.LogError("[PhysicsBench] " + error);
            if (_outDir != null)
            {
                _status = new BenchStatus { runId = _runId, state = "failed", message = error };
                WriteStatus();
            }
            Application.Quit(1);
        }

        private static float EstimateSeconds(BenchCase c)
        {
            float s = c.settleSec + c.moveSec + c.observeSec + 2f;
            if (c.scenario == "curb")
                s += c.approachTimeoutSec + c.pushAfterContactSec + c.reverseSec + c.sideSec * 2f + c.rescueSec * 2f + 1f;
            return s;
        }

        private static List<BenchCase> Shuffle(List<BenchCase> list, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [Serializable]
        private class OrderList
        {
            public int seed;
            public string[] caseIds;
        }

        [Serializable]
        private class RunManifest
        {
            public string runId;
            public string experimentId;
            public string caseSetRevision;
            public string casesPath;
            public string casesSha256;
            public string profileJson;
            public string buildInfoJson;
            public int seed;
            public int attempt;
            public int targetFps;
            public float dtOverride;
            public string ccdOverride;
            public string caseFilter;
            public bool capture;
            public string[] commandLine;
            public string startedUtc;
            public string playerLogPath;
        }

        [Serializable]
        private class EnvironmentInfo
        {
            public string platform;
            public string unityVersion;
            public string appVersion;
            public bool isEditor;
            public bool debugBuild;
            public string os;
            public string cpu;
            public int cpuCount;
            public int memoryMB;
            public string gpu;
            public string gpuVendor;
            public string gpuDriver;
            public string graphicsApi;
            public string screen;
            public string fullScreenMode;
            public string refreshRate;
            public int vSyncCount;
            public int targetFrameRate;
            public int qualityLevel;
            public float fixedDeltaTime;
            public float maximumDeltaTime;
            public bool runInBackground;
            public bool focused;
            public string inputUpdateMode;
            public string inputBackgroundBehavior;
            public string physicsSimulationMode;
            public float defaultContactOffset;
            public int defaultSolverIterations;
            public int defaultSolverVelocityIterations;
            public float gravityY;
        }
    }

    // 플레이어의 각 Rigidbody에 붙여 접촉만 수집한다. 물리 응답에는 관여하지 않는다.
    public class PhysicsBenchContactRecorder : MonoBehaviour
    {
        public struct ContactSample
        {
            public Collider collider;
            public Vector3 normal;
            public Vector3 point;
            public int body;   // 0: 주 Rigidbody, 1 이상: 자식 Rigidbody 순번
            public bool self;  // 상대가 플레이어 자신의 콜라이더
        }

        private readonly List<ContactSample> _samples = new();
        private readonly List<ContactPoint> _buffer = new();
        private Transform _playerRoot;
        private int _body;

        public void Init(Transform playerRoot, int body)
        {
            _playerRoot = playerRoot;
            _body = body;
        }

        private void OnCollisionEnter(Collision collision) => Collect(collision);

        private void OnCollisionStay(Collision collision) => Collect(collision);

        private void Collect(Collision collision)
        {
            collision.GetContacts(_buffer);
            if (_buffer.Count == 0)
                return;
            // 접점을 평균하지 않는다. 한 collider에 바닥·벽 접점이 같이 있으면 평균 법선이 벽을 지운다.
            var other = collision.collider.transform;
            bool self = other == _playerRoot || other.IsChildOf(_playerRoot);
            foreach (var c in _buffer)
            {
                _samples.Add(new ContactSample
                {
                    collider = collision.collider,
                    normal = c.normal,
                    point = c.point,
                    body = _body,
                    self = self,
                });
            }
        }

        public void Drain(List<ContactSample> into)
        {
            into.AddRange(_samples);
            _samples.Clear();
        }

        public void Clear() => _samples.Clear();
    }
}
#endif
