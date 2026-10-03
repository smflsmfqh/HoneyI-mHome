using UnityEngine;

public class CarMovement : MonoBehaviour
{
    [Header("오디오")]
    [SerializeField]
    private AudioSource _audioSource;

    [SerializeField]
    private AudioClip _hornSound;

    private ProximityFeedback _proximity;
    private bool _hornPlaying = false;
    private float _hornMinPlayTime = 1.5f;
    private float _hornPlayedTime = 0f;

    [Header("경로점 (Inspector 직접 지정 시)")]
    [SerializeField]
    private CarDrivingZone _path;

    [SerializeField]
    private int _laneIndex = 0;

    [SerializeField]
    private float _laneOffset = 0f;
    private int _waypointIndex = 0;

    [Header("이동 설정")]
    [SerializeField]
    private float _carSpeed = 5f;

    [SerializeField]
    private float _rotationSpeed = 8f;

    [SerializeField]
    private float _waypointReachDistance = 0.5f;

    private bool _signalStopped = false;
    private Vector3 _currentDestination;
    private bool _initialized = false;
    private bool _tutorialPaused = false;

    public void SetTutorialPause(bool paused) => _tutorialPaused = paused;

    public void SetSignalStop(bool stopped) => _signalStopped = stopped;

    private void Start()
    {
        _audioSource = GetComponentInChildren<AudioSource>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _proximity = player.GetComponent<ProximityFeedback>();
        if (_proximity != null)
            _proximity.RegisterDanger(transform);

        if (_audioSource != null)
        {
            if (_hornSound != null)
                _audioSource.clip = _hornSound;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0f;
            _audioSource.Stop();
        }

        if (_path != null && !_initialized)
            InitializeMovement();
    }

    private void OnDestroy()
    {
        if (_proximity != null)
            _proximity.UnregisterDanger(transform);
    }

    public void SetHornClip(AudioClip clip)
    {
        _hornSound = clip;
        var audio = GetComponentInChildren<AudioSource>();
        if (audio != null)
        {
            audio.clip = clip;
            audio.loop = true;
            audio.spatialBlend = 0f;
            audio.volume = 0f;
            _audioSource = audio;
        }
    }

    public void Initialize(
        CarDrivingZone path,
        int laneIndex,
        int startWaypointIndex,
        float laneOffset = 0f
    )
    {
        _path = path;
        _laneIndex = laneIndex;
        _laneOffset = laneOffset;
        _waypointIndex = startWaypointIndex;
        InitializeMovement();
    }

    private void InitializeMovement()
    {
        _currentDestination = GetWaypointPosition();
        AlignToNextWaypoint();
        _initialized = true;
    }

    private void AlignToNextWaypoint()
    {
        Vector3 dir = _currentDestination - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void Update()
    {
        if (!_initialized || _tutorialPaused)
            return;

        UpdateEngineVolume();

        if (!_signalStopped)
            DriveCar();
    }

    private void UpdateEngineVolume()
    {
        if (_audioSource == null)
            return;

        if (_proximity == null)
            return;

        float dist = Vector3.Distance(transform.position, _proximity.transform.position);
        bool inRange = dist <= _proximity.DangerRadius;

        if (inRange && !_hornPlaying)
        {
            _audioSource.Play();
            _hornPlaying = true;
            _hornPlayedTime = 0f;
        }
        else if (!inRange && _hornPlaying)
        {
            _hornPlayedTime += Time.deltaTime;
            if (_hornPlayedTime >= _hornMinPlayTime)
            {
                _audioSource.Stop();
                _hornPlaying = false;
            }
        }

        if (_hornPlaying)
        {
            float t = Mathf.Clamp01(
                (dist - _proximity.PanicRadius) / (_proximity.DangerRadius - _proximity.PanicRadius)
            );
            _audioSource.volume = 1f - t;
        }
    }

    private void DriveCar()
    {
        if (_path == null || _path.Count == 0)
            return;

        Vector3 flatDest = new(_currentDestination.x, transform.position.y, _currentDestination.z);

        Vector3 dir = flatDest - transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * _rotationSpeed
            );
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _currentDestination,
            _carSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, _currentDestination) <= _waypointReachDistance)
        {
            _waypointIndex = (_waypointIndex + 1) % _path.GetWaypointCount(_laneIndex);
            _currentDestination = GetWaypointPosition();
        }
    }

    private Vector3 GetWaypointPosition() =>
        _path.GetWaypointPosition(_laneIndex, _waypointIndex, _laneOffset);
}
