using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TransparentWindow
{
    internal sealed class WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public string ProcessName;
        public uint ProcessId;
        public bool IsLayered;

        public override string ToString()
        {
            return Title;
        }
    }

    internal sealed class OriginalState
    {
        public long ExStyle;
        public bool WasLayered;
        public bool HasAlphaInfo;
        public uint Key;
        public byte Alpha;
        public uint Flags;
    }

    /// <summary>
    /// 负责把任意顶层窗口变成 WS_EX_LAYERED 窗口并设置整体透明度。
    /// 会记录每个窗口的原始状态，以便随时完整还原。
    /// </summary>
    internal static class OpacityService
    {
        private static readonly Dictionary<IntPtr, OriginalState> States = new Dictionary<IntPtr, OriginalState>();

        public static int PercentToAlpha(int percent)
        {
            if (percent < 1) percent = 1;
            if (percent > 100) percent = 100;
            int a = (int)Math.Round(percent * 255.0 / 100.0, MidpointRounding.AwayFromZero);
            if (a < 1) a = 1;
            if (a > 255) a = 255;
            return a;
        }

        public static int GetCurrentPercent(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return 100;
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            if ((ex & Native.WS_EX_LAYERED) == 0) return 100;

            uint key;
            byte alpha;
            uint flags;
            if (Native.GetLayeredWindowAttributes(hwnd, out key, out alpha, out flags))
            {
                if ((flags & Native.LWA_ALPHA) == 0) return 100;
                int p = (int)Math.Round(alpha * 100.0 / 255.0);
                if (p < 1) p = 1;
                if (p > 100) p = 100;
                return p;
            }
            return 100;
        }

        public static bool Apply(IntPtr hwnd, int percent, out string error)
        {
            error = null;
            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            {
                error = "目标窗口已经关闭";
                return false;
            }

            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            bool layered = (ex & Native.WS_EX_LAYERED) != 0;

            OriginalState state;
            if (!States.TryGetValue(hwnd, out state))
            {
                state = new OriginalState();
                state.ExStyle = ex;
                state.WasLayered = layered;
                state.HasAlphaInfo = false;
                if (layered)
                {
                    uint key;
                    byte alpha;
                    uint flags;
                    if (Native.GetLayeredWindowAttributes(hwnd, out key, out alpha, out flags))
                    {
                        state.HasAlphaInfo = true;
                        state.Key = key;
                        state.Alpha = alpha;
                        state.Flags = flags;
                    }
                }
                States[hwnd] = state;
            }

            if (!layered)
            {
                Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex | Native.WS_EX_LAYERED));
                long check = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
                if ((check & Native.WS_EX_LAYERED) == 0)
                {
                    int code = Marshal.GetLastWin32Error();
                    States.Remove(hwnd);
                    error = DescribeFailure(code, true);
                    return false;
                }
            }

            byte alphaTarget = (byte)PercentToAlpha(percent);
            if (!Native.SetLayeredWindowAttributes(hwnd, 0, alphaTarget, Native.LWA_ALPHA))
            {
                int code = Marshal.GetLastWin32Error();
                if (!state.WasLayered)
                {
                    Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(state.ExStyle));
                }
                else
                {
                    RestoreState(hwnd, state);
                }
                States.Remove(hwnd);
                error = DescribeFailure(code, false);
                return false;
            }

            return true;
        }

        public static bool Restore(IntPtr hwnd)
        {
            OriginalState state;
            if (!States.TryGetValue(hwnd, out state)) return false;
            States.Remove(hwnd);
            if (!Native.IsWindow(hwnd)) return true;
            RestoreState(hwnd, state);
            return true;
        }

        public static void RestoreAll()
        {
            List<IntPtr> handles = new List<IntPtr>(States.Keys);
            foreach (IntPtr h in handles)
            {
                Restore(h);
            }
        }

        public static bool IsTracked(IntPtr hwnd)
        {
            return States.ContainsKey(hwnd);
        }

        private static void RestoreState(IntPtr hwnd, OriginalState state)
        {
            if (state.WasLayered && state.HasAlphaInfo)
            {
                Native.SetLayeredWindowAttributes(hwnd, state.Key, state.Alpha, state.Flags);
                return;
            }
            if (!state.WasLayered)
            {
                Native.SetLayeredWindowAttributes(hwnd, 0, 255, Native.LWA_ALPHA);
                long cur = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
                Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(cur & ~Native.WS_EX_LAYERED));
            }
        }

        private static string DescribeFailure(int code, bool onStyle)
        {
            if (code == 5)
            {
                return "权限不足：目标窗口由管理员权限的程序创建，请右键以管理员身份重新运行本工具";
            }
            if (code == 87)
            {
                return "该窗口使用了逐像素透明（UpdateLayeredWindow），无法用这种方式调节";
            }
            if (code == 1400)
            {
                return "窗口句柄无效，目标窗口可能已经关闭";
            }
            string s = onStyle ? "无法为窗口启用透明支持" : "无法设置窗口透明度";
            return s + "（Win32 错误 " + code + "）";
        }

        public static List<WindowInfo> EnumerateWindows()
        {
            List<WindowInfo> list = new List<WindowInfo>();
            int ownPid = Process.GetCurrentProcess().Id;

            Native.EnumWindows(delegate (IntPtr h, IntPtr lParam)
            {
                try
                {
                    if (!Native.IsWindowVisible(h)) return true;
                    if (Native.GetWindowTextLength(h) <= 0) return true;

                    long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                    if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;

                    int cloaked;
                    if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0)
                    {
                        return true;
                    }

                    RECT r;
                    if (!Native.GetWindowRect(h, out r)) return true;
                    if (r.Width <= 0 || r.Height <= 0) return true;

                    string title = Native.GetTitle(h).Trim();
                    if (title.Length == 0) return true;

                    StringBuilder cls = new StringBuilder(256);
                    Native.GetClassName(h, cls, cls.Capacity);
                    string className = cls.ToString();
                    if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
                    {
                        return true;
                    }

                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (pid == (uint)ownPid) return true;

                    string pname = string.Empty;
                    try
                    {
                        pname = Process.GetProcessById((int)pid).ProcessName;
                    }
                    catch
                    {
                    }

                    WindowInfo info = new WindowInfo();
                    info.Handle = h;
                    info.Title = title;
                    info.ProcessName = pname;
                    info.ProcessId = pid;
                    info.IsLayered = (ex & Native.WS_EX_LAYERED) != 0;
                    list.Add(info);
                }
                catch
                {
                }
                return true;
            }, IntPtr.Zero);

            list.Sort(delegate (WindowInfo a, WindowInfo b)
            {
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        public static string GetProcessName(IntPtr hwnd)
        {
            try
            {
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return string.Empty;
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
