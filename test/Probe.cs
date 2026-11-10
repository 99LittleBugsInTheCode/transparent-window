using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace TransparentWindow
{
    internal static class Probe
    {
        private static int _failures;
        private static int _checks;

        private static void Check(bool condition, string name, string detail)
        {
            _checks++;
            if (condition)
            {
                Console.WriteLine("PASS  " + name + (detail.Length > 0 ? "  [" + detail + "]" : ""));
            }
            else
            {
                _failures++;
                Console.WriteLine("FAIL  " + name + (detail.Length > 0 ? "  [" + detail + "]" : ""));
            }
        }

        private static IntPtr FindWindowByTitle(string title)
        {
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (Native.IsWindowVisible(h) && Native.GetTitle(h) == title)
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static IntPtr WaitForWindow(string title, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                IntPtr h = FindWindowByTitle(title);
                if (h != IntPtr.Zero) return h;
                Thread.Sleep(100);
                waited += 100;
            }
            return IntPtr.Zero;
        }

        private static long ExStyle(IntPtr hwnd)
        {
            return Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        }

        private static byte Alpha(IntPtr hwnd)
        {
            uint key;
            byte alpha;
            uint flags;
            if (!Native.GetLayeredWindowAttributes(hwnd, out key, out alpha, out flags)) return 0;
            return alpha;
        }

        private static bool HasAlphaFlag(IntPtr hwnd)
        {
            uint key;
            byte alpha;
            uint flags;
            if (!Native.GetLayeredWindowAttributes(hwnd, out key, out alpha, out flags)) return false;
            return (flags & Native.LWA_ALPHA) != 0;
        }

        private static IntPtr FindByTitle(string title)
        {
            IntPtr exact = FindWindowByTitle(title);
            if (exact != IntPtr.Zero) return exact;
            IntPtr partial = IntPtr.Zero;
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (Native.IsWindowVisible(h) && Native.GetTitle(h).IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partial = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return partial;
        }

        private static int Cli(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--list")
            {
                foreach (WindowInfo w in OpacityService.EnumerateWindows())
                {
                    RECT r;
                    Native.GetWindowRect(w.Handle, out r);
                    Console.WriteLine("WIN hwnd=" + w.Handle.ToInt64() + " pid=" + w.ProcessId +
                        " layered=" + (w.IsLayered ? 1 : 0) + " pct=" + OpacityService.GetCurrentPercent(w.Handle) +
                        " rect=" + r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom +
                        " title=" + w.Title);
                }
                return 0;
            }
            if (args.Length >= 2 && (args[0] == "--set" || args[0] == "--state"))
            {
                IntPtr h = FindByTitle(args[1]);
                if (h == IntPtr.Zero)
                {
                    Console.WriteLine("NOTFOUND " + args[1]);
                    return 2;
                }
                if (args[0] == "--state")
                {
                    long ex = ExStyle(h);
                    int layered = (ex & Native.WS_EX_LAYERED) != 0 ? 1 : 0;
                    Console.WriteLine("STATE hwnd=" + h.ToInt64() + " layered=" + layered +
                        " alpha=" + Alpha(h) + " percent=" + OpacityService.GetCurrentPercent(h) +
                        " title=" + Native.GetTitle(h));
                    return 0;
                }
                int percent = 50;
                if (args.Length >= 3) int.TryParse(args[2], out percent);
                string error;
                bool ok = OpacityService.Apply(h, percent, out error);
                Console.WriteLine((ok ? "SET" : "ERROR") + " hwnd=" + h.ToInt64() + " percent=" + percent +
                    " alpha=" + Alpha(h) + " err=" + (error ?? ""));
                return ok ? 0 : 1;
            }
            return -1;
        }

        private static int Main(string[] args)
        {
            int cli = Cli(args);
            if (cli >= 0) return cli;

            bool visual = false;
            foreach (string a in args)
            {
                if (a == "--visual") visual = true;
            }

            Console.WriteLine("== Transparent Window probe ==");
            Console.WriteLine("OS: " + Environment.OSVersion.VersionString + "  64-bit process: " + (IntPtr.Size == 8));

            Check(OpacityService.PercentToAlpha(100) == 255, "PercentToAlpha(100) == 255", OpacityService.PercentToAlpha(100).ToString());
            Check(OpacityService.PercentToAlpha(1) >= 1, "PercentToAlpha(1) >= 1", OpacityService.PercentToAlpha(1).ToString());
            Check(OpacityService.PercentToAlpha(50) == 128, "PercentToAlpha(50) == 128", OpacityService.PercentToAlpha(50).ToString());

            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string testExe = Path.Combine(dir, "TestWindow.exe");
            if (!File.Exists(testExe))
            {
                Console.WriteLine("FAIL  TestWindow.exe not found at " + testExe);
                return 2;
            }

            Process proc = Process.Start(testExe);
            IntPtr hwnd = WaitForWindow("TW-TestWindow", 10000);
            Check(hwnd != IntPtr.Zero, "Test window created", hwnd.ToString("X"));
            if (hwnd == IntPtr.Zero)
            {
                try { proc.Kill(); } catch { }
                return 3;
            }

            long originalEx = ExStyle(hwnd);
            long originalLayered = originalEx & Native.WS_EX_LAYERED;
            Check(originalLayered == 0, "Test window starts non-layered", originalEx.ToString("X"));

            string error;
            bool ok = OpacityService.Apply(hwnd, 37, out error);
            Check(ok, "Apply 37%", error ?? "");
            Check((ExStyle(hwnd) & Native.WS_EX_LAYERED) != 0, "WS_EX_LAYERED set after apply", ExStyle(hwnd).ToString("X"));
            Check(HasAlphaFlag(hwnd), "LWA_ALPHA flag present", "");
            Check(Alpha(hwnd) == OpacityService.PercentToAlpha(37), "Readback alpha for 37%", Alpha(hwnd) + " (expected " + OpacityService.PercentToAlpha(37) + ")");
            Check(OpacityService.GetCurrentPercent(hwnd) == 37, "GetCurrentPercent == 37", OpacityService.GetCurrentPercent(hwnd).ToString());

            ok = OpacityService.Apply(hwnd, 1, out error);
            Check(ok, "Apply minimum 1%", error ?? "");
            Check(Alpha(hwnd) == OpacityService.PercentToAlpha(1) && Alpha(hwnd) > 0, "Alpha for 1% is non-zero", Alpha(hwnd).ToString());
            Check(OpacityService.GetCurrentPercent(hwnd) == 1, "GetCurrentPercent == 1", OpacityService.GetCurrentPercent(hwnd).ToString());

            ok = OpacityService.Apply(hwnd, 100, out error);
            Check(ok, "Apply 100%", error ?? "");
            Check(Alpha(hwnd) == 255, "Alpha 255 at 100%", Alpha(hwnd).ToString());

            ok = OpacityService.Apply(hwnd, 45, out error);
            Check(ok, "Apply 45%", error ?? "");
            Check(Alpha(hwnd) == OpacityService.PercentToAlpha(45), "Alpha for 45%", Alpha(hwnd).ToString());
            Check(OpacityService.IsTracked(hwnd), "Window tracked for restore", "");

            List<WindowInfo> windows = OpacityService.EnumerateWindows();
            bool listed = false;
            foreach (WindowInfo w in windows)
            {
                if (w.Title == "TW-TestWindow") listed = true;
            }
            Check(listed, "Enumeration lists the test window", windows.Count + " windows");

            ok = OpacityService.Apply(IntPtr.Zero, 50, out error);
            Check(!ok && !string.IsNullOrEmpty(error), "Zero handle rejected with message", error ?? "");

            if (visual)
            {
                Console.WriteLine("VISUAL-HWND=" + hwnd.ToInt64());
                Console.WriteLine("Visual check: window left at 45% transparency.");
                return 0;
            }

            Check(OpacityService.Restore(hwnd), "Restore single window", "");
            Check((ExStyle(hwnd) & Native.WS_EX_LAYERED) == 0, "WS_EX_LAYERED removed after restore", ExStyle(hwnd).ToString("X"));
            Check(ExStyle(hwnd) == originalEx, "ExStyle identical to original", ExStyle(hwnd).ToString("X"));
            Check(!OpacityService.IsTracked(hwnd), "Window no longer tracked", "");

            OpacityService.Apply(hwnd, 60, out error);
            OpacityService.RestoreAll();
            Check((ExStyle(hwnd) & Native.WS_EX_LAYERED) == 0, "RestoreAll removes layered style", ExStyle(hwnd).ToString("X"));

            try { proc.Kill(); } catch { }

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? "RESULT: ALL " + _checks + " CHECKS PASSED"
                : "RESULT: " + _failures + " OF " + _checks + " CHECKS FAILED");
            return _failures == 0 ? 0 : 1;
        }
    }
}
