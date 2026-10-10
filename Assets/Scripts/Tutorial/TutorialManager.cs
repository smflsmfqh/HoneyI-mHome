using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private MissionMessageUI _messageUI;

    [Header("분노 시스템")]
    [SerializeField]
    private AngerSystem _angerSystem;

    [Header("튜토리얼 고양이")]
    [SerializeField]
    private CatMovement _catPrefab;

    [SerializeField]
    private float _catSpawnDistance = 6f;

    [SerializeField]
    private float _catLifetime = 8f;

    [Header("플레이어")]
    [SerializeField]
    private PlayerHealth _player;

    [SerializeField]
    private SphereCollider _movementZone;

    [Header("약국 진입 트리거")]
    [SerializeField]
    private TriggerZone _pharmacyZone;

    [Header("방향 화살표 파티클")]
    [SerializeField]
    private ParticleSystem _arrowParticle;

    [Header("스킵 입력")]
    [SerializeField]
    private InputAction _skipAction = new InputAction(
        name: "SkipTutorial",
        type: InputActionType.Button,
        binding: "<Keyboard>/q"
    );

    [Header("튜토리얼 패널 존")]
    [SerializeField]
    private GameObject _skipTextZone;

    [SerializeField]
    private GameObject _keyInfoTextZone;

    [SerializeField]
    private GameObject _pressCZone;

    private bool _active = true;
    private bool _firstMissionDisplayed;
    private bool _pharmacyEntered;
    private bool _energyDrinkCollected;
    private Coroutine _mainCo;

    private NPCMovement[] _pausedNPCs;
    private CatMovement[] _pausedCats;
    private CarMovement[] _pausedCars;

    private static readonly float FastSlide = 4f / 60f;
    private static readonly float NormalSlide = 0.3f;

    private void Awake()
    {
        // TutorialPanel 부모가 에디터에서 비활성화되어 있으므로 먼저 활성화
        _skipTextZone?.transform.parent?.gameObject.SetActive(true);
        _skipTextZone?.SetActive(true);
        _keyInfoTextZone?.SetActive(true);
        _messageUI?.SetSlideDuration(FastSlide);

        // 화살표 파티클: 약국 진입 전까지 꺼둠
        if (_arrowParticle != null)
            _arrowParticle.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _skipAction.performed += OnSkipPerformed;
        _skipAction.Enable();
    }

    private void OnDisable()
    {
        _skipAction.performed -= OnSkipPerformed;
        _skipAction.Disable();
    }

    private void OnSkipPerformed(InputAction.CallbackContext ctx)
    {
        if (_active)
            Skip();
    }

    private void Start()
    {
        MissionManager.Instance.PauseMissionAssignment();
        _angerSystem?.Pause();

        if (_player != null)
            _player.SetTutorialInvincible(true);

        _pausedNPCs = FindObjectsByType<NPCMovement>(FindObjectsSortMode.None);
        foreach (var npc in _pausedNPCs)
            npc.SetExternalPause(true);

        _pausedCats = FindObjectsByType<CatMovement>(FindObjectsSortMode.None);
        foreach (var cat in _pausedCats)
            cat.SetExternalPause(true);

        _pausedCars = FindObjectsByType<CarMovement>(FindObjectsSortMode.None);
        foreach (var car in _pausedCars)
            car.SetTutorialPause(true);

        if (_pharmacyZone == null)
            _pharmacyZone = FindFirstObjectByType<TriggerZone>();
        if (_pharmacyZone != null)
            _pharmacyZone.OnPlayerEntered += OnPharmacyEntered;

        // 재시작(게임오버 후)이면 튜토리얼 자동 스킵
        if (PlayerPrefs.GetInt("SkipTutorial", 0) == 1)
        {
            PlayerPrefs.DeleteKey("SkipTutorial");
            Skip();
            return;
        }

        _mainCo = StartCoroutine(RunTutorial());
    }

    private void Update()
    {
        UpdateArrowParticle();
    }

    private void UpdateArrowParticle()
    {
        if (_arrowParticle == null || !_arrowParticle.gameObject.activeSelf)
            return;
        if (_player == null || _pharmacyZone == null)
            return;

        Vector3 playerPos = _player.transform.position;
        Vector3 targetPos = _pharmacyZone.transform.position;
        Vector3 dir = targetPos - playerPos;
        dir.y = 0f;

        _arrowParticle.transform.position = new Vector3(playerPos.x, 0f, playerPos.z);

        if (dir.sqrMagnitude > 0.01f)
            _arrowParticle.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private IEnumerator RunTutorial()
    {
        // 1. anger_0/1/2 메시지가 모두 큐잉될 때까지 대기
        yield return new WaitUntil(() => _angerSystem == null || _angerSystem.IntroQueued);

        // 1-1. 이동·카메라·조작 안내 (이동 감지 전에 방법 먼저 알려주기)
        yield return EnqueueAndWait("TUT_WASD");
        yield return EnqueueAndWait("TUT_MOUSE");
        yield return EnqueueAndWait("TUT_CONTROLS");

        // 2. 분노 메시지 큐잉 이후부터 스피어 이탈을 유효한 트리거로 인정
        Vector3 startPos = _player != null ? _player.transform.position : Vector3.zero;
        float radius =
            _movementZone != null
                ? _movementZone.radius * _movementZone.transform.lossyScale.x
                : 3f;
        yield return new WaitUntil(() =>
            _player != null && Vector3.Distance(_player.transform.position, startPos) >= radius
        );

        // 3. TUT_CAT_WARNING 슬라이드인 시점에 고양이 스폰 + C키 안내 UI 활성화
        bool catDone = false;
        _messageUI.EnqueueTutorialMessage(
            "TUT_CAT_WARNING",
            () =>
            {
                SpawnTutorialCat();
                _pressCZone?.SetActive(true);
                catDone = true;
            }
        );
        yield return new WaitUntil(() => catDone);

        // 5. 고양이 처리 시간 대기 후 게임 재개 + 첫 미션(약국) 할당
        yield return new WaitForSeconds(_catLifetime);
        ResumeGame();

        // 미션 할당 직후 픽업 콜백 등록 — step 7 대기 중 픽업해도 놓치지 않음
        MissionManager.Instance.OnMissionCompleted += OnEnergyDrinkCollected;

        // 6. 약국 미션 메시지가 화면에 표시될 때까지 대기
        MissionManager.Instance.OnMissionDisplayed += OnFirstMissionDisplayed;
        yield return new WaitUntil(() => _firstMissionDisplayed);

        // 7. 지도 + 방향 화살표 설명 (약국 콜라이더 진입 전 + 아이템 미픽업 상태인 경우만)
        if (!_pharmacyEntered && !_energyDrinkCollected)
        {
            yield return EnqueueAndWait("TUT_MAP");

            // TUT_MAP 대기 중 약국 진입 또는 아이템 픽업이 일어났을 수 있으므로 재검사
            if (!_pharmacyEntered && !_energyDrinkCollected)
            {
                if (_arrowParticle != null)
                {
                    _arrowParticle.gameObject.SetActive(true);
                    _arrowParticle.Play(true);
                }

                yield return EnqueueAndWait("TUT_ARROW");
            }
        }

        // 8. 약국 숙취해소제 픽업 대기
        yield return new WaitUntil(() => _energyDrinkCollected);

        // 9. 픽업 완료 후 미션 목표 안내
        yield return EnqueueAndWait("TUT_GOAL");

        // 10. 다음 미션 즉시 할당
        MissionManager.Instance?.ForceAssignNext();

        Finalize();
    }

    // 약국 앞 콜라이더 진입 — 약국 미션이 표시된 후에만 아이템 튜토리얼 출력
    private void OnPharmacyEntered()
    {
        _pharmacyEntered = true;
        if (_firstMissionDisplayed)
            ShowItemTutorial();
        // 아직 미션 미표시 상태면 OnFirstMissionDisplayed에서 처리
    }

    // 첫 미션(약국) 표시 완료 콜백
    private void OnFirstMissionDisplayed(ItemData _)
    {
        _firstMissionDisplayed = true;
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionDisplayed -= OnFirstMissionDisplayed;

        // 미션 표시 전에 이미 약국 콜라이더에 진입했다면 지금 아이템 튜토리얼 표시
        if (_pharmacyEntered)
            ShowItemTutorial();
    }

    private void ShowItemTutorial()
    {
        if (_energyDrinkCollected)
            return;
        _messageUI.EnqueueFrontTutorialMessages("TUT_ITEM_GLOW", "TUT_ITEM_PICKUP");
    }

    // 약국 숙취해소제 픽업 완료 콜백
    private void OnEnergyDrinkCollected(ItemData _)
    {
        _energyDrinkCollected = true;
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionCompleted -= OnEnergyDrinkCollected;
    }

    private IEnumerator EnqueueAndWait(string csvKey)
    {
        bool done = false;
        _messageUI.EnqueueTutorialMessage(csvKey, () => done = true);
        yield return new WaitUntil(() => done);
    }

    private void SpawnTutorialCat()
    {
        if (_catPrefab == null || _player == null)
            return;

        Vector3 dir = (_player.transform.forward + Random.insideUnitSphere * 0.5f).normalized;
        Vector3 origin = _player.transform.position + dir * _catSpawnDistance;
        origin.y = _player.transform.position.y;

        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, _catSpawnDistance, NavMesh.AllAreas))
            origin = hit.position;

        var cat = Instantiate(_catPrefab, origin, Quaternion.identity);
        cat.MarkAsTutorialCat();
        cat.SetPlayer(_player); // 근접 경고 등록·해제는 CatMovement가 Start/OnDestroy에서 직접 처리

        StartCoroutine(DestroyCatAfter(cat));
    }

    private IEnumerator DestroyCatAfter(CatMovement cat)
    {
        yield return new WaitForSeconds(_catLifetime);
        if (cat != null)
            Destroy(cat.gameObject);
        _pressCZone?.SetActive(false);
    }

    public void Skip()
    {
        if (_mainCo != null)
            StopCoroutine(_mainCo);

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionDisplayed -= OnFirstMissionDisplayed;
            MissionManager.Instance.OnMissionCompleted -= OnEnergyDrinkCollected;
        }

        // 약국 트리거 비활성화 — 스킵 후 약국 진입해도 아이템 튜토리얼 안 뜨도록
        if (_pharmacyZone != null)
        {
            _pharmacyZone.OnPlayerEntered -= OnPharmacyEntered;
            _pharmacyZone.gameObject.SetActive(false);
        }

        // 메시지 큐 즉시 비우기
        _messageUI?.ClearQueue();

        // 스킵 UI 즉시 비활성화
        _skipTextZone?.transform.parent?.gameObject.SetActive(false);
        _skipTextZone?.SetActive(false);
        _keyInfoTextZone?.SetActive(false);

        _mainCo = StartCoroutine(RunSkip());
    }

    private IEnumerator RunSkip()
    {
        // 타이머 포함해서 즉시 게임 재개
        ResumeGame(resumeTimers: true);
        yield return EnqueueAndWait("TUT_GOAL");
        Finalize(resumeTimers: false);
    }

    // NPC/차/고양이 재개 + 플레이어 무적 해제 + 약국 미션 메시지 표시
    private void ResumeGame(bool resumeTimers = false)
    {
        _active = false;
        _messageUI?.SetSlideDuration(NormalSlide);

        if (_pausedNPCs != null)
            foreach (var npc in _pausedNPCs)
                if (npc != null)
                    npc.SetExternalPause(false);
        if (_pausedCats != null)
            foreach (var cat in _pausedCats)
                if (cat != null)
                    cat.SetExternalPause(false);
        if (_pausedCars != null)
            foreach (var car in _pausedCars)
                if (car != null)
                    car.SetTutorialPause(false);

        if (_player != null)
            _player.SetTutorialInvincible(false);

        MissionManager.Instance?.ForceAssignNext();

        if (resumeTimers)
        {
            MissionManager.Instance?.ResumeMissionAssignment();
            _angerSystem?.Resume();
        }
    }

    // UI 정리 + TutorialManager 비활성화
    // resumeTimers=true(기본)면 분노 게이지·미션 타이머도 재개
    private void Finalize(bool resumeTimers = true)
    {
        if (resumeTimers)
        {
            MissionManager.Instance?.ResumeMissionAssignment();
            _angerSystem?.Resume();
        }

        if (_arrowParticle != null)
        {
            _arrowParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _arrowParticle.gameObject.SetActive(false);
        }
        _skipTextZone?.transform.parent?.gameObject.SetActive(false);
        _skipTextZone?.SetActive(false);
        _keyInfoTextZone?.SetActive(false);
        _pressCZone?.SetActive(false);
        gameObject.SetActive(false);
    }
}
