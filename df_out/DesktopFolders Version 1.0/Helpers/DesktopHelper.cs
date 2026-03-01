using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopFolders.Helpers
{
    public static class DesktopHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Show Desktop on Win8+ cloaks windows via DWM — no WndProc message fires.
        // DWMWA_CLOAKED is the only reliable detection method.
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute,
            out int pvAttribute, uint cbAttribute);

        private const uint DWMWA_CLOAKED     = 14;
        private const int  SW_SHOWNOACTIVATE = 4;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private const uint SWP_NOMOVE     = 0x0002;
        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW  = 0x00040000;

        // Exposed for DesktopDropWindow to set WS_EX_TRANSPARENT
        public static int  GetExStyle(IntPtr hwnd) => GetWindowLong(hwnd, GWL_EXSTYLE);
        public static void SetExStyle(IntPtr hwnd, int style) =>
            SetWindowLong(hwnd, GWL_EXSTYLE, style);

        /// <summary>
        /// Configures a window to live on the desktop layer. Does NOT use
        /// SetParent/WS_CHILD — that corrupts WPF's HwndSource and prevents
        /// the window from rendering.  Show Desktop is handled by the
        /// per-widget watchdog timer via IsHiddenOrCloaked / RestoreWindow.
        /// </summary>
        public static void PinToDesktop(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |=  WS_EX_NOACTIVATE;
            ex |=  WS_EX_TOOLWINDOW;
            ex &= ~WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);

            SendToBottom(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }

        public static void SendToBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void RestoreWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            SendToBottom(hwnd);
        }

        public static bool IsHiddenOrCloaked(IntPtr hwnd)
        {
            if (IsIconic(hwnd))         return true;
            if (!IsWindowVisible(hwnd)) return true;
            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED,
                    out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;
            return false;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam,
            IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE     = 0x0021;
            const int WM_SETFOCUS          = 0x0007;
            const int WM_WINDOWPOSCHANGING = 0x0046;
            const int MA_NOACTIVATE        = 3;

            switch (msg)
            {
                case WM_MOUSEACTIVATE:
                    handled = true;
                    return new IntPtr(MA_NOACTIVATE);
                case WM_SETFOCUS:
                    handled = true;
                    return IntPtr.Zero;
                case WM_WINDOWPOSCHANGING:
                    if (lParam != IntPtr.Zero)
                    {
                        var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                        if (wp.hwndInsertAfter != HWND_BOTTOM)
                        {
                            wp.hwndInsertAfter = HWND_BOTTOM;
                            wp.flags          |= SWP_NOACTIVATE;
                            Marshal.StructureToPtr(wp, lParam, false);
                        }
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd, hwndInsertAfter;
            public int    x, y, cx, cy;
            public uint   flags;
        }
    }
}
