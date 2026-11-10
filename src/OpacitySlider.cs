using System;
using System.Drawing;
using System.Windows.Forms;

namespace TransparentWindow
{
    /// <summary>自绘的不透明度滑块（1 - 100）。</summary>
    internal sealed class OpacitySlider : Control
    {
        private int _min = 1;
        private int _max = 100;
        private int _value = 100;
        private bool _dragging;
        private bool _hover;

        public event EventHandler ValueChanged;

        public OpacitySlider()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Bg;
            TabStop = true;
            Height = Ui.S(28);
            Cursor = Cursors.Hand;
        }

        public int Minimum
        {
            get { return _min; }
            set
            {
                _min = value;
                if (_value < _min) _value = _min;
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return _max; }
            set
            {
                _max = value;
                if (_value > _max) _value = _max;
                Invalidate();
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = value;
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                if (v == _value) return;
                _value = v;
                Invalidate();
                RaiseValueChanged();
            }
        }

        private void RaiseValueChanged()
        {
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private Rectangle TrackBounds
        {
            get
            {
                int h = Ui.S(6);
                int knob = Ui.S(16);
                return new Rectangle(knob / 2, (Height - h) / 2, Width - knob, h);
            }
        }

        private int KnobCenterX
        {
            get
            {
                Rectangle t = TrackBounds;
                double ratio = (double)(_value - _min) / (double)(_max - _min);
                return t.Left + (int)Math.Round(ratio * t.Width);
            }
        }

        private int ValueFromX(int x)
        {
            Rectangle t = TrackBounds;
            if (t.Width <= 0) return _min;
            double ratio = (double)(x - t.Left) / (double)t.Width;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            int v = _min + (int)Math.Round(ratio * (_max - _min));
            if (v < _min) v = _min;
            if (v > _max) v = _max;
            return v;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush back = new SolidBrush(BackColor))
            {
                g.FillRectangle(back, ClientRectangle);
            }

            bool enabled = Enabled;
            Color trackColor = enabled ? Theme.Card : Color.FromArgb(31, 34, 42);
            Color fillColor = enabled ? Theme.Accent : Color.FromArgb(64, 68, 82);
            Color knobColor = enabled ? Theme.White : Color.FromArgb(120, 126, 142);

            Rectangle track = TrackBounds;
            Ui.FillRound(g, track, track.Height / 2, trackColor);

            int cx = KnobCenterX;
            int fillWidth = cx - track.Left;
            if (fillWidth > 0)
            {
                Ui.FillRound(g, new Rectangle(track.Left, track.Top, fillWidth, track.Height), track.Height / 2, fillColor);
            }

            int knob = Ui.S(16);
            Rectangle knobRect = new Rectangle(cx - knob / 2, (Height - knob) / 2, knob, knob);
            if (enabled && (_hover || _dragging || Focused))
            {
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(70, Theme.Accent)))
                {
                    g.FillEllipse(halo, knobRect.X - Ui.S(4), knobRect.Y - Ui.S(4), knobRect.Width + Ui.S(8), knobRect.Height + Ui.S(8));
                }
            }
            using (SolidBrush kb = new SolidBrush(knobColor))
            {
                g.FillEllipse(kb, knobRect);
            }
            using (Pen pen = new Pen(enabled ? Theme.Accent : Theme.Line, Math.Max(1f, Ui.S(2))))
            {
                g.DrawEllipse(pen, knobRect);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            _dragging = true;
            Capture = true;
            Value = ValueFromX(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Value = ValueFromX(e.X);
            }
            else
            {
                bool hover = e.X >= 0 && e.X <= Width && e.Y >= 0 && e.Y <= Height;
                if (hover != _hover)
                {
                    _hover = hover;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                Value = ValueFromX(e.X);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_dragging && _hover)
            {
                _hover = false;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Enabled) return;
            int delta = e.Delta > 0 ? 5 : -5;
            Value = _value + delta;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right ||
                keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.Home || keyData == Keys.End ||
                keyData == Keys.PageUp || keyData == Keys.PageDown)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down:
                    Value = _value - (e.Shift ? 5 : 1);
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Up:
                    Value = _value + (e.Shift ? 5 : 1);
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    Value = _value - 10;
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    Value = _value + 10;
                    e.Handled = true;
                    break;
                case Keys.Home:
                    Value = _min;
                    e.Handled = true;
                    break;
                case Keys.End:
                    Value = _max;
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
    }
}
