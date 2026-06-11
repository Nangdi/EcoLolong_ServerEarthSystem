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

    [Header("타이머 동기화 설정")]
    [Tooltip("리플레이 중 동영상이 타이머 위치에서 이만큼(초) 이상 벌어지면 해당 위치로 보정(시킹)합니다.")]
    [SerializeField] private float _syncDriftThresholdSeconds = 0.2f;

    [Header("오래된 비디오 정리 설정")]
    [Tooltip("게임 시작 시 이 일수보다 오래된 비디오 파일을 folderPath에서 삭제합니다.")]
    [SerializeField] private int _videoExpireDays = 7;

    // 확장자가 이 목록에 포함된 파일만 정리 대상으로 삼습니다.
    private static readonly string[] _videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".wmv" };

    // 게임 실행 중 정리가 단 한 번만 수행되도록 보장하는 플래그입니다.
    private static bool _oldVideosCleaned;

    private VideoPlayer _videoPlayer;
    private AudioSource _audioSource;
    private RenderTexture _renderTexture;
    private Coroutine _prepareCoroutine;
    private bool _isReadyToPlay;

    // 리플레이 재생 중 매 프레임 GameTimer에 동영상 진행도를 밀어 넣을지 여부입니다.
    private bool _isSyncingTimer;

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

        // 게임 시작 시 한 번만 오래된 비디오를 정리합니다.
        CleanupOldVideos();
    }

    // folderPath 내에서 _videoExpireDays(기본 7일)보다 오래된 비디오 파일을 삭제합니다. 게임 실행당 한 번만 동작합니다.
    private void CleanupOldVideos()
    {
        if (_oldVideosCleaned)
            return;

        _oldVideosCleaned = true;

        VideoSettingJson setting = JsonManager.instance != null
            ? JsonManager.instance.videoSettingJson
            : null;

        if (setting == null || string.IsNullOrWhiteSpace(setting.folderPath))
        {
            Debug.LogWarning("[VideoPlayback] 오래된 비디오 정리 건너뜀 / folderPath가 비어 있습니다.");
            return;
        }

        if (!Directory.Exists(setting.folderPath))
        {
            Debug.LogWarning($"[VideoPlayback] 오래된 비디오 정리 건너뜀 / 폴더가 존재하지 않습니다 / {setting.folderPath}");
            return;
        }

        int expireDays = Mathf.Max(1, _videoExpireDays);
        DateTime threshold = DateTime.Now.AddDays(-expireDays);
        int deletedCount = 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(setting.folderPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[VideoPlayback] 폴더 목록 읽기 실패 / {exception.Message}");
            return;
        }

        foreach (string file in files)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (Array.IndexOf(_videoExtensions, extension) < 0)
                continue;

            try
            {
                if (File.GetLastWriteTime(file) > threshold)
                    continue;

                File.Delete(file);
                deletedCount++;
                Debug.Log($"[VideoPlayback] 오래된 비디오 삭제 / {file}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VideoPlayback] 비디오 삭제 실패 / {file} / {exception.Message}");
            }
        }

        Debug.Log($"[VideoPlayback] 오래된 비디오 정리 완료 / 기준 {expireDays}일 / 삭제 {deletedCount}개");
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
        // 공유폴더(UNC) 사용 시 호스트 PC가 잠시 꺼져 있거나 네트워크가 끊겼을 수 있으므로,
        // 파일 안정성 검사 전에 폴더 자체가 접근 가능한지 먼저 대기/확인합니다.
        bool folderReady = false;
        yield return WaitUntilFolderReady(setting.folderPath, setting, ok => folderReady = ok);

        if (!folderReady)
        {
            Debug.LogError(
                $"[VideoPlayback] 공유폴더에 접근할 수 없어 재생을 중단합니다 / {setting.folderPath}\n" +
                "확인: ① 공유기/호스트 PC 전원·네트워크 연결 ② 공유 권한 및 자격 증명 ③ folderPath 경로(UNC: \\\\IP\\공유명)");
            _prepareCoroutine = null;
            yield break;
        }

        Debug.Log($"[VideoPlayback] 파일 안정성 대기 시작 / {fullPath}");

        bool ready = false;
        yield return WaitUntilFileReady(fullPath, setting, success => ready = success);

        if (!ready)
        {
            Debug.LogError(
                $"[VideoPlayback] 파일 준비 실패 또는 시간 초과 / {fullPath}\n" +
                "확인: 업로드 미완료(전송 중)·네트워크 지연으로 maxWaitSeconds 초과·파일 접근 권한·파일명 불일치 가능성");
            _prepareCoroutine = null;
            yield break;
        }

        if (_videoPlayer.isPlaying)
            _videoPlayer.Stop();

        // 로컬 절대경로/UNC 경로 모두 VideoPlayer가 안정적으로 열 수 있도록 file:// URL로 변환합니다.
        _videoPlayer.url = ToVideoPlayerUrl(fullPath);
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

        // 게임 타이머가 기준(마스터)입니다. 동영상 native 재생 속도를 타이머 진행 속도에 맞춰 두면
        // 매 프레임 시킹 없이도 대체로 따라가고, 남는 오차는 Update의 드리프트 보정이 잡아줍니다.
        ApplyPlaybackSpeedFromTimer();
        _isSyncingTimer = true;

        Debug.Log("[VideoPlayback] R 키 입력 / 재생 시작");
    }

    // 타이머가 0→targetTime 까지 흐르는 실시간 길이에 맞춰 동영상 전체 길이가 소모되도록 재생 속도를 설정합니다.
    private void ApplyPlaybackSpeedFromTimer()
    {
        GameTimer timer = GameTimer.Instance;
        if (timer == null || _videoPlayer == null)
            return;

        double length = _videoPlayer.length;
        float timerSpeed = Mathf.Max(0.0001f, timer.settingGameScale);
        float timerRealDuration = timer.targetTime / timerSpeed; // 타이머가 끝까지 흐르는 데 걸리는 실제 시간(초)
        if (length <= 0d || timerRealDuration <= 0f)
            return;

        // VideoPlayer.playbackSpeed 허용 범위(0~10) 안으로 제한합니다.
        float speed = Mathf.Clamp((float)(length / timerRealDuration), 0.0625f, 10f);
        _videoPlayer.playbackSpeed = speed;
    }

    private void Update()
    {
        if (!_isSyncingTimer || _videoPlayer == null || !_videoPlayer.isPlaying)
            return;

        GameTimer timer = GameTimer.Instance;
        double length = _videoPlayer.length;
        if (timer == null || length <= 0d)
            return;

        // 타이머의 정규화 진행도(0~1)를 기준으로 동영상이 있어야 할 위치를 계산합니다.
        double targetVideoTime = timer.GetNormalizedProgress() * length;

        // 드리프트가 임계값을 넘을 때만 해당 위치로 보정해 불필요한 시킹을 줄입니다. (타이머가 항상 기준)
        double drift = targetVideoTime - _videoPlayer.time;
        if (Math.Abs(drift) > _syncDriftThresholdSeconds)
            _videoPlayer.time = targetVideoTime;
    }

    // VideoPlayer.url에 넣을 경로를 형식에 맞게 보정합니다.
    // - 이미 스킴(file://, http:// 등)이 붙은 경우: 그대로 사용
    // - UNC 공유경로(\\host\share\...): 원본 경로 그대로 사용
    //   (new Uri().AbsoluteUri는 file://host/... 를 만드는데, Unity VideoPlayer(Media Foundation)는
    //    이 호스트명 형식을 "Cannot read file"로 거부합니다. 원본 UNC 경로를 직접 줘야 재생됩니다.)
    // - 로컬 절대경로(C:\...): file:///C:/... 로 변환
    private static string ToVideoPlayerUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // 이미 스킴이 붙은 URL(file://, http://, https:// 등)이면 그대로 둡니다.
        if (path.Contains("://"))
            return path;

        // UNC 경로(\\... 또는 //...)는 변환하지 않고 원본을 그대로 사용합니다.
        if (path.StartsWith("\\\\") || path.StartsWith("//"))
            return path;

        try
        {
            // 로컬 절대경로만 file:/// URL로 변환합니다. (C:\dir\a.mp4 → file:///C:/dir/a.mp4)
            return new Uri(path).AbsoluteUri;
        }
        catch (Exception exception)
        {
            // 변환 실패 시 원본 경로를 그대로 사용합니다(로컬 경로는 보통 이대로도 재생 가능).
            Debug.LogWarning($"[VideoPlayback] URL 변환 실패 / 원본 경로 사용 / {path} / {exception.Message}");
            return path;
        }
    }

    private enum FolderStatus { Accessible, NotFound, AccessDenied, NetworkError }

    // 공유폴더(UNC 포함) 접근 가능 여부를 확인하고, 네트워크 단절/권한 문제를 구분해 detail로 돌려줍니다.
    private static FolderStatus CheckFolderStatus(string folderPath, out string detail)
    {
        detail = string.Empty;
        try
        {
            if (Directory.Exists(folderPath))
                return FolderStatus.Accessible;

            detail = "경로를 찾을 수 없습니다(공유폴더 미연결·호스트 오프라인·경로 오타 가능).";
            return FolderStatus.NotFound;
        }
        catch (UnauthorizedAccessException exception)
        {
            detail = $"접근 권한이 없습니다(공유 권한·자격 증명 확인). {exception.Message}";
            return FolderStatus.AccessDenied;
        }
        catch (IOException exception)
        {
            detail = $"네트워크 오류로 접근 실패(공유기·호스트 연결 확인). {exception.Message}";
            return FolderStatus.NetworkError;
        }
        catch (Exception exception)
        {
            detail = exception.Message;
            return FolderStatus.NetworkError;
        }
    }

    // 공유폴더가 접근 가능해질 때까지 대기합니다. 권한 거부는 기다려도 풀리지 않으므로 즉시 실패 처리합니다.
    private IEnumerator WaitUntilFolderReady(string folderPath, VideoSettingJson setting, Action<bool> onComplete)
    {
        float interval = Mathf.Max(0.05f, setting.stabilityCheckIntervalSeconds);
        float maxWait = Mathf.Max(1f, setting.maxWaitSeconds);
        float startTime = Time.realtimeSinceStartup;
        string lastDetail = null;

        while (true)
        {
            FolderStatus status = CheckFolderStatus(folderPath, out string detail);

            if (status == FolderStatus.Accessible)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            // 권한 거부는 재시도해도 동일하므로 곧바로 실패로 종료합니다.
            if (status == FolderStatus.AccessDenied)
            {
                Debug.LogError($"[VideoPlayback] 공유폴더 접근 거부 / {folderPath} / {detail}");
                onComplete?.Invoke(false);
                yield break;
            }

            // 동일한 사유의 반복 로그를 줄이기 위해 사유가 바뀔 때만 출력합니다.
            if (detail != lastDetail)
            {
                Debug.LogWarning($"[VideoPlayback] 공유폴더 대기 중 / {folderPath} / {detail}");
                lastDetail = detail;
            }

            if (Time.realtimeSinceStartup - startTime > maxWait)
            {
                Debug.LogError($"[VideoPlayback] 공유폴더 연결 시간 초과 / {folderPath} / {detail}");
                onComplete?.Invoke(false);
                yield break;
            }

            yield return new WaitForSeconds(interval);
        }
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
        // 타이머가 기준이므로 동영상이 먼저 끝나도 타이머의 종료(OnTimeOver→Ended)에 맡깁니다. 여기서는 동기화만 중단합니다.
        _isSyncingTimer = false;

        // TODO: 재생 종료 후 추가 동작(결과 화면 전환 등)을 여기에 작성하세요.
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

        // 재생을 강제 중단할 때는 타이머 동기화도 해제합니다.
        _isSyncingTimer = false;

        SetRawImageActive(false);
        _isReadyToPlay = false;
    }
}
