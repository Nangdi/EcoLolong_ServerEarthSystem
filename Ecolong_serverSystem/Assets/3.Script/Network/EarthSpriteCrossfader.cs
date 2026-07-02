using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// EarthStateManager의 친환경도(A)/발전도(B) 단계에 맞춰 "A-B" 스프라이트로 지구 이미지를 교체합니다.
// 단계가 바뀔 때마다 기존 이미지는 페이드아웃, 새 이미지는 페이드인하는 크로스페이드로 전환합니다.
//
// [기록/리플레이] ResourceGraphs가 (시간, 값)을 기록해 두었다가 리플레이 때 다시 그리는 것과 동일하게,
// 플레이 중 25단계(친환경도 5 x 발전도 5) 변화를 (시간, eco, dev)로 기록해 두고
// R 키 리플레이 시 GameTimer.CurrentTime에 맞춰 같은 순서/타이밍으로 스프라이트를 전환합니다.
// 리플레이는 settingGameScale(기본 15배속)로 빠르게 흐르므로 크로스페이드 시간도 배속으로 나눠 비례시킵니다.
//
// 스프라이트는 Resources 밖(Assets/4.Sprite/Earth)에 있으므로 25칸 배열로 직렬화해 두고,
// 컨텍스트 메뉴 "Auto-fill Sprites from 4.Sprite/Earth"로 에디터에서 한 번에 채울 수 있습니다.
[RequireComponent(typeof(Image))]
public class EarthSpriteCrossfader : MonoBehaviour
{
    private const int LevelMin = 1;
    private const int LevelMax = 5;
    private const int LevelCount = LevelMax - LevelMin + 1; // 5

    // 25단계 변화를 (게임시간, 친환경도, 발전도)로 기록하는 한 줄입니다.
    [Serializable]
    public struct StateSample
    {
        public float Time;
        public int Eco;
        public int Dev;
    }

    [Header("상태 연결")]
    [Tooltip("비어 있으면 EarthStateManager.Instance를 자동으로 사용합니다.")]
    [SerializeField] private EarthStateManager _earthStateManager;
    [Tooltip("지구를 표시하는 메인 Image. 비어 있으면 같은 오브젝트의 Image를 사용합니다.")]
    [SerializeField] private Image _earthImage;
    [Tooltip("리플레이 진행 시간의 기준이 되는 타이머. 비어 있으면 GameTimer.Instance를 자동으로 사용합니다.")]
    [SerializeField] private GameTimer _timer;

    [Header("스프라이트 (친환경도 A x 발전도 B = 25칸)")]
    [Tooltip("인덱스 = (친환경도-1)*5 + (발전도-1). 컨텍스트 메뉴 'Auto-fill...'로 자동 채울 수 있습니다.")]
    [SerializeField] private Sprite[] _sprites = new Sprite[LevelCount * LevelCount];

    [Header("전환 연출")]
    [Tooltip("크로스페이드(페이드아웃/페이드인) 지속 시간(초). 0이면 즉시 교체.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = 0.6f;
    [Tooltip("켜면 리플레이/빠른 플레이 배속(settingGameScale)에 비례해 크로스페이드 시간을 줄입니다(예: 15배속 → 0.6/15초).")]
    [SerializeField] private bool _scaleFadeWithGameSpeed = true;

    [Header("디버그")]
    [SerializeField] private List<StateSample> _samples = new List<StateSample>();
    private List<StateSample> _recordSamples = new List<StateSample>();

    private Image _overlayImage; // 크로스페이드용 오버레이(런타임 자동 생성, 메인 이미지 위에 겹침)
    private bool _isSubscribedToEarthState;
    private bool _isSubscribedToGame;
    private int _currentDev = -1;
    private int _currentEco = -1;
    private int _lastReplayedIndex = -1;
    private Coroutine _fadeRoutine;
    private int debugIndex = 0;

    private void Awake()
    {
        if (_earthImage == null)
            _earthImage = GetComponent<Image>();
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
        if (_timer == null)
            _timer = GameTimer.Instance;

        EnsureOverlay();
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
        // 매니저가 늦게 초기화되는 경우를 대비해 구독될 때까지 재시도합니다.
        TrySubscribe();

        if(Input.GetKeyDown(KeyCode.RightBracket))
        {
            debugIndex = (debugIndex + 1) % _sprites.Length;
            debugIndex = Mathf.Clamp(debugIndex, 0, _sprites.Length - 1);
            CommitInstant(_sprites[debugIndex]);
        }
          if(Input.GetKeyDown(KeyCode.LeftBracket))
        {
            debugIndex = (debugIndex - 1) % _sprites.Length;
            debugIndex = Mathf.Clamp(debugIndex, 0, _sprites.Length - 1);
            CommitInstant(_sprites[debugIndex]);
        }

        // 리플레이 중에는 EarthStateManager가 멈춰 StateChanged가 오지 않으므로,
        // 기록해 둔 샘플을 GameTimer 진행 시간에 맞춰 직접 적용합니다.
        if (IsReplayPlaying())
            ApplyStateForReplayTime(_timer.CurrentTime);
    }

    // 메인 이미지의 자식으로 전체를 덮는 오버레이 Image를 만들어 위에 겹칩니다.
    // 자식 Image는 부모 위에 렌더링되고, 알파는 부모와 독립적이므로 크로스페이드에 적합합니다.
    private void EnsureOverlay()
    {
        if (_overlayImage != null || _earthImage == null)
            return;

        GameObject go = new GameObject("EarthCrossfadeOverlay", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(_earthImage.rectTransform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        _overlayImage = go.GetComponent<Image>();
        _overlayImage.raycastTarget = false;
        _overlayImage.preserveAspect = _earthImage.preserveAspect;
        _overlayImage.type = _earthImage.type;
        SetAlpha(_overlayImage, 0f);
        _overlayImage.enabled = false;
    }

    private void TrySubscribe()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
        if (_timer == null)
            _timer = GameTimer.Instance;

        if (!_isSubscribedToEarthState && _earthStateManager != null)
        {
            _earthStateManager.StateChanged += OnStateChanged;
            _isSubscribedToEarthState = true;
            // 구독 직후 현재 상태를 즉시(페이드 없이) 반영해 초기 이미지를 맞춥니다.
            ApplySnapshot(_earthStateManager.CurrentState, instant: true);
        }

        if (!_isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart += OnGameStart;
            GameManager.Instance.OnGameEnd += OnGameEnd;
            GameManager.Instance.OnReplay += OnReplay;
            _isSubscribedToGame = true;
        }
    }

    private void Unsubscribe()
    {
        if (_isSubscribedToEarthState && _earthStateManager != null)
            _earthStateManager.StateChanged -= OnStateChanged;
        if (_isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart -= OnGameStart;
            GameManager.Instance.OnGameEnd -= OnGameEnd;
            GameManager.Instance.OnReplay -= OnReplay;
        }
        _isSubscribedToEarthState = false;
        _isSubscribedToGame = false;
    }

    // 새 게임 시작마다 이전 기록을 비우고 표시 상태를 초기화합니다.
    private void OnGameStart()
    {
        _samples.Clear();
        _recordSamples.Clear();
        _lastReplayedIndex = -1;
        _currentDev = -1;
        _currentEco = -1;
    }

    // 게임 종료 시점에 이번 회차 기록을 리플레이용으로 백업합니다.
    private void OnGameEnd()
    {
        _recordSamples = new List<StateSample>(_samples);
    }

    // R 키 리플레이 시작 신호. 다음 Update부터 기록 시간 기반으로 스프라이트를 다시 전환합니다.
    private void OnReplay()
    {
        _lastReplayedIndex = -1;
        // 리플레이는 처음 상태부터 다시 보여줘야 하므로 표시 단계를 리셋해
        // 첫 샘플이 강제로 적용되도록 합니다.
        _currentDev = -1;
        _currentEco = -1;
    }

    private void OnStateChanged(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // 플레이 중에는 (시간, eco, dev) 변화를 기록합니다.
        RecordSample(snapshot);

        // 리플레이가 "재생 중"일 때만 기록 재생(ApplyStateForReplayTime)이 스프라이트를 담당하므로
        // 여기서는 실시간 반영을 건너뜁니다.
        // TimeOut(리플레이 대기)처럼 타이머가 멈춘 상태에서는 ResetState 같은 StateChanged를 그대로 반영해
        // 지구 이미지가 초기 상태로 되돌아가 대기하도록 합니다.
        if (IsReplayPlaying())
            return;

        ApplySnapshot(snapshot, instant: false);
    }

    // 친환경도/발전도 단계가 직전 샘플과 달라졌을 때만 (시간, eco, dev) 한 줄을 추가합니다.
    private void RecordSample(EarthStateSnapshot snapshot)
    {
        if (_timer == null || GameManager.Instance == null)
            return;
        if (GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        int eco = Mathf.Clamp(snapshot.EcoLevel, LevelMin, LevelMax);
        int dev = Mathf.Clamp(snapshot.DevelopmentLevel, LevelMin, LevelMax);

        if (_samples.Count > 0)
        {
            StateSample last = _samples[_samples.Count - 1];
            if (last.Eco == eco && last.Dev == dev)
                return;
        }

        _samples.Add(new StateSample { Time = _timer.CurrentTime, Eco = eco, Dev = dev });
    }

    // 리플레이가 실제로 재생 중인지(IsReplay이면서 타이머가 돌고 있는지) 판정합니다.
    // TimeOut 대기 중에는 타이머가 멈춰 있어 false가 되므로, 이때의 StateChanged(초기화 등)는 그대로 반영됩니다.
    private bool IsReplayPlaying()
    {
        return _timer != null && GameManager.Instance != null && GameManager.Instance.IsReplay && _timer.IsRunning;
    }

    // currentTime 이전의 마지막 샘플을 골라 스프라이트에 적용합니다.
    private void ApplyStateForReplayTime(float currentTime)
    {
        if (_recordSamples.Count == 0)
            return;

        int idx = -1;
        for (int i = 0; i < _recordSamples.Count; i++)
        {
            if (_recordSamples[i].Time <= currentTime)
                idx = i;
            else
                break;
        }

        if (idx < 0 || idx == _lastReplayedIndex)
            return;

        // 이번 리플레이에서 처음 적용하는 칸이면 시작 상태로 즉시 스냅하고,
        // 이후 변화는 배속에 맞춘 크로스페이드로 전환합니다.
        bool instant = _lastReplayedIndex == -1;
        StateSample sample = _recordSamples[idx];
        ApplyLevels(sample.Eco, sample.Dev, instant);
        _lastReplayedIndex = idx;
    }

    private void ApplySnapshot(EarthStateSnapshot snapshot, bool instant)
    {
        if (snapshot == null)
            return;

        int dev = Mathf.Clamp(snapshot.DevelopmentLevel, LevelMin, LevelMax);
        int eco = Mathf.Clamp(snapshot.EcoLevel, LevelMin, LevelMax);
        ApplyLevels(eco, dev, instant);
    }

    private void ApplyLevels(int eco, int dev, bool instant)
    {
        if (_earthImage == null)
            return;

        eco = Mathf.Clamp(eco, LevelMin, LevelMax);
        dev = Mathf.Clamp(dev, LevelMin, LevelMax);

        // 단계가 바뀌지 않았으면 전환하지 않습니다.
        if (dev == _currentDev && eco == _currentEco)
            return;

        // 파일명 규칙 "A-B" = "친환경도-발전도" 이므로 (eco, dev) 순서로 조회합니다.
        Sprite next = GetSprite(eco, dev);
        if (next == null)
        {
            Debug.LogWarning($"[EarthSpriteCrossfader] '{eco}-{dev}' 스프라이트가 비어 있습니다. 인스펙터에서 Auto-fill 하세요.");
            return;
        }

        _currentDev = dev;
        _currentEco = eco;

        float fadeDuration = GetActiveFadeDuration();
        bool skipFade = instant || fadeDuration <= 0f || !isActiveAndEnabled || _earthImage.sprite == null;
        if (skipFade)
        {
            CommitInstant(next);
            return;
        }

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossfadeTo(next, fadeDuration));
    }

    // 현재 게임/리플레이 배속(settingGameScale)에 비례해 크로스페이드 시간을 줄입니다.
    // 예) 리플레이 15배속이면 0.6초 → 0.04초로 단축돼 빨라진 진행 속도와 어울립니다.
    private float GetActiveFadeDuration()
    {
        if (!_scaleFadeWithGameSpeed)
            return _fadeDuration;

        float scale = _timer != null ? _timer.settingGameScale : 1f;
        if (scale <= 1f)
            return _fadeDuration;

        return _fadeDuration / scale;
    }

    private void CommitInstant(Sprite next)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _earthImage.sprite = next;
        SetAlpha(_earthImage, 1f);
        if (_overlayImage != null)
        {
            _overlayImage.enabled = false;
            SetAlpha(_overlayImage, 0f);
        }
    }

    private IEnumerator CrossfadeTo(Sprite next, float fadeDuration)
    {
        EnsureOverlay();
        if (_overlayImage == null || fadeDuration <= 0f)
        {
            CommitInstant(next);
            yield break;
        }

        // 새 스프라이트를 오버레이(위)에 올려 페이드인, 기존 메인 이미지는 페이드아웃합니다.
        _overlayImage.sprite = next;
        _overlayImage.enabled = true;
        SetAlpha(_overlayImage, 0f);
        SetAlpha(_earthImage, 1f);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            SetAlpha(_overlayImage, k);    // 새 이미지 페이드인
            SetAlpha(_earthImage, 1f - k); // 기존 이미지 페이드아웃
            yield return null;
        }

        // 전환 완료: 새 스프라이트를 메인으로 확정하고 오버레이를 숨깁니다.
        _earthImage.sprite = next;
        SetAlpha(_earthImage, 1f);
        _overlayImage.enabled = false;
        SetAlpha(_overlayImage, 0f);
        _fadeRoutine = null;
    }

    // 파일명 "A-B" 순서(a=친환경도, b=발전도)에 대응하는 스프라이트를 반환합니다. 인덱스 = (a-1)*5 + (b-1).
    private Sprite GetSprite(int a, int b)
    {
        int idx = (a - LevelMin) * LevelCount + (b - LevelMin);
        if (_sprites != null && idx >= 0 && idx < _sprites.Length)
            return _sprites[idx];
        return null;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null)
            return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-fill Sprites from 4.Sprite/Earth")]
    private void AutoFillSprites()
    {
        _sprites = new Sprite[LevelCount * LevelCount];
        for (int dev = LevelMin; dev <= LevelMax; dev++)
        {
            for (int eco = LevelMin; eco <= LevelMax; eco++)
            {
                string path = $"Assets/4.Sprite/Earth/{dev}-{eco}.png";
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null)
                    Debug.LogWarning($"[EarthSpriteCrossfader] 로드 실패: {path}");
                _sprites[(dev - LevelMin) * LevelCount + (eco - LevelMin)] = sp;
            }
        }
        EditorUtility.SetDirty(this);
        Debug.Log("[EarthSpriteCrossfader] 25개 스프라이트 자동 채움 완료.");
    }
#endif
}
