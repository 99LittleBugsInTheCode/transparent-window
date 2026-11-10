using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TransparentWindow
{
    internal sealed class WindowPickerForm : Form
    {
        private const int BaseW = 380;
        private const int BaseH = 460;

        private ListBox _list;
        private Label _empty;
        private Button _select;
        private Button _cancel;
        private Button _refresh;
        private Rectangle _closeR;
        private bool _closeHot;
        private bool _closePressed;

        public WindowPickerForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            Text = "选择窗口";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;

            ClientSize = new Size(Ui.S(BaseW), Ui.S(BaseH));
            MinimumSize = Size;
            MaximumSize = Size;

            _closeR = new Rectangle(ClientSize.Width - Ui.S(38), Ui.S(8), Ui.S(28), Ui.S(26));

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Theme.Card;
            _list.ForeColor = Theme.Text;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Ui.S(48);
            _list.IntegralHeight = false;
            _list.Font = Ui.Body;
            _list.Location = new Point(Ui.S(16), Ui.S(48));
            _list.Size = new Size(ClientSize.Width - Ui.S(32), ClientSize.Height - Ui.S(48) - Ui.S(60));
            _list.DrawItem += ListDrawItem;
            _list.SelectedIndexChanged += delegate { UpdateButtons(); };
            _list.DoubleClick += delegate { Accept(); };
            Controls.Add(_list);

            _empty = new Label();
            _empty.Text = "没有找到可调节的窗口";
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.BackColor = Theme.Card;
            _empty.ForeColor = Theme.Muted;
            _empty.Font = Ui.Body;
            _empty.Bounds = _list.Bounds;
            _empty.Visible = false;
            Controls.Add(_empty);
            _empty.BringToFront();

            _refresh = MakeButton("刷新", Theme.Card, Theme.Text, 0);
            _refresh.Bounds = new Rectangle(ClientSize.Width - Ui.S(38) - Ui.S(8) - Ui.S(64), Ui.S(8), Ui.S(64), Ui.S(26));
            _refresh.Click += delegate { RefreshList(); };
            Controls.Add(_refresh);

            int by = ClientSize.Height - Ui.S(16) - Ui.S(32);
            _select = MakeButton("选择", Theme.Accent, Theme.White, 0);
            _select.Bounds = new Rectangle(ClientSize.Width - Ui.S(16) - Ui.S(84), by, Ui.S(84), Ui.S(32));
            _select.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            _select.Click += delegate { Accept(); };
            Controls.Add(_select);

            _cancel = MakeButton("取消", Theme.Card, Theme.Text, 0);
            _cancel.Bounds = new Rectangle(_select.Left - Ui.S(8) - Ui.S(72), by, Ui.S(72), Ui.S(32));
            _cancel.Click += delegate { CloseWithCancel(); };
            Controls.Add(_cancel);

            Load += delegate
            {
                if (Owner != null)
                {
                    Rectangle wa = Screen.FromControl(Owner).WorkingArea;
                    int x = Owner.Left + (Owner.Width - Width) / 2;
                    int y = Owner.Top + (Owner.Height - Height) / 2;
                    if (x + Width > wa.Right) x = wa.Right - Width - Ui.S(12);
                    if (y + Height > wa.Bottom) y = wa.Bottom - Height - Ui.S(12);
                    if (x < wa.Left) x = wa.Left + Ui.S(12);
                    if (y < wa.Top) y = wa.Top + Ui.S(12);
                    Location = new Point(x, y);
                }
                RefreshList();
            };
        }

        private Button MakeButton(string text, Color back, Color fore, int pad)
        {
            Button b = new Button();
            b.Text = text;
            b.Font = Ui.Body;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseDownBackColor = back;
            b.FlatAppearance.MouseOverBackColor = Theme.CardHover;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            return b;
        }

        public WindowInfo SelectedWindow
        {
            get { return _list.SelectedItem as WindowInfo; }
        }

        private void RefreshList()
        {
            List<WindowInfo> windows = OpacityService.EnumerateWindows();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (WindowInfo info in windows)
            {
                _list.Items.Add(info);
            }
            _list.EndUpdate();
            _empty.Visible = windows.Count == 0;
            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool has = _list.SelectedItem != null;
            _select.Enabled = has;
            _select.BackColor = has ? Theme.Accent : Theme.Card;
            _select.ForeColor = has ? Theme.White : Theme.Muted;
        }

        private void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            WindowInfo info = _list.Items[e.Index] as WindowInfo;
            if (info == null) return;

            Graphics g = e.Graphics;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle r = e.Bounds;

            using (SolidBrush back = new SolidBrush(selected ? Theme.AccentSoft : Theme.Card))
            {
                g.FillRectangle(back, r);
            }
            if (selected)
            {
                using (SolidBrush bar = new SolidBrush(Theme.Accent))
                {
                    g.FillRectangle(bar, new Rectangle(r.X, r.Y, Ui.S(3), r.Height));
                }
            }

            Ui.DrawText(g, info.Title, Ui.Body, selected ? Theme.Text : Theme.Text,
                new Rectangle(r.X + Ui.S(12), r.Y + Ui.S(5), r.Width - Ui.S(24), Ui.S(21)), Ui.LeftMid);

            string sub = info.ProcessName;
            if (sub.Length > 0) sub = sub + ".exe";
            if (info.IsLayered)
            {
                if (sub.Length > 0) sub = sub + "  ·  已有透明效果";
                else sub = "已有透明效果";
            }
            if (sub.Length > 0)
            {
                Ui.DrawText(g, sub, Ui.Tiny, Theme.Muted,
                    new Rectangle(r.X + Ui.S(12), r.Y + Ui.S(26), r.Width - Ui.S(24), Ui.S(16)), Ui.LeftMid);
            }

            using (Pen pen = new Pen(Theme.Line))
            {
                g.DrawLine(pen, r.Left + Ui.S(12), r.Bottom - 1, r.Right - Ui.S(12), r.Bottom - 1);
            }
        }

        private void Accept()
        {
            if (SelectedWindow == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CloseWithCancel()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CloseWithCancel();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                Accept();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            Ui.DrawText(g, "选择窗口", Ui.DialogTitle, Theme.Text,
                new Rectangle(Ui.S(18), Ui.S(10), w - Ui.S(160), Ui.S(24)), Ui.LeftMid);

            if (_closeHot)
            {
                Ui.FillRound(g, _closeR, Ui.S(7), Theme.CloseHover);
            }
            Color closeColor = _closeHot ? Theme.White : Theme.Muted;
            using (Pen pen = new Pen(closeColor, Math.Max(1f, Ui.S(1))))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                int pad = Ui.S(9);
                g.DrawLine(pen, _closeR.X + pad, _closeR.Y + pad, _closeR.Right - pad, _closeR.Bottom - pad);
                g.DrawLine(pen, _closeR.Right - pad, _closeR.Y + pad, _closeR.X + pad, _closeR.Bottom - pad);
            }

            Ui.DrawText(g, "双击列表项快速选择", Ui.Tiny, Theme.Muted,
                new Rectangle(Ui.S(16), h - Ui.S(52), w - Ui.S(220), Ui.S(32)), Ui.LeftMid);

            Ui.StrokeRound(g, new Rectangle(Ui.S(1), Ui.S(1), w - Ui.S(2), h - Ui.S(2)), Ui.S(12), Theme.Line, 1f);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hot = _closeR.Contains(e.Location);
            if (hot != _closeHot)
            {
                _closeHot = hot;
                Invalidate();
            }
            if (hot) Cursor = Cursors.Hand;
            else if (e.Y < Ui.S(44)) Cursor = Cursors.SizeAll;
            else Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_closeR.Contains(e.Location))
            {
                _closePressed = true;
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
            if (_closePressed)
            {
                _closePressed = false;
                if (_closeR.Contains(e.Location)) CloseWithCancel();
            }
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
        }
    }
}
