using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TransparentWindow
{
    internal sealed class MainForm : Form
    {
        private const int HotNone = 0;
        private const int HotCard = 1;
        private const int HotPick = 2;
        private const int HotClose = 3;
        private const int HotMin = 4;
        private const int HotQuickBase = 10;
        private const int HotRestoreAll = 20;

        private const int BaseW = 404;
        private const int BaseH = 300;

        private static readonly int[] QuickValues = new int[] { 100, 75, 50, 25 };

        private IntPtr _target = IntPtr.Zero;
        private string _targetTitle = string.Empty;
        private string _targetProc = string.Empty;
        private int _percent = 100;
        private bool _syncing;

        private int _hot = HotNone;
        private int _pressed = HotNone;
        private bool _picking;
        private bool _leftWasDown;
        private bool _rightWasDown;

        private Timer _watchTimer;
        private Timer _pickTimer;
        private NotifyIcon _tray;
        private OpacitySlider _slider;

        private Rectangle _closeR;
        private Rectangle _minR;
        private Rectangle _cardR;
        private Rectangle _pickR;
        private Rectangle _restoreAllR;
        private Rectangle[] _quickR = new Rectangle[4];

        private string _status = "拖动滑块实时调整，关闭本工具会自动还原";
        private Color _statusColor = Theme.Muted;

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            Text = "窗口透明度";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            TopMost = true;
            KeyPreview = true;

            ClientSize = new Size(Ui.S(BaseW), Ui.S(BaseH));
            MinimumSize = Size;
            MaximumSize = Size;

            try
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _slider = new OpacitySlider();
            _slider.Minimum = 1;
            _slider.Maximum = 100;
            _slider.Value = 100;
            _slider.Enabled = false;
            _slider.ValueChanged += SliderValueChanged;
            Controls.Add(_slider);

            LayoutUi();

            _watchTimer = new Timer();
            _watchTimer.Interval = 800;
            _watchTimer.Tick += WatchTick;
            _watchTimer.Start();

            _pickTimer = new Timer();
            _pickTimer.Interval = 30;
            _pickTimer.Tick += PickTick;

            SetupTray();

            Load += OnLoaded;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = wa.Right - Width - Ui.S(24);
            int y = wa.Top + Ui.S(24);
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            Location = new Point(x, y);
        }

        private void LayoutUi()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            _closeR = new Rectangle(w - Ui.S(38), Ui.S(8), Ui.S(28), Ui.S(26));
            _minR = new Rectangle(w - Ui.S(70), Ui.S(8), Ui.S(28), Ui.S(26));
            _cardR = new Rectangle(Ui.S(16), Ui.S(46), w - Ui.S(32), Ui.S(64));
            _pickR = new Rectangle(_cardR.Right - Ui.S(44), _cardR.Top + Ui.S(16), Ui.S(32), Ui.S(32));

            int qy = Ui.S(198);
            int qx = Ui.S(20);
            int qw = Ui.S(54);
            int gap = Ui.S(8);
            for (int i = 0; i < 4; i++)
            {
                _quickR[i] = new Rectangle(qx + i * (qw + gap), qy, qw, Ui.S(28));
            }
            _restoreAllR = new Rectangle(w - Ui.S(20) - Ui.S(72), qy, Ui.S(72), Ui.S(28));

            _slider.Bounds = new Rectangle(Ui.S(20), Ui.S(158), w - Ui.S(40), Ui.S(28));
        }

        private void SetupTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = Icon != null ? Icon : SystemIcons.Application;
            _tray.Text = "窗口透明度";
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Ui.Body;
            ToolStripMenuItem show = new ToolStripMenuItem("显示主界面");
            show.Click += delegate { RestoreFromTray(); };
            ToolStripMenuItem restore = new ToolStripMenuItem("还原全部窗口");
            restore.Click += delegate { DoRestoreAll(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { Close(); };
            menu.Items.Add(show);
            menu.Items.Add(restore);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { RestoreFromTray(); };
            _tray.Visible = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int pref = Native.DWMWCP_ROUND;
                Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            }
            catch
            {
            }

            uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
            Native.RegisterHotKey(Handle, 1, mods, Native.VK_UP);
            Native.RegisterHotKey(Handle, 2, mods, Native.VK_DOWN);
            Native.RegisterHotKey(Handle, 3, mods, Native.VK_0);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, 1);
            Native.UnregisterHotKey(Handle, 2);
            Native.UnregisterHotKey(Handle, 3);
            base.OnHandleDestroyed(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            Ui.FillRound(g, new Rectangle(Ui.S(18), Ui.S(13), Ui.S(4), Ui.S(16)), Ui.S(2), Theme.Accent);
            Ui.DrawText(g, "窗口透明度", Ui.Title, Theme.Text,
                new Rectangle(Ui.S(30), Ui.S(9), w - Ui.S(120), Ui.S(24)), Ui.LeftMid);

            DrawChrome(g, _minR, _hot == HotMin, false);
            DrawChrome(g, _closeR, _hot == HotClose, true);

            Color cardColor = _hot == HotCard ? Theme.CardHover : Theme.Card;
            Ui.FillRound(g, _cardR, Ui.S(10), cardColor);

            Ui.DrawText(g, "目标窗口", Ui.Tiny, Theme.Muted,
                new Rectangle(_cardR.X + Ui.S(14), _cardR.Y + Ui.S(7), _cardR.Width - Ui.S(70), Ui.S(14)), Ui.LeftMid);

            bool hasTarget = _target != IntPtr.Zero;
            string title = hasTarget ? _targetTitle : "未选择，点击选择窗口";
            Color titleColor = hasTarget ? Theme.Text : Theme.Muted;
            Ui.DrawText(g, title, Ui.BodyLarge, titleColor,
                new Rectangle(_cardR.X + Ui.S(14), _cardR.Y + Ui.S(23), _cardR.Width - Ui.S(62), Ui.S(22)), Ui.LeftMid);

            if (hasTarget && _targetProc.Length > 0)
            {
                Ui.DrawText(g, _targetProc, Ui.Tiny, Theme.Muted,
                    new Rectangle(_cardR.X + Ui.S(14), _cardR.Y + Ui.S(44), _cardR.Width - Ui.S(62), Ui.S(14)), Ui.LeftMid);
            }

            if (_hot == HotPick)
            {
                Ui.FillRound(g, _pickR, Ui.S(8), Theme.AccentSoft);
            }
            DrawCrosshair(g, _pickR, _hot == HotPick ? Theme.Accent : Theme.Muted);

            string percentText = hasTarget ? (_percent.ToString() + "%") : "—";
            Ui.DrawText(g, percentText, Ui.Big, hasTarget ? Theme.Text : Theme.Muted,
                new Rectangle(Ui.S(20), Ui.S(110), Ui.S(170), Ui.S(44)), Ui.LeftMid);
            Ui.DrawText(g, "不透明度 · 1% – 100%", Ui.Tiny, Theme.Muted,
                new Rectangle(w - Ui.S(20) - Ui.S(170), Ui.S(128), Ui.S(170), Ui.S(16)), Ui.RightMid);

            for (int i = 0; i < 4; i++)
            {
                bool active = hasTarget && _percent == QuickValues[i];
                bool hover = _hot == HotQuickBase + i;
                Color bg = active ? Theme.Accent : (hover ? Theme.CardHover : Theme.Card);
                Color fg = active ? Theme.White : (hasTarget ? Theme.Text : Theme.Muted);
                Ui.FillRound(g, _quickR[i], Ui.S(8), bg);
                Ui.DrawText(g, QuickValues[i].ToString() + "%", Ui.Small, fg, _quickR[i], Ui.CenterMid);
            }

            bool raHover = _hot == HotRestoreAll;
            Ui.FillRound(g, _restoreAllR, Ui.S(8), raHover ? Theme.CardHover : Theme.Card);
            Ui.DrawText(g, "还原全部", Ui.Small, raHover ? Theme.Text : Theme.Muted, _restoreAllR, Ui.CenterMid);

            Ui.DrawText(g, _status, Ui.Small, _statusColor,
                new Rectangle(Ui.S(20), Ui.S(234), w - Ui.S(40), Ui.S(30)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            Ui.DrawText(g, "Ctrl+Alt+↑/↓ 微调 · Ctrl+Alt+0 还原 · 退出时自动恢复", Ui.Tiny, Theme.Muted,
                new Rectangle(Ui.S(20), Ui.S(272), w - Ui.S(40), Ui.S(16)), Ui.LeftMid);

            Ui.StrokeRound(g, new Rectangle(Ui.S(1), Ui.S(1), w - Ui.S(2), h - Ui.S(2)), Ui.S(12), Theme.Line, 1f);
        }

        private void DrawChrome(Graphics g, Rectangle r, bool hot, bool close)
        {
            if (hot)
            {
                Ui.FillRound(g, r, Ui.S(7), close ? Theme.CloseHover : Theme.CardHover);
            }
            Color color = hot ? Theme.White : Theme.Muted;
            float width = Math.Max(1f, Ui.S(1));
            using (Pen pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int pad = Ui.S(9);
                if (close)
                {
                    g.DrawLine(pen, r.X + pad, r.Y + pad, r.Right - pad, r.Bottom - pad);
                    g.DrawLine(pen, r.Right - pad, r.Y + pad, r.X + pad, r.Bottom - pad);
                }
                else
                {
                    int y = r.Y + r.Height / 2;
                    g.DrawLine(pen, r.X + pad, y, r.Right - pad, y);
                }
            }
        }

        private void DrawCrosshair(Graphics g, Rectangle r, Color color)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            int rad = Ui.S(6);
            int arm = Ui.S(3);
            using (Pen pen = new Pen(color, Math.Max(1f, Ui.S(1))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawLine(pen, cx, cy - rad - arm, cx, cy - rad);
                g.DrawLine(pen, cx, cy + rad, cx, cy + rad + arm);
                g.DrawLine(pen, cx - rad - arm, cy, cx - rad, cy);
                g.DrawLine(pen, cx + rad, cy, cx + rad + arm, cy);
            }
        }

        private int HitTest(Point p)
        {
            if (_closeR.Contains(p)) return HotClose;
            if (_minR.Contains(p)) return HotMin;
            if (_pickR.Contains(p)) return HotPick;
            if (_cardR.Contains(p)) return HotCard;
            for (int i = 0; i < 4; i++)
            {
                if (_quickR[i].Contains(p)) return HotQuickBase + i;
            }
            if (_restoreAllR.Contains(p)) return HotRestoreAll;
            return HotNone;
        }

        private static bool IsHandCursor(int hot)
        {
            return hot == HotCard || hot == HotPick || hot == HotClose || hot == HotMin ||
                   hot == HotRestoreAll || (hot >= HotQuickBase && hot < HotQuickBase + 4);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = HitTest(e.Location);
            if (hot != _hot)
            {
                _hot = hot;
                Invalidate();
            }
            if (IsHandCursor(hot))
            {
                Cursor = Cursors.Hand;
            }
            else if (e.Y < Ui.S(44))
            {
                Cursor = Cursors.SizeAll;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != HotNone)
            {
                _hot = HotNone;
                Invalidate();
            }
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_picking)
            {
                CancelPick("已取消选择");
                return;
            }
            int hot = HitTest(e.Location);
            if (hot != HotNone)
            {
                _pressed = hot;
                Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Left && e.Y < Ui.S(44))
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, new IntPtr(Native.HTCAPTION), IntPtr.Zero);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_picking) return;
            int hot = HitTest(e.Location);
            int pressed = _pressed;
            _pressed = HotNone;
            if (pressed != HotNone && hot == pressed)
            {
                ActivateHot(pressed);
            }
            Invalidate();
        }

        private void ActivateHot(int hot)
        {
            switch (hot)
            {
                case HotClose:
                    Close();
                    break;
                case HotMin:
                    HideToTray();
                    break;
                case HotCard:
                    ShowPicker();
                    break;
                case HotPick:
                    StartPick();
                    break;
                case HotRestoreAll:
                    DoRestoreAll();
                    break;
                default:
                    if (hot >= HotQuickBase && hot < HotQuickBase + 4)
                    {
                        _slider.Value = QuickValues[hot - HotQuickBase];
                    }
                    break;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape && _picking)
            {
                CancelPick("已取消选择");
            }
        }

        private void SliderValueChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            ApplyPercent(_slider.Value);
        }

        private void ApplyPercent(int percent)
        {
            if (percent < 1) percent = 1;
            if (percent > 100) percent = 100;
            _percent = percent;

            if (_target == IntPtr.Zero)
            {
                SetStatus("请先点击上方卡片选择目标窗口", Theme.Accent);
                Invalidate();
                return;
            }
            if (!Native.IsWindow(_target))
            {
                ClearTarget("目标窗口已关闭");
                return;
            }

            string error;
            if (OpacityService.Apply(_target, percent, out error))
            {
                if (percent >= 100)
                {
                    SetStatus("已恢复为完全不透明", Theme.Muted);
                }
                else
                {
                    SetStatus("当前透明度 " + percent + "%（拖动滑块可继续调整）", Theme.Muted);
                }
            }
            else
            {
                SetStatus(error, Theme.Danger);
            }
            Invalidate();
        }

        private void SetTarget(IntPtr hwnd)
        {
            _target = hwnd;
            _targetTitle = Native.GetTitle(hwnd);
            if (_targetTitle.Length == 0) _targetTitle = "(无标题窗口)";
            _targetProc = OpacityService.GetProcessName(hwnd);
            if (_targetProc.Length > 0) _targetProc = _targetProc + ".exe";

            _percent = OpacityService.GetCurrentPercent(hwnd);
            _syncing = true;
            _slider.Value = _percent;
            _syncing = false;
            _slider.Enabled = true;

            SetStatus("已选择目标窗口，拖动滑块即可实时调整", Theme.Muted);
            Invalidate();
        }

        private void ClearTarget(string message)
        {
            if (_target != IntPtr.Zero)
            {
                OpacityService.Restore(_target);
            }
            _target = IntPtr.Zero;
            _targetTitle = string.Empty;
            _targetProc = string.Empty;
            _percent = 100;
            _syncing = true;
            _slider.Value = 100;
            _syncing = false;
            _slider.Enabled = false;
            SetStatus(message, Theme.Muted);
            Invalidate();
        }

        private void SetStatus(string text, Color color)
        {
            _status = text;
            _statusColor = color;
        }

        private void WatchTick(object sender, EventArgs e)
        {
            if (_target != IntPtr.Zero && !Native.IsWindow(_target))
            {
                ClearTarget("目标窗口已关闭，请重新选择");
            }
        }

        private void ShowPicker()
        {
            using (WindowPickerForm dlg = new WindowPickerForm())
            {
                dlg.TopMost = true;
                DialogResult result = dlg.ShowDialog(this);
                if (result == DialogResult.OK && dlg.SelectedWindow != null)
                {
                    SetTarget(dlg.SelectedWindow.Handle);
                }
            }
        }

        private void StartPick()
        {
            if (_picking) return;
            _picking = true;
            _leftWasDown = Native.IsKeyDown(Native.VK_LBUTTON);
            _rightWasDown = Native.IsKeyDown(Native.VK_RBUTTON);
            _pickTimer.Start();
            SetStatus("请点击要调节透明度的窗口（Esc 取消）", Theme.Accent);
            Invalidate();
        }

        private void CancelPick(string message)
        {
            if (!_picking) return;
            _picking = false;
            _pickTimer.Stop();
            SetStatus(message, Theme.Muted);
            Invalidate();
        }

        private void PickTick(object sender, EventArgs e)
        {
            if (!_picking)
            {
                _pickTimer.Stop();
                return;
            }

            bool esc = Native.IsKeyDown(Native.VK_ESCAPE);
            bool right = Native.IsKeyDown(Native.VK_RBUTTON);
            if (esc || (right && !_rightWasDown))
            {
                _rightWasDown = right;
                CancelPick("已取消选择");
                return;
            }
            _rightWasDown = right;

            bool left = Native.IsKeyDown(Native.VK_LBUTTON);
            bool rising = left && !_leftWasDown;
            _leftWasDown = left;
            if (!rising) return;

            POINT pt;
            if (!Native.GetCursorPos(out pt)) return;
            IntPtr hit = Native.WindowFromPoint(pt);
            if (hit == IntPtr.Zero) return;
            IntPtr root = Native.GetAncestor(hit, Native.GA_ROOT);
            if (root == IntPtr.Zero) return;

            if (root == Handle)
            {
                CancelPick("已取消选择");
                return;
            }

            uint pid;
            Native.GetWindowThreadProcessId(root, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id)
            {
                CancelPick("已取消选择");
                return;
            }

            string title = Native.GetTitle(root);
            if (title.Length == 0)
            {
                CancelPick("这个窗口没有标题，请在列表中选择");
                return;
            }

            _picking = false;
            _pickTimer.Stop();
            SetTarget(root);
        }

        private void DoRestoreAll()
        {
            OpacityService.RestoreAll();
            if (_target != IntPtr.Zero && Native.IsWindow(_target))
            {
                _percent = OpacityService.GetCurrentPercent(_target);
                _syncing = true;
                _slider.Value = _percent;
                _syncing = false;
            }
            SetStatus("已还原所有被本工具调整过的窗口", Theme.Good);
            Invalidate();
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (_target != IntPtr.Zero && _slider.Enabled)
                {
                    if (id == 1) _slider.Value = _percent + 5;
                    else if (id == 2) _slider.Value = _percent - 5;
                    else if (id == 3) _slider.Value = 100;
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            OpacityService.RestoreAll();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_watchTimer != null) _watchTimer.Dispose();
            if (_pickTimer != null) _pickTimer.Dispose();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            base.OnFormClosed(e);
        }
    }
}
