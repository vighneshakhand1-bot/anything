using System;
using System.Runtime.InteropServices;

namespace Fluensia.Core
{
    /// <summary>
    /// Minimal window plumbing the reference UI needs that Veldrid/Veldrid.SDL2 does not expose
    /// through its public API: per-monitor DPI scale and the OS "blur behind" (acrylic / mac
    /// vibrancy equivalents). All calls are best-effort: failures are silent and the UI simply
    /// renders with its opaque glass colours instead.
    /// </summary>
    public static class Platform
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        // ---------------------------------------------------------------- DPI
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
        [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")] private static extern uint GetDpiForWindowWin(IntPtr hwnd);

        /// <summary>Monitors' DPI / 96. Returns 1 when unknown (Linux/macOS handled by SDL).</summary>
        public static float DpiScale(IntPtr hwnd)
        {
            try
            {
                if (IsWindows && hwnd != IntPtr.Zero)
                {
                    uint dpi;
                    try { dpi = GetDpiForWindowWin(hwnd); }
                    catch (EntryPointNotFoundException) { dpi = 0; }
                    if (dpi > 0) return dpi / 96f;
                    var mon = MonitorFromWindow(hwnd, 2 /* DPI_AWARENESS_CONTEXT default */);
                    if (mon != IntPtr.Zero && GetDpiForMonitor(mon, 0, out uint dx, out _) == 0 && dx > 0)
                        return dx / 96f;
                }
            }
            catch { /* shcore missing on Win7 */ }
            return 1f;
        }

        // ---------------------------------------------------------------- acrylic / blur
        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy { public int State; public int Flags; public int Color; public int Animation; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WinCompAttrData { public int Attribute; public IntPtr Data; public IntPtr SizeOfData; }
        [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WinCompAttrData data);

        /// <summary>Windows 10/11 acrylic behind the window (the reference has a live-blurred desktop).</summary>
        public static void EnableAcrylic(IntPtr hwnd, uint argb)
        {
            if (!IsWindows || hwnd == IntPtr.Zero) return;
            try
            {
                var policy = new AccentPolicy { State = 4 /* ACCENT_ENABLE_ACRYLICBLURBEHIND */, Flags = 2, Color = (int)argb, Animation = 0 };
                int size = Marshal.SizeOf(policy);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new WinCompAttrData { Attribute = 19 /* WCA_ACCENT_POLICY */, Data = ptr, SizeOfData = (IntPtr)size };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(ptr);
            }
            catch { }
        }

        // ---------------------------------------------------------------- window drag
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Hand the drag to the OS so the window moves natively while the mouse is down.</summary>
        public static void BeginWindowDrag(IntPtr hwnd)
        {
            if (!IsWindows || hwnd == IntPtr.Zero) return;
            try { ReleaseCapture(); SendMessage(hwnd, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero); }
            catch { }
        }
    }
}
