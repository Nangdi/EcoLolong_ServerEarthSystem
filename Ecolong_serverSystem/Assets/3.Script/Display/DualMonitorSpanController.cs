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

    private void Start()
    {
        if (!_applySpanInBuild || Application.isEditor)
            return;

        ApplySpanWindow();
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
