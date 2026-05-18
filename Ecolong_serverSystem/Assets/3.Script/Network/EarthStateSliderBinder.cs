using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// EarthStateSnapshot의 레벨 값을 3개 슬라이더에 정규화 비율(0~1)로 동기화합니다.
// - Sustainability = EcoLevel + DevelopmentLevel (2~10)
// - Development    = DevelopmentLevel             (1~5)
// - Eco            = EcoLevel                     (1~5)
public class EarthStateSliderBinder : MonoBehaviour
{
    [Header("상태 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;

    [Header("슬라이더")]
    [Tooltip("지속가능성 레벨용 슬라이더 (EcoLevel + DevelopmentLevel, 최대 10)")]
    [FormerlySerializedAs("sustainabilitySlider")]
    [SerializeField] private Slider _sustainabilitySlider;
    [Tooltip("발전도 레벨용 슬라이더 (DevelopmentLevel, 최대 5)")]
    [FormerlySerializedAs("developmentSlider")]
    [SerializeField] private Slider _developmentSlider;
    [Tooltip("친환경도 레벨용 슬라이더 (EcoLevel, 최대 5)")]
    [FormerlySerializedAs("ecoSlider")]
    [SerializeField] private Slider _ecoSlider;

    [Header("정규화 분모 (각 레벨의 최대치)")]
    [Min(1)]
    [SerializeField] private int _maxSustainability = 10;
    [Min(1)]
    [SerializeField] private int _maxDevelopment = 5;
    [Min(1)]
    [SerializeField] private int _maxEco = 5;

    [Header("애니메이션")]
    [Tooltip("목표 값에 도달하는 부드러움 시간(초). 작을수록 빨리/뚝 도착, 클수록 더 천천히 자연스럽게. 0이면 즉시 반영.")]
    [Min(0f)]
    [SerializeField] private float _smoothTime = 0.4f;

    private bool _isSubscribed;
    // 슬라이더별 목표값과 SmoothDamp 내부 속도. 매 프레임 현재값을 목표값으로 부드럽게 이동시킵니다.
    private float _sustainabilityTarget;
    private float _developmentTarget;
    private float _ecoTarget;
    private float _sustainabilityVelocity;
    private float _developmentVelocity;
    private float _ecoVelocity;

    private void Awake()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_isSubscribed)
            TrySubscribe();

        // 매 프레임 현재 슬라이더 값을 목표값으로 자연스럽게 보간합니다.
        StepSlider(_sustainabilitySlider, _sustainabilityTarget, ref _sustainabilityVelocity);
        StepSlider(_developmentSlider,    _developmentTarget,    ref _developmentVelocity);
        StepSlider(_ecoSlider,            _ecoTarget,            ref _ecoVelocity);
    }

    private void StepSlider(Slider slider, float target, ref float velocity)
    {
        if (slider == null)
            return;

        if (_smoothTime <= 0f)
        {
            slider.value = target;
            velocity = 0f;
            return;
        }

        slider.value = Mathf.SmoothDamp(slider.value, target, ref velocity, _smoothTime);
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;

        if (_earthStateManager == null)
            return;

        _earthStateManager.StateChanged += OnStateChanged;
        _isSubscribed = true;
        ApplySnapshot(_earthStateManager.CurrentState);
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _earthStateManager == null)
            return;

        _earthStateManager.StateChanged -= OnStateChanged;
        _isSubscribed = false;
    }

    private void OnStateChanged(EarthStateSnapshot snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;
        _sustainabilityTarget = ComputeSliderTarget(_sustainabilitySlider, sustainability, _maxSustainability);
        _developmentTarget    = ComputeSliderTarget(_developmentSlider, snapshot.DevelopmentLevel, _maxDevelopment);
        _ecoTarget            = ComputeSliderTarget(_ecoSlider, snapshot.EcoLevel, _maxEco);
    }

    // value/max 비율을 슬라이더의 min/max 범위에 매핑합니다 (min/max가 0~1이 아닌 경우에도 안전).
    private static float ComputeSliderTarget(Slider slider, int value, int max)
    {
        if (slider == null)
            return 0f;

        float ratio = Mathf.Clamp01((float)value / Mathf.Max(1, max));
        return Mathf.Lerp(slider.minValue, slider.maxValue, ratio);
    }
}
