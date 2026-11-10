using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TransparentWindow
{
    internal static class UiE2E
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static int _checks;
        private static int _fails;

        private static void Check(bool ok, string name, string detail)
        {
            _checks++;
            if (!ok) _fails++;
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (detail.Length > 0 ? "  [" + detail + "]" : ""));
        }

        private static int Percent(IntPtr h)
        {
            return OpacityService.GetCurrentPercent(h);
        }

        private static bool Layered(IntPtr h)
        {
            return (Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64() & Native.WS_EX_LAYERED) != 0;
        }

        private static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(80);
            mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(90);
            mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(180);
        }

        private static double _scale;

        private static void ClickClient(IntPtr h, int baseX, int baseY)
        {
            POINT p = new POINT();
            RECT unused;
            GetClientRect(h, out unused);
            p.X = baseX;
            p.Y = baseY;
            ClientToScreen(h, ref p);
            ClickAt(p.X, p.Y);
        }

        private static IntPtr FindChild(IntPtr parent, string classPart, string exactText)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, delegate (IntPtr h, IntPtr l)
            {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(h, cls, 256);
                bool classOk = string.IsNullOrEmpty(classPart) ||
                    cls.ToString().IndexOf(classPart, StringComparison.OrdinalIgnoreCase) >= 0;
                bool textOk = exactText == null;
                if (!textOk)
                {
                    StringBuilder text = new StringBuilder(512);
                    GetWindowText(h, text, 512);
                    textOk = text.ToString() == exactText;
                }
                if (classOk && textOk)
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string ListItemText(IntPtr list, int index)
        {
            StringBuilder sb = new StringBuilder(512);
            SendMessage(list, 0x0189, new IntPtr(index), sb);
            return sb.ToString();
        }

        private static void KeyPress(byte vk)
        {
            keybd_event(vk, 0, 0, IntPtr.Zero);
            Thread.Sleep(60);
            keybd_event(vk, 0, 2, IntPtr.Zero);
            Thread.Sleep(200);
        }

        private static void Combo(byte mod1, byte mod2, byte vk)
        {
            keybd_event(mod1, 0, 0, IntPtr.Zero);
            keybd_event(mod2, 0, 0, IntPtr.Zero);
            Thread.Sleep(60);
            keybd_event(vk, 0, 0, IntPtr.Zero);
            Thread.Sleep(60);
            keybd_event(vk, 0, 2, IntPtr.Zero);
            keybd_event(mod1, 0, 2, IntPtr.Zero);
            keybd_event(mod2, 0, 2, IntPtr.Zero);
            Thread.Sleep(300);
        }

        private static void Screenshot(string path)
        {
            int w = GetSystemMetrics(0);
            int h = GetSystemMetrics(1);
            using (Bitmap bmp = new Bitmap(w, h))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(w, h));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static IntPtr WaitWindow(string title, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                IntPtr h = FindWindow(null, title);
                if (h != IntPtr.Zero) return h;
                Thread.Sleep(100);
                waited += 100;
            }
            return IntPtr.Zero;
        }

        private static int Main()
        {
            Console.WriteLine("== UI end-to-end test ==");
            IntPtr gui = FindWindow(null, "窗口透明度");
            if (gui == IntPtr.Zero)
            {
                Console.WriteLine("FAIL  main window not found - start TransparentWindow.exe first");
                return 2;
            }

            foreach (Process p in Process.GetProcessesByName("TestWindow"))
            {
                try { p.Kill(); } catch { }
            }
            Thread.Sleep(300);

            string testExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestWindow.exe");
            if (!File.Exists(testExe))
            {
                Console.WriteLine("FAIL  TestWindow.exe missing");
                return 2;
            }
            Process.Start(testExe, "--topmost --x 140 --y 600");
            IntPtr test = WaitWindow("TW-TestWindow", 10000);
            if (test == IntPtr.Zero)
            {
                Console.WriteLine("FAIL  test window did not start");
                return 2;
            }
            Thread.Sleep(400);

            RECT tr;
            GetWindowRect(test, out tr);
            Check(tr.Left >= 100 && tr.Right <= 900,
                "Test window placed in a safe area",
                tr.Left + "," + tr.Top + "," + tr.Right + "," + tr.Bottom);

            RECT cr;
            GetClientRect(gui, out cr);
            _scale = cr.Right / 404.0;
            Console.WriteLine("GUI client width " + cr.Right + " px, scale " + _scale);

            Check(!Layered(test) && Percent(test) == 100, "Test window starts opaque", "layered=" + Layered(test) + " pct=" + Percent(test));

            SetForegroundWindow(gui);
            Thread.Sleep(200);

            // --- picker dialog: open, cancel, reopen, choose the test window ---
            ClickClient(gui, 200, 78);
            Thread.Sleep(700);
            IntPtr dlg = FindWindow(null, "选择窗口");
            Check(dlg != IntPtr.Zero, "Picker dialog opens from target card", dlg.ToInt64().ToString());
            if (dlg == IntPtr.Zero) return 4;

            RECT dr;
            GetWindowRect(dlg, out dr);
            Check(dr.Top >= 0 && dr.Left >= 0 && dr.Bottom <= GetSystemMetrics(1) && dr.Right <= GetSystemMetrics(0),
                "Picker dialog fully on screen", dr.Left + "," + dr.Top + "," + dr.Right + "," + dr.Bottom);

            string pickerShot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "picker.png");
            try { Screenshot(pickerShot); } catch { }

            KeyPress(0x1B);
            Thread.Sleep(400);
            Check(FindWindow(null, "选择窗口") == IntPtr.Zero, "Picker closes on Escape", "");

            ClickClient(gui, 200, 78);
            Thread.Sleep(700);
            dlg = FindWindow(null, "选择窗口");
            Check(dlg != IntPtr.Zero, "Picker reopens", "");

            IntPtr list = FindChild(dlg, "LISTBOX", null);
            Check(list != IntPtr.Zero, "Picker list exists", "");
            int count = 0;
            int targetIndex = -1;
            if (list != IntPtr.Zero)
            {
                count = SendMessage(list, 0x018B, IntPtr.Zero, IntPtr.Zero).ToInt32();
                for (int i = 0; i < count; i++)
                {
                    if (ListItemText(list, i) == "TW-TestWindow") targetIndex = i;
                }
            }
            Check(targetIndex >= 0, "Test window listed in picker", count + " rows, index " + targetIndex);
            if (targetIndex < 0) return 4;

            RECT listRect;
            GetWindowRect(list, out listRect);
            ClickAt(listRect.Left + 90, listRect.Top + targetIndex * 48 + 24);
            Thread.Sleep(300);
            IntPtr choose = FindChild(dlg, null, "选择");
            Check(choose != IntPtr.Zero, "Picker 'choose' button found", "");
            if (choose != IntPtr.Zero)
            {
                RECT br;
                GetWindowRect(choose, out br);
                ClickAt((br.Left + br.Right) / 2, (br.Top + br.Bottom) / 2);
            }
            Thread.Sleep(600);
            Check(FindWindow(null, "选择窗口") == IntPtr.Zero, "Picker closes after choosing", "");

            ClickClient(gui, 233, 212);
            Thread.Sleep(500);
            Check(Percent(test) == 25 && Layered(test), "Picked through list, 25% applied", "pct=" + Percent(test));
            ClickClient(gui, 47, 212);
            Thread.Sleep(400);
            Check(Percent(test) == 100, "Reset to 100% before crosshair test", "pct=" + Percent(test));

            ClickClient(gui, 360, 78);
            Thread.Sleep(400);
            Console.WriteLine("Clicked crosshair picker");

            int cx = (tr.Left + tr.Right) / 2;
            int cy = tr.Top + (tr.Bottom - tr.Top) * 3 / 5;
            TransparentWindow.POINT probePoint = new TransparentWindow.POINT();
            probePoint.X = cx;
            probePoint.Y = cy;
            IntPtr under = Native.WindowFromPoint(probePoint);
            IntPtr root = under == IntPtr.Zero ? IntPtr.Zero : Native.GetAncestor(under, Native.GA_ROOT);
            Check(root == test, "Click point resolves to test window", "under=" + under.ToInt64() + " root=" + root.ToInt64() + " test=" + test.ToInt64());
            if (root != test)
            {
                Console.WriteLine("ABORT: test window is covered by another window");
                return 3;
            }

            Console.WriteLine("Clicking test window at " + cx + "," + cy);
            ClickAt(cx, cy);
            Thread.Sleep(700);

            ClickClient(gui, 171, 212);
            Thread.Sleep(500);
            Check(Percent(test) == 50 && Layered(test), "GUI 50% button applies to picked window", "pct=" + Percent(test));

            Combo(0x11, 0x12, 0x26);
            Check(Percent(test) == 55, "Hotkey Ctrl+Alt+Up raises opacity by 5", "pct=" + Percent(test));

            Combo(0x11, 0x12, 0x28);
            Check(Percent(test) == 50, "Hotkey Ctrl+Alt+Down lowers opacity by 5", "pct=" + Percent(test));

            ClickClient(gui, 233, 212);
            Thread.Sleep(500);
            Check(Percent(test) == 25, "GUI 25% button", "pct=" + Percent(test));

            ClickClient(gui, 47, 212);
            Thread.Sleep(500);
            Check(Percent(test) == 100, "GUI 100% button", "pct=" + Percent(test));

            ClickClient(gui, 200, 172);
            Thread.Sleep(200);
            KeyPress(0x24);
            Check(Percent(test) == 1 && Layered(test), "Slider Home key reaches minimum 1%", "pct=" + Percent(test));

            ClickClient(gui, 171, 212);
            Thread.Sleep(600);
            Check(Percent(test) == 50, "Back to 50% for screenshot", "pct=" + Percent(test));

            string shot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "e2e-50.png");
            try
            {
                Screenshot(shot);
                Console.WriteLine("Screenshot: " + shot);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Screenshot failed: " + ex.Message);
            }

            PostMessage(gui, 0x0010, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(2000);
            Check(FindWindow(null, "窗口透明度") == IntPtr.Zero, "GUI closed", "");
            Check(!Layered(test) && Percent(test) == 100, "Closing GUI restored original window state", "layered=" + Layered(test) + " pct=" + Percent(test));

            uint pid;
            GetWindowThreadProcessId(test, out pid);
            try
            {
                Process.GetProcessById((int)pid).Kill();
            }
            catch
            {
            }

            Console.WriteLine();
            Console.WriteLine(_fails == 0
                ? "UI RESULT: ALL " + _checks + " CHECKS PASSED"
                : "UI RESULT: " + _fails + " OF " + _checks + " CHECKS FAILED");
            return _fails == 0 ? 0 : 1;
        }
    }
}
