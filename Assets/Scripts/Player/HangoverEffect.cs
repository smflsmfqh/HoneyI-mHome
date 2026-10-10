using UnityEngine;

public class HangoverEffect : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem _particle;

    [SerializeField]
    private Material _hangoverMaterial;

    [SerializeField]
    private float _hangoverSpeedMultiplier = 0.6f;

    [SerializeField]
    private float _flickerSpeed = 3f; // 깜빡임 빠르기

    private Renderer[] _renderers;
    private Material[][] _originalMaterials;
    private Material _ghostMatInstance;
    private PlayerMovement _playerMovement;
    private bool _active = false;

    private void Start()
    {
        if (_particle != null)
        {
            // 게임 시간과 함께 멈추도록 scaled 시간 사용 (예전 튜토리얼의 timeScale 정지용 unscaled 설정 제거)
            var main = _particle.main;
            main.useUnscaledTime = false;
            _particle.Play();
        }

        if (_hangoverMaterial != null)
        {
            // 공유 머티리얼이 아닌 인스턴스를 사용
            _ghostMatInstance = new Material(_hangoverMaterial);

            _renderers = GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_renderers.Length][];

            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalMaterials[i] = _renderers[i].materials;
                var mats = new Material[_originalMaterials[i].Length + 1];
                _originalMaterials[i].CopyTo(mats, 0);
                mats[mats.Length - 1] = _ghostMatInstance;
                _renderers[i].materials = mats;
            }

            _active = true;
        }

        _playerMovement = GetComponent<PlayerMovement>();
        _playerMovement?.SetHangoverDebuff(_hangoverSpeedMultiplier);

        MissionManager.Instance.OnMissionCompleted += HandleMissionCompleted;
    }

    private void Update()
    {
        if (!_active || _ghostMatInstance == null)
            return;

        // 0~1 사인 파형으로 _Transparency 깜빡임
        float t = Mathf.Sin(Time.time * _flickerSpeed) * 0.5f + 0.5f;
        _ghostMatInstance.SetFloat("_Transparency", t);
    }

    private void OnDestroy()
    {
        if (_ghostMatInstance != null)
            Destroy(_ghostMatInstance);

        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
    }

    private void HandleMissionCompleted(ItemData item)
    {
        if (item == null || item.itemName.ToUpper() != "ENERGYDRINK")
            return;

        _active = false;

        if (_particle != null)
        {
            _particle.Stop();
            _particle.gameObject.SetActive(false);
        }

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].materials = _originalMaterials[i];
            }
        }

        if (_ghostMatInstance != null)
        {
            Destroy(_ghostMatInstance);
            _ghostMatInstance = null;
        }

        _playerMovement?.SetHangoverDebuff(1f);

        MissionManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
    }
}
