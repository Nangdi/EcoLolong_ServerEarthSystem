using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

// 리플레이(리플레이 키) 구간 동안 화면 전체를 녹화해 mp4로 저장합니다.
// - 녹화는 외부 ffmpeg 프로세스(gdigrab)로 수행하며, Windows에서만 동작합니다.
// - 저장 파일 이름은 녹화를 시작한 시각의 "yyyyMMdd_HHmmss.mp4" 형식입니다.
// - 프로그램이 켜질 때 보관 기간(기본 30일)이 지난 영상은 자동으로 삭제합니다.
//
// 씬에 직접 배치할 필요는 없습니다. RuntimeInitializeOnLoadMethod로 실행 시 자동 생성되며,
// 씬에 수동으로 배치해 두었다면 그 인스턴스가 우선 사용됩니다.
public class ReplayScreenRecorder : MonoBehaviour
{
    // 저장 파일 이름 및 보관 기간 판정에 사용하는 날짜/시간 형식입니다.
    private const string FileTimeFormat = "yyyyMMdd_HHmmss";
    private const string FileExtension = ".mp4";
    // 정지 신호(q) 후 ffmpeg가 mp4 인덱스를 마무리할 때까지 기다리는 최대 시간(ms)입니다.
    private const int StopWaitMilliseconds = 5000;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static ReplayScreenRecorder s_instance;

    private Process _ffmpeg;
    private string _currentFilePath;
    private bool _isReplaySubscribed;
    private GameManager _gameManager;

    public bool IsRecording => _ffmpeg != null && !_ffmpeg.HasExited;

    // 씬에 배치하지 않아도 동작하도록 씬 로드 직후 자동으로 인스턴스를 만듭니다.
    // JsonManager가 Awake에서 설정을 이미 읽어 둔 뒤이므로 이 시점에 설정을 참조할 수 있습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null)
            return;

        if (FindObjectOfType<ReplayScreenRecorder>() != null)
            return;

        GameObject host = new GameObject(nameof(ReplayScreenRecorder));
        host.AddComponent<ReplayScreenRecorder>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
    }

    private void Start()
    {
        // 프로그램이 켜질 때 보관 기간이 지난 오래된 영상을 정리합니다.
        DeleteExpiredRecordings();
        TrySubscribeReplay();
    }

    private void Update()
    {
        // GameManager가 늦게 초기화되는 케이스를 위해 구독을 반복 시도합니다.
        TrySubscribeReplay();

        if (!IsRecording)
            return;

        // 리플레이가 끝나(Ended) 상태가 바뀌거나 F5 강제 초기화로 Ready로 돌아가면 녹화를 종료합니다.
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.CurrentGameState != GameState.TimeOut || !manager.IsReplay)
            StopRecording();
    }

    private void OnDestroy()
    {
        UnsubscribeReplay();
        StopRecording();

        if (s_instance == this)
            s_instance = null;
    }

    private void OnApplicationQuit()
    {
        // 종료 시 녹화 중이면 파일이 깨지지 않도록 정상 종료 신호를 보냅니다.
        StopRecording();
    }

    private void TrySubscribeReplay()
    {
        if (_isReplaySubscribed)
            return;

        _gameManager = GameManager.Instance;
        if (_gameManager == null)
            return;

        _gameManager.OnReplay += OnReplayStarted;
        _isReplaySubscribed = true;
    }

    private void UnsubscribeReplay()
    {
        if (!_isReplaySubscribed || _gameManager == null)
            return;

        _gameManager.OnReplay -= OnReplayStarted;
        _isReplaySubscribed = false;
    }

    // 리플레이 시작(리플레이 키) 시점에 호출됩니다.
    private void OnReplayStarted()
    {
        GameSettingData settings = GetSettings();
        if (settings != null && !settings.recordReplay)
        {
            Debug.Log("[ReplayRecorder] recordReplay=false 이므로 녹화를 시작하지 않습니다.");
            return;
        }

        StartRecording();
    }

    // ffmpeg(gdigrab)로 화면 전체 녹화를 시작합니다.
    public void StartRecording()
    {
        // 이전 녹화가 남아 있으면 먼저 정리한 뒤 새로 시작합니다.
        if (IsRecording)
            StopRecording();

        if (Application.platform != RuntimePlatform.WindowsPlayer
            && Application.platform != RuntimePlatform.WindowsEditor)
        {
            Debug.LogWarning("[ReplayRecorder] 화면 녹화는 Windows에서만 지원됩니다.");
            return;
        }

        string ffmpegPath = ResolveFFmpegPath();
        string folder = ResolveRecordFolder();

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayRecorder] 저장 폴더를 만들지 못했습니다: {folder} / {e.Message}");
            return;
        }

        string filePath = Path.Combine(folder, DateTime.Now.ToString(FileTimeFormat, CultureInfo.InvariantCulture) + FileExtension);

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = BuildFFmpegArguments(filePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                WorkingDirectory = folder
            };

            _ffmpeg = new Process { StartInfo = startInfo };
            // stderr를 비워 주지 않으면 파이프 버퍼가 차서 ffmpeg가 멈출 수 있으므로 비동기로 읽어 둡니다.
            _ffmpeg.ErrorDataReceived += OnFFmpegError;
            _ffmpeg.Start();
            _ffmpeg.BeginErrorReadLine();

            _currentFilePath = filePath;
            Debug.Log($"[ReplayRecorder] 화면 녹화 시작 → {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayRecorder] 녹화를 시작하지 못했습니다({ffmpegPath}): {e.Message}"
                + " ffmpeg.exe를 StreamingAssets/ffmpeg/ 아래에 두거나 설정의 recordFFmpegPath에 경로를 지정하세요.");
            DisposeProcess();
        }
    }

    // 녹화를 정상 종료합니다. ffmpeg에 q를 보내 mp4 헤더까지 마무리하게 합니다.
    public void StopRecording()
    {
        if (_ffmpeg == null)
            return;

        string savedPath = _currentFilePath;

        try
        {
            if (!_ffmpeg.HasExited)
            {
                _ffmpeg.StandardInput.Write("q");
                _ffmpeg.StandardInput.Flush();

                if (!_ffmpeg.WaitForExit(StopWaitMilliseconds))
                {
                    Debug.LogWarning("[ReplayRecorder] ffmpeg가 제때 종료되지 않아 강제 종료합니다. 파일이 손상될 수 있습니다.");
                    _ffmpeg.Kill();
                    _ffmpeg.WaitForExit(StopWaitMilliseconds);
                }
            }

            Debug.Log($"[ReplayRecorder] 화면 녹화 종료 → {savedPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayRecorder] 녹화 종료 처리 중 오류: {e.Message}");
        }
        finally
        {
            DisposeProcess();
        }
    }

    private void DisposeProcess()
    {
        if (_ffmpeg != null)
        {
            _ffmpeg.ErrorDataReceived -= OnFFmpegError;
            _ffmpeg.Dispose();
            _ffmpeg = null;
        }

        _currentFilePath = null;
    }

    private static void OnFFmpegError(object sender, DataReceivedEventArgs e)
    {
        // ffmpeg는 진행 상황도 stderr로 출력하므로 파이프만 비우고 로그로는 남기지 않습니다.
        // (실패 원인 추적이 필요하면 아래 주석을 해제하세요.)
        // if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[ffmpeg] {e.Data}");
    }

    // gdigrab로 가상 화면(모든 모니터 합산) 전체를 잡아 H.264 mp4로 인코딩하는 인자를 만듭니다.
    private string BuildFFmpegArguments(string filePath)
    {
        GameSettingData settings = GetSettings();
        int frameRate = settings != null && settings.recordFrameRate > 0 ? settings.recordFrameRate : 30;

        GetVirtualScreen(out int originX, out int originY, out int width, out int height);

        string audioInput = string.Empty;
        string audioCodec = string.Empty;
        if (settings != null && !string.IsNullOrEmpty(settings.recordAudioDevice))
        {
            // 시스템 소리를 함께 담으려면 dshow 오디오 장치 이름(예: 스테레오 믹스)을 설정에 넣습니다.
            audioInput = $"-f dshow -i audio=\"{settings.recordAudioDevice}\" ";
            audioCodec = "-c:a aac -b:a 160k ";
        }

        return "-y -hide_banner -loglevel error "
            + $"-f gdigrab -framerate {frameRate} -draw_mouse 0 "
            + $"-offset_x {originX} -offset_y {originY} -video_size {width}x{height} -i desktop "
            + audioInput
            + "-c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p "
            + audioCodec
            + $"-r {frameRate} \"{filePath}\"";
    }

    // 모든 모니터를 포함한 가상 화면의 원점과 크기를 구합니다.
    // H.264(yuv420p)는 가로/세로가 짝수여야 하므로 홀수면 1픽셀 줄입니다.
    private static void GetVirtualScreen(out int originX, out int originY, out int width, out int height)
    {
        originX = 0;
        originY = 0;
        width = Screen.currentResolution.width;
        height = Screen.currentResolution.height;

        try
        {
            int virtualX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int virtualY = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (virtualWidth > 0 && virtualHeight > 0)
            {
                originX = virtualX;
                originY = virtualY;
                width = virtualWidth;
                height = virtualHeight;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayRecorder] 가상 화면 크기를 읽지 못해 기본 해상도를 사용합니다: {e.Message}");
        }

        if (width % 2 != 0)
            width -= 1;

        if (height % 2 != 0)
            height -= 1;
    }

    // 녹화 파일을 저장할 폴더 경로입니다.
    // 설정이 비어 있으면 실행 파일 옆(에디터에서는 프로젝트 루트)의 ReplayRecordings 폴더를 사용합니다.
    public static string ResolveRecordFolder()
    {
        GameSettingData settings = GetSettings();
        if (settings != null && !string.IsNullOrEmpty(settings.recordFolderPath))
            return settings.recordFolderPath;

        return Path.GetFullPath(Path.Combine(Application.dataPath, "../ReplayRecordings"));
    }

    // ffmpeg 실행 파일 경로를 설정 → StreamingAssets → 시스템 PATH 순으로 찾습니다.
    private static string ResolveFFmpegPath()
    {
        GameSettingData settings = GetSettings();
        if (settings != null && !string.IsNullOrEmpty(settings.recordFFmpegPath) && File.Exists(settings.recordFFmpegPath))
            return settings.recordFFmpegPath;

        string bundled = Path.Combine(Application.streamingAssetsPath, "ffmpeg/ffmpeg.exe");
        if (File.Exists(bundled))
            return bundled;

        string bundledFlat = Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe");
        if (File.Exists(bundledFlat))
            return bundledFlat;

        // PATH에 등록된 ffmpeg가 있으면 그대로 사용합니다. 없으면 실행 시점에 예외로 걸러집니다.
        return "ffmpeg";
    }

    // 보관 기간(기본 30일)이 지난 녹화 파일을 삭제합니다. 프로그램 시작 시 1회 호출됩니다.
    public static void DeleteExpiredRecordings()
    {
        GameSettingData settings = GetSettings();
        int retentionDays = settings != null ? settings.recordRetentionDays : 30;

        // 0 이하이면 자동 삭제를 사용하지 않는 것으로 봅니다.
        if (retentionDays <= 0)
            return;

        string folder = ResolveRecordFolder();
        if (!Directory.Exists(folder))
            return;

        DateTime threshold = DateTime.Now.AddDays(-retentionDays);
        int deleted = 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*" + FileExtension);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayRecorder] 녹화 폴더를 읽지 못했습니다: {folder} / {e.Message}");
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            if (GetRecordingTime(files[i]) > threshold)
                continue;

            try
            {
                File.Delete(files[i]);
                deleted++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReplayRecorder] 오래된 녹화 파일 삭제 실패: {files[i]} / {e.Message}");
            }
        }

        if (deleted > 0)
            Debug.Log($"[ReplayRecorder] {retentionDays}일이 지난 녹화 파일 {deleted}개를 삭제했습니다. ({folder})");
    }

    // 파일 이름(yyyyMMdd_HHmmss)에서 녹화 시각을 읽고, 형식이 다르면 파일 수정 시각을 사용합니다.
    private static DateTime GetRecordingTime(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        if (DateTime.TryParseExact(name, FileTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            return parsed;

        return File.GetLastWriteTime(filePath);
    }

    private static GameSettingData GetSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        return jsonManager != null ? jsonManager.gameSettingData : null;
    }
}
