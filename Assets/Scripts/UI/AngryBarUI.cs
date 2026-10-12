using UnityEngine;
using UnityEngine.UI;

public class AngryBarUI : MonoBehaviour
{
    [SerializeField]
    private Sprite _queenFace;

    [SerializeField]
    private Sprite _angryQueenFace;

    [SerializeField]
    private Sprite _veryAngryQueenFace;

    [SerializeField]
    private Image _faceImage;

    [SerializeField]
    private Image _fillImage;

    [SerializeField]
    private AngerSystem _angerSystem;

    [SerializeField]
    private Gradient _colorGradient;

    private int _faceLevel = -1; // 0 기본 / 1 화남 / 2 매우 화남

    private void Awake()
    {
        SetFaceLevel(0);
    }

    private void OnEnable()
    {
        if (_angerSystem != null)
            _angerSystem.OnAngerChanged += UpdateBar;
    }

    private void OnDisable()
    {
        if (_angerSystem != null)
            _angerSystem.OnAngerChanged -= UpdateBar;
    }

    private void UpdateBar(float normalized)
    {
        SetFaceLevel(normalized >= 0.7f ? 2 : normalized >= 0.5f ? 1 : 0);

        _fillImage.fillAmount = normalized;
        _fillImage.color = _colorGradient.Evaluate(normalized);
    }

    // 표정은 단계가 바뀔 때만 교체 (fill·색은 분노가 매 프레임 변하므로 매번 갱신)
    private void SetFaceLevel(int level)
    {
        if (_faceLevel == level)
            return;
        _faceLevel = level;
        _faceImage.sprite = level switch
        {
            2 => _veryAngryQueenFace,
            1 => _angryQueenFace,
            _ => _queenFace,
        };
    }
}
