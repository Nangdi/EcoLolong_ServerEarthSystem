using UnityEngine;
using UnityEngine.Serialization;

// EarthStateSnapshot.ArcticIcePercent 값에 따라 SpriteRenderer의 sprite를 3단계로 교체합니다.
// 50% 이상 → fullSprite, 20% 이상 → halfSprite, 그 외 → brokenSprite.
[RequireComponent(typeof(SpriteRenderer))]
public class ArcticIceSpriteController : MonoBehaviour
{
    private const float HalfThresholdPercent = 50f;
    private const float BrokenThresholdPercent = 20f;

    [Header("상태 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;

    [Header("Sprite Renderer")]
    [FormerlySerializedAs("iceRenderer")]
    [SerializeField] private SpriteRenderer _iceRenderer;

    [Header("단계별 Sprite")]
    [Tooltip("얼음 잔존율이 50% 이상일 때 표시")]
    [FormerlySerializedAs("fullSprite")]
    [SerializeField] private Sprite _fullSprite;
    [Tooltip("얼음 잔존율이 20% 이상 ~ 50% 미만일 때 표시")]
    [FormerlySerializedAs("halfSprite")]
    [SerializeField] private Sprite _halfSprite;
    [Tooltip("얼음 잔존율이 20% 미만일 때 표시")]
    [FormerlySerializedAs("brokenSprite")]
    [SerializeField] private Sprite _brokenSprite;

    // 같은 sprite를 반복 할당하지 않도록 마지막으로 적용한 단계를 캐싱합니다.
    private IceStage _lastAppliedStage = IceStage.None;
    private bool _isSubscribed;

    private enum IceStage
    {
        None,
        Full,
        Half,
        Broken
    }

    private void Awake()
    {
        if (_iceRenderer == null)
            _iceRenderer = GetComponent<SpriteRenderer>();
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
        // EarthStateManager가 늦게 생성돼도 한 번은 구독되도록 매 프레임 시도합니다.
        if (!_isSubscribed)
            TrySubscribe();
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
        // 구독 직후 현재 값으로 초기 sprite를 맞춰줍니다.
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
        if (_iceRenderer == null || snapshot == null)
            return;

        IceStage stage = ResolveStage(snapshot.ArcticIcePercent);
        if (stage == _lastAppliedStage)
            return;

        Sprite target = SpriteForStage(stage);
        if (target == null)
            return;

        _iceRenderer.sprite = target;
        _lastAppliedStage = stage;
    }

    private static IceStage ResolveStage(float arcticIcePercent)
    {
        if (arcticIcePercent >= HalfThresholdPercent)
            return IceStage.Full;
        if (arcticIcePercent >= BrokenThresholdPercent)
            return IceStage.Half;
        return IceStage.Broken;
    }

    private Sprite SpriteForStage(IceStage stage)
    {
        switch (stage)
        {
            case IceStage.Full: return _fullSprite;
            case IceStage.Half: return _halfSprite;
            case IceStage.Broken: return _brokenSprite;
            default: return null;
        }
    }
}
