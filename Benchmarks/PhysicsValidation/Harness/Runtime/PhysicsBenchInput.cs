#if PHYSICS_BENCH
// 테스트 전용 가상 키보드·마우스. 게임플레이 코드는 바꾸지 않고 PlayerInput에 이 장치만 페어링한다.
// 실제 키보드·마우스는 페어링하지 않으므로 Windows 실행 중 사용자의 입력이 개미를 움직이지 않는다.
// 요청마다 상태가 바뀐 키에 묶인 액션을 모두 "필요 액션"으로 두고, 액션별 수신을 따로 기록한다.
// (Move+Sprint 요청에서 Move만 수신되면 Sprint 미전달로 남는다.)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PhysicsBench
{
    public class PhysicsBenchInput : IDisposable
    {
        public Keyboard Keyboard { get; private set; }
        public Mouse Mouse { get; private set; }
        // 판정기 검증(sabotage=no_input)에서만 PlayerInput에 페어링하는 미사용 장치
        public Keyboard DecoyKeyboard { get; private set; }
        public Mouse DecoyMouse { get; private set; }

        private readonly List<BenchInputEvent> _events = new();
        private readonly Dictionary<InputAction, Action<InputAction.CallbackContext>> _handlers = new();
        private readonly HashSet<Key> _held = new();
        private readonly Dictionary<Key, string> _keyAction = new();
        // 판정기 검증(partial_input)용: 요청에는 남기되 장치로 보내지 않는 키
        private readonly HashSet<Key> _dropped = new();

        public IReadOnlyList<BenchInputEvent> Events => _events;

        public PhysicsBenchInput()
        {
            Keyboard = InputSystem.AddDevice<Keyboard>("PhysicsBenchKeyboard");
            Mouse = InputSystem.AddDevice<Mouse>("PhysicsBenchMouse");
            DecoyKeyboard = InputSystem.AddDevice<Keyboard>("PhysicsBenchDecoyKeyboard");
            DecoyMouse = InputSystem.AddDevice<Mouse>("PhysicsBenchDecoyMouse");
        }

        // profile의 키 바인딩으로 키→액션 표를 만든다.
        public void MapKeys(IEnumerable<(Key key, string action)> pairs)
        {
            _keyAction.Clear();
            foreach (var (key, action) in pairs)
                _keyAction[key] = action;
        }

        public void SetDropped(IEnumerable<Key> keys)
        {
            _dropped.Clear();
            foreach (var k in keys)
                _dropped.Add(k);
        }

        public void Pair(PlayerInput playerInput, string scheme, string map, bool decoy)
        {
            playerInput.neverAutoSwitchControlSchemes = true;
            if (decoy)
                playerInput.SwitchCurrentControlScheme(scheme, DecoyKeyboard, DecoyMouse);
            else
                playerInput.SwitchCurrentControlScheme(scheme, Keyboard, Mouse);
            playerInput.SwitchCurrentActionMap(map);
            playerInput.ActivateInput();
        }

        // 관측 전용 구독: 액션 수신 시각만 기록하고 게임 로직에는 관여하지 않는다.
        public void Observe(PlayerInput playerInput, params string[] actionNames)
        {
            Unobserve();
            foreach (var name in actionNames)
            {
                var action = playerInput.actions.FindAction(name);
                if (action == null)
                    continue;
                Action<InputAction.CallbackContext> handler = ctx => MarkReceived(name);
                action.performed += handler;
                action.canceled += handler;
                _handlers[action] = handler;
            }
        }

        public void Unobserve()
        {
            foreach (var pair in _handlers)
            {
                pair.Key.performed -= pair.Value;
                pair.Key.canceled -= pair.Value;
            }
            _handlers.Clear();
        }

        public void ResetLog() => _events.Clear();

        // label은 사람이 읽는 이름이다. 수신 확인 대상은 바뀐 키에서 계산한다.
        public void SetKeys(string label, params Key[] keys)
        {
            var next = new HashSet<Key>(keys);
            var changed = new HashSet<Key>(_held);
            changed.SymmetricExceptWith(next);
            _held.Clear();
            _held.UnionWith(next);
            var sent = keys.Where(k => !_dropped.Contains(k)).ToArray();
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(sent));
            Request(label, keys.Length == 0 ? "none" : string.Join("+", keys), changed,
                string.Join("+", keys.Where(k => _dropped.Contains(k))));
        }

        public void ReleaseAll(string label = "Release") => SetKeys(label);

        public void LookDelta(float dx)
        {
            InputSystem.QueueStateEvent(Mouse, new MouseState { delta = new Vector2(dx, 0f) });
            _events.Add(new BenchInputEvent
            {
                action = "Look",
                value = dx.ToString("R"),
                requiredActions = "Look",
                receivedActions = "",
                requestFixedTime = Time.fixedTime,
                requestRealtime = Time.realtimeSinceStartup,
            });
        }

        public void LookZero()
        {
            InputSystem.QueueStateEvent(Mouse, new MouseState { delta = Vector2.zero });
        }

        private void Request(string label, string value, HashSet<Key> changed, string dropped)
        {
            var required = changed.Where(k => _keyAction.ContainsKey(k)).Select(k => _keyAction[k]).Distinct().OrderBy(a => a, StringComparer.Ordinal);
            _events.Add(new BenchInputEvent
            {
                action = label,
                value = value,
                requiredActions = string.Join(",", required),
                receivedActions = "",
                droppedKeys = dropped,
                requestFixedTime = Time.fixedTime,
                requestRealtime = Time.realtimeSinceStartup,
            });
        }

        private void MarkReceived(string actionName)
        {
            // 이 액션을 기다리는 가장 오래된 요청에 수신을 기록한다.
            foreach (var e in _events)
            {
                var required = Split(e.requiredActions);
                var received = Split(e.receivedActions);
                if (!required.Contains(actionName) || received.Contains(actionName))
                    continue;
                received.Add(actionName);
                e.receivedActions = string.Join(",", received);
                if (e.firstReceivedFixedTime < 0f)
                    e.firstReceivedFixedTime = Time.fixedTime;
                if (required.All(received.Contains))
                {
                    e.receivedRealtime = Time.realtimeSinceStartup;
                    e.receivedFixedTime = Time.fixedTime;
                }
                return;
            }
        }

        private static List<string> Split(string s) =>
            string.IsNullOrEmpty(s) ? new List<string>() : s.Split(',').ToList();

        public void Dispose()
        {
            Unobserve();
            foreach (var d in new InputDevice[] { Keyboard, Mouse, DecoyKeyboard, DecoyMouse })
                if (d != null && d.added)
                    InputSystem.RemoveDevice(d);
        }
    }
}
#endif
