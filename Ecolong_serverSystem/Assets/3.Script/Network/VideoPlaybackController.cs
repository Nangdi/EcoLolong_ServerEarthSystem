using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPlaybackController : MonoBehaviour
{
    [Header("UI 배치 (Inspector에서 수동 할당)")]
    [SerializeField] private RawImage _rawImage;

    [Header("RenderTexture 설정")]
    [SerializeField] private int _renderTextureWidth = 1920;
    [SerializeField] private int _renderTextureHeight = 1080;
    [SerializeField] private int _renderTextureDepth = 0;

    private VideoPlayer _videoPlayer;
    private AudioSource _audioSource;
    private RenderTexture _renderTexture;
    private Coroutine _prepareCoroutine;
    private bool _isReadyToPlay;

    private void Awake()
    {
        EnsureRenderTexture();
        EnsureVideoPlayer();
        BindRawImage();
    }

    private void OnEnable()
    {
        SubscribeAggregator();
        SubscribeGameManager();
    }

    private void Start()
    {
        // TcpDataAggregator / GameManager가 Awake 이후 활성화되는 경우를 대비해 Start에서도 한 번 더 보장합니다.
        SubscribeAggregator();
        SubscribeGameManager();
    }

    private void OnDisable()
    {
        if (TcpDataAggregator.Instance != null)
            TcpDataAggregator.Instance.VideoReadyReceived -= HandleVideoReady;

        if (GameManager.Instance != null)
            GameManager.Instance.OnReplay -= HandleReplay;
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null)
            _videoPlayer.loopPointReached -= HandleLoopPointReached;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }

    private void SubscribeAggregator()
    {
        TcpDataAggregator aggregator = TcpDataAggregator.Instance;
        if (aggregator == null)
            return;

        aggregator.VideoReadyReceived -= HandleVideoReady;
        aggregator.VideoReadyReceived += HandleVideoReady;
    }

    private void SubscribeGameManager()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        gameManager.OnReplay -= HandleReplay;
        gameManager.OnReplay += HandleReplay;
    }

    private void EnsureRenderTexture()
    {
        if (_renderTexture != null)
            return;

        _renderTexture = new RenderTexture(_renderTextureWidth, _renderTextureHeight, _renderTextureDepth, RenderTextureFormat.ARGB32);
        _renderTexture.name = "VideoRenderTexture";
        _renderTexture.Create();
    }

    private void EnsureVideoPlayer()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        _videoPlayer = GetComponent<VideoPlayer>();
        if (_videoPlayer == null)
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();

        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _videoPlayer.SetTargetAudioSource(0, _audioSource);
        _videoPlayer.loopPointReached -= HandleLoopPointReached;
        _videoPlayer.loopPointReached += HandleLoopPointReached;
    }

    // Inspector에서 수동 할당한 RawImage에 RenderTexture를 연결하고 초기 상태를 비활성으로 둡니다.
    private void BindRawImage()
    {
        if (_rawImage == null)
        {
            Debug.LogWarning("[VideoPlayback] RawImage가 할당되지 않았습니다. Inspector에서 _rawImage 필드를 연결하세요.");
            return;
        }

        _rawImage.texture = _renderTexture;
        _rawImage.gameObject.SetActive(false);
    }

    private void SetRawImageActive(bool active)
    {
        if (_rawImage != null)
            _rawImage.gameObject.SetActive(active);
    }

    private void HandleVideoReady(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogWarning("[VideoPlayback] 수신된 파일명이 비어 있습니다.");
            return;
        }

        VideoSettingJson setting = JsonManager.instance != null
            ? JsonManager.instance.videoSettingJson
            : new VideoSettingJson();

        if (string.IsNullOrWhiteSpace(setting.folderPath))
        {
            Debug.LogError("[VideoPlayback] VideoSetting.json의 folderPath가 비어 있습니다.");
            return;
        }

        string fullPath = Path.Combine(setting.folderPath, fileName);

        _isReadyToPlay = false;
        SetRawImageActive(false);

        if (_prepareCoroutine != null)
            StopCoroutine(_prepareCoroutine);

        _prepareCoroutine = StartCoroutine(PrepareVideo(fullPath, setting));
    }

    // 파일 안정성을 확인하고 VideoPlayer.Prepare까지 완료한 뒤 OnReplay(R 키) 입력을 기다립니다.
    private IEnumerator PrepareVideo(string fullPath, VideoSettingJson setting)
    {
        Debug.Log($"[VideoPlayback] 파일 안정성 대기 시작 / {fullPath}");

        bool ready = false;
        yield return WaitUntilFileReady(fullPath, setting, success => ready = success);

        if (!ready)
        {
            Debug.LogError($"[VideoPlayback] 파일 준비 실패 또는 시간 초과 / {fullPath}");
            _prepareCoroutine = null;
            yield break;
        }

        if (_videoPlayer.isPlaying)
            _videoPlayer.Stop();

        _videoPlayer.url = fullPath;
        _videoPlayer.isLooping = setting.loop;
        _audioSource.mute = !setting.playAudio;
        _audioSource.volume = Mathf.Clamp01(setting.volume);

        _videoPlayer.Prepare();
        while (!_videoPlayer.isPrepared)
            yield return null;

        // 첫 프레임을 RenderTexture에 그리기 위해 음소거로 한 프레임만 진행한 뒤 일시정지합니다.
        yield return RenderFirstFrame();
        SetRawImageActive(true);

        _isReadyToPlay = true;
        Debug.Log($"[VideoPlayback] 재생 준비 완료 / R 키 입력 대기 / {fullPath}");
        TcpDataAggregator.Instance.AddRecentMessage($"[VideoPlayback] 재생 준비 완료 / R 키 입력 대기 / {fullPath}");

        _prepareCoroutine = null;
    }

    // 음소거 상태로 한 프레임만 재생해 첫 프레임을 RenderTexture에 기록한 뒤 일시정지합니다.
    private IEnumerator RenderFirstFrame()
    {
        bool originalMute = _audioSource.mute;
        _audioSource.mute = true;

        long initialFrame = _videoPlayer.frame;
        _videoPlayer.Play();

        // 한 프레임이라도 진행될 때까지 대기 (안전 차원으로 maxFrames 횟수 제한)
        int safety = 0;
        while (_videoPlayer.frame <= initialFrame && safety < 120)
        {
            safety++;
            yield return null;
        }

        _videoPlayer.Pause();
        _videoPlayer.time = 0;
        _audioSource.mute = originalMute;
    }

    // GameManager.OnReplay 이벤트(R 키) 발생 시 호출됩니다.
    private void HandleReplay()
    {
        if (!_isReadyToPlay || _videoPlayer == null)
        {
            Debug.Log("[VideoPlayback] R 키 입력 / 재생할 비디오가 준비되지 않았습니다.");
            return;
        }

        SetRawImageActive(true);

        _videoPlayer.time = 0;
        _videoPlayer.Play();
        Debug.Log("[VideoPlayback] R 키 입력 / 재생 시작");
    }

    // 파일이 더 이상 쓰여지지 않고 안전하게 열 수 있을 때까지 대기합니다.
    private IEnumerator WaitUntilFileReady(string fullPath, VideoSettingJson setting, Action<bool> onComplete)
    {
        float interval = Mathf.Max(0.05f, setting.stabilityCheckIntervalSeconds);
        int requiredStableCount = Mathf.Max(1, setting.stabilityCheckCount);
        float maxWait = Mathf.Max(1f, setting.maxWaitSeconds);
        float startTime = Time.realtimeSinceStartup;

        long previousLength = -1;
        int stableCount = 0;

        while (true)
        {
            if (Time.realtimeSinceStartup - startTime > maxWait)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            if (!File.Exists(fullPath))
            {
                stableCount = 0;
                previousLength = -1;
                yield return new WaitForSeconds(interval);
                continue;
            }

            long currentLength = -1;
            bool sizeReadOk = false;
            try
            {
                currentLength = new FileInfo(fullPath).Length;
                sizeReadOk = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VideoPlayback] 파일 정보 읽기 실패 / {exception.Message}");
            }

            if (!sizeReadOk || currentLength <= 0 || currentLength != previousLength)
            {
                stableCount = 0;
                previousLength = currentLength;
                yield return new WaitForSeconds(interval);
                continue;
            }

            if (!TryOpenForRead(fullPath))
            {
                stableCount = 0;
                yield return new WaitForSeconds(interval);
                continue;
            }

            stableCount++;
            if (stableCount >= requiredStableCount)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            yield return new WaitForSeconds(interval);
        }
    }

    // FileShare.Read 로 열어 다른 프로세스가 쓰기 중이면 실패하도록 합니다.
    private static bool TryOpenForRead(string fullPath)
    {
        try
        {
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return stream.Length > 0;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void HandleLoopPointReached(VideoPlayer source)
    {
        if (source.isLooping)
            return;

        Debug.Log("[VideoPlayback] 재생 종료");
        OnVideoPlaybackFinished();
    }

    // 재생이 끝난 직후 호출됩니다. RawImage는 마지막 프레임이 유지된 상태이며,
    // 여기에 게임 결과 화면 전환, UI 활성화 등 후속 동작을 작성하면 됩니다.
    private void OnVideoPlaybackFinished()
    {
        // TODO: 재생 종료 후 동작을 여기에 작성하세요.
    }

    public void StopPlayback()
    {
        if (_prepareCoroutine != null)
        {
            StopCoroutine(_prepareCoroutine);
            _prepareCoroutine = null;
        }

        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Stop();

        SetRawImageActive(false);
        _isReadyToPlay = false;
    }
}
