using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;

public class DualMonitorSpanController : MonoBehaviour
{
    public enum ResolutionMode
    {
        Auto,
        Manual
    }

    [Header("스팬 창 설정")]
    [FormerlySerializedAs("applySpanInBuild")]
    [SerializeField] private bool _applySpanInBuild = true;
    [FormerlySerializedAs("forceBorderlessWindow")]
    [SerializeField] private bool _forceBorderlessWindow = true;

    [Header("해상도 모드")]
    [Tooltip("Auto: 가상 화면(모니터 합산) 자동 인식 / Manual: 아래 값으로 강제 적용")]
    [FormerlySerializedAs("resolutionMode")]
    [SerializeField] private ResolutionMode _resolutionMode = ResolutionMode.Auto;

    [Header("수동 해상도 설정 (Manual 모드 전용)")]
    [FormerlySerializedAs("manualWidth")]
    [SerializeField] private int _manualWidth = 3840;
    [FormerlySerializedAs("manualHeight")]
    [SerializeField] private int _manualHeight = 1080;
    [Tooltip("창의 좌상단 원점 좌표 (가상 화면 기준)")]
    [FormerlySerializedAs("manualOriginX")]
    [SerializeField] private int _manualOriginX = 0;
    [FormerlySerializedAs("manualOriginY")]
    [SerializeField] private int _manualOriginY = 0;

    private const int GWL_STYLE = -16;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    // 마지막으로 창에 실제 적용한 설정 스냅샷입니다. Save 때 값이 그대로면 창을 다시 배치하지 않습니다.
    private bool _hasApplied;
    private bool _appliedSpan;
    private bool _appliedBorderless;
    private ResolutionMode _appliedMode;
    private int _appliedWidth;
    private int _appliedHeight;
    private int _appliedOriginX;
    private int _appliedOriginY;

    private void Start()
    {
        // ESC 설정창(GameSettingData)에 저장된 값을 우선 반영한 뒤 적용합니다.
        LoadFromGameSettings();

        if (!_applySpanInBuild || Application.isEditor)
            return;

        ApplySpanWindow();
    }

    // ESC 설정창에서 Save가 눌릴 때 호출됩니다. 변경된 설정을 읽어 즉시 스팬 창에 반영합니다.
    public void ApplyFromSettings()
    {
        LoadFromGameSettings();

        if (!_applySpanInBuild)
        {
            Debug.Log("[DualMonitorSpan] dualMonitorSpan=false 이므로 스팬 창을 적용하지 않습니다.");
            return;
        }

        if (Application.isEditor)
        {
            // 에디터 활성 창은 Unity 에디터 본체라 창 조작을 건너뜁니다(빌드에서만 동작).
            Debug.Log("[DualMonitorSpan] 에디터에서는 스팬 창이 적용되지 않습니다(빌드에서만 동작).");
            return;
        }

        // 듀얼모니터 설정이 직전 적용과 동일하면 창을 다시 배치하지 않습니다.
        // (Save를 누를 때마다 화면 위치/크기가 흔들리는 것을 방지)
        if (_hasApplied && !HasSpanConfigChanged())
        {
            Debug.Log("[DualMonitorSpan] 스팬 설정 변경 없음 → 창 재배치 생략.");
            return;
        }

        ApplySpanWindow();
    }

    // 현재 로드된 설정이 마지막으로 창에 적용한 스냅샷과 다른지 비교합니다.
    private bool HasSpanConfigChanged()
    {
        return _appliedSpan != _applySpanInBuild
            || _appliedBorderless != _forceBorderlessWindow
            || _appliedMode != _resolutionMode
            || _appliedWidth != _manualWidth
            || _appliedHeight != _manualHeight
            || _appliedOriginX != _manualOriginX
            || _appliedOriginY != _manualOriginY;
    }

    // 창 적용에 성공한 직후 현재 설정을 스냅샷으로 저장합니다.
    private void CaptureAppliedConfig()
    {
        _appliedSpan = _applySpanInBuild;
        _appliedBorderless = _forceBorderlessWindow;
        _appliedMode = _resolutionMode;
        _appliedWidth = _manualWidth;
        _appliedHeight = _manualHeight;
        _appliedOriginX = _manualOriginX;
        _appliedOriginY = _manualOriginY;
        _hasApplied = true;
    }

    // JsonManager의 GameSettingData 값으로 내부 설정을 동기화합니다. JsonManager가 없으면 인스펙터 값을 그대로 둡니다.
    private void LoadFromGameSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager == null || jsonManager.gameSettingData == null)
            return;

        GameSettingData settings = jsonManager.gameSettingData;
        _applySpanInBuild = settings.dualMonitorSpan;
        _forceBorderlessWindow = settings.dualMonitorBorderless;
        _resolutionMode = settings.dualMonitorManual ? ResolutionMode.Manual : ResolutionMode.Auto;
        _manualWidth = settings.dualMonitorWidth;
        _manualHeight = settings.dualMonitorHeight;
        _manualOriginX = settings.dualMonitorOriginX;
        _manualOriginY = settings.dualMonitorOriginY;
    }

    public void ApplySpanWindow()
    {
        ResolveTargetRect(out int targetX, out int targetY, out int targetWidth, out int targetHeight);
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            Debug.LogWarning($"스팬 창 해상도가 올바르지 않습니다: {targetWidth}x{targetHeight}");
            return;
        }

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(targetWidth, targetHeight, false);

        IntPtr windowHandle = GetActiveWindow();
        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogWarning("스팬 창 핸들을 찾지 못했습니다.");
            return;
        }

        if (_forceBorderlessWindow)
            SetWindowLong(windowHandle, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        SetWindowPos(windowHandle, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight, SWP_SHOWWINDOW);
        CaptureAppliedConfig();
        Debug.Log($"스팬 창 적용 완료 [{_resolutionMode}]: {targetWidth}x{targetHeight} / origin({targetX}, {targetY})");
    }

    private void ResolveTargetRect(out int targetX, out int targetY, out int targetWidth, out int targetHeight)
    {
        if (_resolutionMode == ResolutionMode.Manual)
        {
            targetX = _manualOriginX;
            targetY = _manualOriginY;
            targetWidth = _manualWidth;
            targetHeight = _manualHeight;
            return;
        }

        targetX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        targetY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        targetWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        targetHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
    }
}
