using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DualMonitorSpanController : MonoBehaviour
{
    public enum ResolutionMode
    {
        Auto,
        Manual
    }

    [Header("스팬 창 설정")]
    [SerializeField] private bool applySpanInBuild = true;
    [SerializeField] private bool forceBorderlessWindow = true;

    [Header("해상도 모드")]
    [Tooltip("Auto: 가상 화면(모니터 합산) 자동 인식 / Manual: 아래 값으로 강제 적용")]
    [SerializeField] private ResolutionMode resolutionMode = ResolutionMode.Auto;

    [Header("수동 해상도 설정 (Manual 모드 전용)")]
    [SerializeField] private int manualWidth = 3840;
    [SerializeField] private int manualHeight = 1080;
    [Tooltip("창의 좌상단 원점 좌표 (가상 화면 기준)")]
    [SerializeField] private int manualOriginX = 0;
    [SerializeField] private int manualOriginY = 0;

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

    // 빌드 실행 시 현재 윈도우의 전체 가상 화면 크기에 맞춰 스팬 창을 적용합니다.
    private void Start()
    {
        if (!applySpanInBuild || Application.isEditor)
            return;

        ApplySpanWindow();
    }

    // 두 모니터가 하나의 큰 화면처럼 보이도록 윈도우 크기와 위치를 설정합니다.
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

        if (forceBorderlessWindow)
            SetWindowLong(windowHandle, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        SetWindowPos(windowHandle, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight, SWP_SHOWWINDOW);
        Debug.Log($"스팬 창 적용 완료 [{resolutionMode}]: {targetWidth}x{targetHeight} / origin({targetX}, {targetY})");
    }

    // 현재 모드와 인스펙터 값을 조합해 적용할 창의 크기/원점을 계산합니다.
    private void ResolveTargetRect(out int targetX, out int targetY, out int targetWidth, out int targetHeight)
    {
        if (resolutionMode == ResolutionMode.Manual)
        {
            targetX = manualOriginX;
            targetY = manualOriginY;
            targetWidth = manualWidth;
            targetHeight = manualHeight;
            return;
        }

        targetX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        targetY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        targetWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        targetHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
    }
}
