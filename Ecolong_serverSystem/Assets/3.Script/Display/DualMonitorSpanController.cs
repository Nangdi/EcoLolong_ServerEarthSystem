using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DualMonitorSpanController : MonoBehaviour
{
    [Header("스팬 창 설정")]
    [SerializeField] private bool applySpanInBuild = true;
    [SerializeField] private bool forceBorderlessWindow = true;

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
        int virtualX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int virtualY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(virtualWidth, virtualHeight, false);

        IntPtr windowHandle = GetActiveWindow();
        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogWarning("스팬 창 핸들을 찾지 못했습니다.");
            return;
        }

        if (forceBorderlessWindow)
            SetWindowLong(windowHandle, GWL_STYLE, WS_POPUP | WS_VISIBLE);

        SetWindowPos(windowHandle, IntPtr.Zero, virtualX, virtualY, virtualWidth, virtualHeight, SWP_SHOWWINDOW);
        Debug.Log($"스팬 창 적용 완료: {virtualWidth}x{virtualHeight} / origin({virtualX}, {virtualY})");
    }
}
