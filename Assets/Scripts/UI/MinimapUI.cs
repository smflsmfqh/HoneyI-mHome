using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// MissionManager보다 먼저 Awake되어 Instance가 준비되도록
[DefaultExecutionOrder(-2)]
public class MinimapUI : MonoBehaviour
{
    public static MinimapUI Instance { get; private set; }

    [Header("맵 범위 (월드 XZ 좌표 기준)")]
    [SerializeField]
    private Vector2 _mapCenter;

    [SerializeField]
    private Vector2 _mapSize = new Vector2(200f, 200f);

    [Header("UI 참조")]
    [SerializeField]
    private RectTransform _mapPanel;

    [SerializeField]
    private float _borderPadding = 4f;

    [Header("아이콘 크기")]
    [SerializeField]
    private float _defaultIconSize = 20f;

    [SerializeField]
    private float _playerIconSize = 16f;

    [SerializeField]
    private float _flowerIconSize = 30f;

    [SerializeField]
    private float _shopIconSize = 40f;

    [SerializeField]
    private float _destinationIconSize = 40f;

    [Header("기본 색상")]
    [SerializeField]
    private Color _playerColor = new Color(0.2f, 0.6f, 1f);

    [SerializeField]
    private Color _destinationColor = new Color(1f, 0.85f, 0f);

    [SerializeField]
    private Color _shopColor = new Color(1f, 0.5f, 0f);

    [SerializeField]
    private Color _itemColor = new Color(0.2f, 1f, 0.4f);

    private readonly Dictionary<MinimapMarker, RectTransform> _icons = new();
    private readonly List<MinimapMarker> _pendingRemoval = new();
    private bool _isIterating;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // OnEnable보다 늦게 실행되는 Start에서 이미 씬에 있는 마커들을 수집
    private void Start()
    {
        foreach (var marker in FindObjectsByType<MinimapMarker>(FindObjectsSortMode.None))
        {
            if (marker.isActiveAndEnabled)
                Register(marker);
        }
    }

    private void LateUpdate()
    {
        _isIterating = true;

        foreach (var kvp in _icons)
        {
            // 파괴된 오브젝트 처리 (Destroy 후 Unity == null 연산자 활용)
            if (kvp.Key == null)
            {
                _pendingRemoval.Add(kvp.Key);
                continue;
            }

            kvp.Value.anchoredPosition = WorldToMinimap(kvp.Key.transform.position);

            // 플레이어 아이콘은 캐릭터 방향에 맞춰 회전
            if (kvp.Key.type == MinimapMarker.MarkerType.Player)
                kvp.Value.localEulerAngles = new Vector3(0f, 0f, -kvp.Key.transform.eulerAngles.y);
        }

        _isIterating = false;

        foreach (var m in _pendingRemoval)
            RemoveIcon(m);
        _pendingRemoval.Clear();
    }

    public void Register(MinimapMarker marker)
    {
        if (_icons.ContainsKey(marker))
            return;

        var go = new GameObject($"MinimapIcon_{marker.type}_{marker.name}");
        go.transform.SetParent(_mapPanel, false);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        if (marker.iconSprite != null)
        {
            img.sprite = marker.iconSprite;
            img.color = Color.white;
        }
        else
        {
            img.color = ResolveColor(marker);
        }

        float size = marker.type switch
        {
            MinimapMarker.MarkerType.Player => _playerIconSize,
            MinimapMarker.MarkerType.Shop => _shopIconSize,
            MinimapMarker.MarkerType.Destination => _destinationIconSize,
            MinimapMarker.MarkerType.Flower => _flowerIconSize,
            _ => _defaultIconSize,
        };
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        _icons[marker] = rt;
    }

    // 등록 뒤에 colorOverride가 바뀐 마커의 아이콘 색을 다시 적용
    public void RefreshColor(MinimapMarker marker)
    {
        if (marker.iconSprite != null || !_icons.TryGetValue(marker, out var rt) || rt == null)
            return;

        rt.GetComponent<Image>().color = ResolveColor(marker);
    }

    public void Unregister(MinimapMarker marker)
    {
        // LateUpdate 순회 중에는 즉시 제거하지 않고 대기열에 추가
        if (_isIterating)
        {
            _pendingRemoval.Add(marker);
            return;
        }
        RemoveIcon(marker);
    }

    private void RemoveIcon(MinimapMarker marker)
    {
        if (!_icons.TryGetValue(marker, out var rt))
            return;
        if (rt != null)
            Destroy(rt.gameObject);
        _icons.Remove(marker);
    }

    // 월드 XZ 좌표 → 미니맵 패널 내 anchoredPosition
    private Vector2 WorldToMinimap(Vector3 worldPos)
    {
        float u = (worldPos.x - _mapCenter.x) / _mapSize.x;
        float v = (worldPos.z - _mapCenter.y) / _mapSize.y;

        float halfW = _mapPanel.rect.width * 0.5f - _borderPadding;
        float halfH = _mapPanel.rect.height * 0.5f - _borderPadding;

        return new Vector2(
            Mathf.Clamp(u * _mapPanel.rect.width, -halfW, halfW),
            Mathf.Clamp(v * _mapPanel.rect.height, -halfH, halfH)
        );
    }

    public void PingMarker(MinimapMarker marker)
    {
        if (!_icons.TryGetValue(marker, out var rt))
            return;

        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOScale(Vector3.one * 1.5f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    public void StopPingMarker(MinimapMarker marker)
    {
        if (!_icons.TryGetValue(marker, out var rt))
            return;

        rt.DOKill();
        rt.localScale = Vector3.one;
    }

    private Color ResolveColor(MinimapMarker marker)
    {
        // colorOverride가 설정되어 있으면 우선 적용 (alpha > 0 체크)
        if (marker.colorOverride.a > 0f)
            return marker.colorOverride;

        return marker.type switch
        {
            MinimapMarker.MarkerType.Player => _playerColor,
            MinimapMarker.MarkerType.Destination => _destinationColor,
            MinimapMarker.MarkerType.Shop => _shopColor,
            MinimapMarker.MarkerType.Item => _itemColor,
            _ => Color.white,
        };
    }
}
