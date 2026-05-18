using UnityEngine;
using UnityEngine.Serialization;

// EarthStateSnapshot.SeaLevelRiseMeters를 0~maxSeaLevelMeters 범위로 보고,
// 같은 GameObject의 RectTransform Height를 0(시작) ~ 원본 height(50cm 도달 시)로 보간합니다.
// Start 시점의 RectTransform Height가 "해수면이 maxSeaLevelMeters에 도달했을 때의 최종 높이"로 캡쳐됩니다.
[RequireComponent(typeof(RectTransform))]
public class SeaLevelHeightController : MonoBehaviour
{
    [Header("상태 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;

    [Header("해수면 범위")]
    [Tooltip("이 값(미터)에 도달했을 때 RectTransform Height가 최대치가 됩니다. 50cm = 0.5m")]
    [Min(0.0001f)]
    [FormerlySerializedAs("maxSeaLevelMeters")]
    [SerializeField] private float _maxSeaLevelMeters = 0.5f;

    private RectTransform _rectTransform;
    // Start 시점에 캡쳐한 원본 height. 해수면 100%(=maxSeaLevelMeters) 도달 시의 height가 됩니다.
    private float _maxHeight;
    private bool _isSubscribed;
    private bool _isInitialized;

    // 도메인 리로드/리플렉션 호출에서도 안전하도록 lazy하게 RectTransform을 얻습니다.
    private RectTransform Rect
    {
        get
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            return _rectTransform;
        }
    }

    private void Start()
    {
        CaptureMaxHeight();
        ResetToZero();
        TrySubscribe();
    }

    private void OnEnable()
    {
        if (_isInitialized)
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
    }

    private void CaptureMaxHeight()
    {
        if (Rect == null)
            return;

        _maxHeight = Rect.rect.height;
        _isInitialized = true;
    }

    private void ResetToZero()
    {
        ApplyHeight(0f);
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;

        if (_earthStateManager == null)
            return;

        _earthStateManager.StateChanged += OnEarthStateChanged;
        _isSubscribed = true;
        ApplySnapshot(_earthStateManager.CurrentState);
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _earthStateManager == null)
            return;

        _earthStateManager.StateChanged -= OnEarthStateChanged;
        _isSubscribed = false;
    }

    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(EarthStateSnapshot snapshot)
    {
        if (!_isInitialized || snapshot == null)
            return;

        float ratio = Mathf.Clamp01(snapshot.SeaLevelRiseMeters / _maxSeaLevelMeters);
        ApplyHeight(Mathf.Lerp(0f, _maxHeight, ratio));
    }

    // RectTransform의 anchor 상태와 무관하게 Height만 안전하게 변경하기 위해
    // SetSizeWithCurrentAnchors를 사용합니다. (sizeDelta 직접 수정은 anchor 폭이 0이 아닐 때 의도와 어긋날 수 있음)
    private void ApplyHeight(float height)
    {
        if (Rect == null)
            return;

        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
