#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// 에디터 전용: 잠긴 선택 미션(꽃) 옆에서 E 픽업 대상 선택을 바로 확인하기 위한 테스트 도구.
// 숫자 9를 누르면 개미를 꽃 바로 옆(0.3m)에 옮기고, 그보다 먼 곳(0.6m)에 음식을 떨어뜨린다.
// 이 상태에서 E를 눌러 음식이 주워지면 정상 (잠긴 꽃이 더 가까워도 건너뛰어야 함).
public class PickupDebugTester : MonoBehaviour
{
    private const string k_FoodDataPath = "Assets/Scriptables/Item/Toast.asset";
    private const float k_PlayerOffset = 0.3f;
    private const float k_FoodOffset = 0.9f;

    private readonly InputAction _setupAction = new InputAction(
        name: "DebugPickupSetup",
        type: InputActionType.Button,
        binding: "<Keyboard>/9"
    );

    private ItemData _foodData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 게임 씬에서만 생성 (타이틀 씬 제외)
        if (FindAnyObjectByType<MissionManager>() == null)
            return;

        new GameObject("[Debug] PickupDebugTester").AddComponent<PickupDebugTester>();
    }

    private void Awake()
    {
        _foodData = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(k_FoodDataPath);
    }

    private void OnEnable()
    {
        _setupAction.performed += OnSetupPerformed;
        _setupAction.Enable();
    }

    private void OnDisable()
    {
        _setupAction.performed -= OnSetupPerformed;
        _setupAction.Disable();
    }

    private void OnSetupPerformed(InputAction.CallbackContext ctx)
    {
        MissionItem locked = FindLockedOptional();
        if (locked == null)
        {
            Debug.LogWarning("[PickupDebugTester] 잠긴 선택 미션 아이템이 없습니다. (이미 해제됐거나 주웠으면 재시작)");
            return;
        }

        if (_foodData == null || TrashDropManager.Instance == null)
        {
            Debug.LogWarning($"[PickupDebugTester] 음식 데이터({k_FoodDataPath}) 또는 TrashDropManager가 없습니다.");
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Vector3 flowerPos = locked.transform.position;
        Vector3 dir = player.transform.position - flowerPos;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        Teleport(player, GroundPoint(flowerPos + dir * k_PlayerOffset, flowerPos.y));

        Vector3 foodPos = GroundPoint(flowerPos + dir * k_FoodOffset, flowerPos.y);
        foodPos.y += _foodData.spawnHeightOffset;
        TrashDropManager.Instance.SpawnItem(_foodData, foodPos);

        Debug.Log(
            $"[PickupDebugTester] 꽃 {k_PlayerOffset}m / {_foodData.itemName} "
                + $"{k_FoodOffset - k_PlayerOffset}m 거리에 배치. E를 누르면 {_foodData.itemName}이 주워져야 합니다."
        );
    }

    private static MissionItem FindLockedOptional()
    {
        foreach (var item in FindObjectsByType<MissionItem>(FindObjectsSortMode.None))
        {
            if (!item.CanInteract)
                return item;
        }
        return null;
    }

    private static Vector3 GroundPoint(Vector3 pos, float fallbackY)
    {
        if (
            Physics.Raycast(
                pos + Vector3.up,
                Vector3.down,
                out var hit,
                3f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            )
        )
            return hit.point + Vector3.up * 0.01f;

        pos.y = fallbackY;
        return pos;
    }

    private static void Teleport(GameObject player, Vector3 pos)
    {
        if (player.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = pos;
        }
        player.transform.position = pos;
    }
}
#endif
