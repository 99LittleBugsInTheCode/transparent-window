using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TwTest
{
    internal sealed class TestWindow : Form
    {
        public TestWindow()
        {
            Text = "TW-TestWindow";
            StartPosition = FormStartPosition.Manual;
            Location = new Point(240, 180);
            ClientSize = new Size(520, 320);
            MinimumSize = new Size(300, 200);
            BackColor = Color.FromArgb(20, 40, 70);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = ClientRectangle;
            if (r.Width <= 1 || r.Height <= 1) return;
            using (LinearGradientBrush b = new LinearGradientBrush(r,
                Color.FromArgb(0, 122, 255), Color.FromArgb(255, 70, 130), 35f))
            {
                g.FillRectangle(b, r);
            }
            using (Font f = new Font("Microsoft YaHei UI", 22f, FontStyle.Bold))
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.DrawString("透明测试窗口", f, white, 28, 26);
            }
            using (Font f = new Font("Microsoft YaHei UI", 12f))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                g.DrawString("这个窗口应能看到后面的桌面", f, white, 30, 92);
            }
            using (Pen pen = new Pen(Color.FromArgb(210, 255, 255, 255), 2f))
            {
                g.DrawRectangle(pen, 24, 130, r.Width - 48, r.Height - 154);
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TestWindow form = new TestWindow();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--topmost") form.TopMost = true;
                else if (args[i] == "--x" && i + 1 < args.Length) form.Left = int.Parse(args[i + 1]);
                else if (args[i] == "--y" && i + 1 < args.Length) form.Top = int.Parse(args[i + 1]);
                else if (args[i] == "--title" && i + 1 < args.Length) form.Text = args[i + 1];
                else if (args[i] == "--width" && i + 1 < args.Length) form.Width = int.Parse(args[i + 1]);
                else if (args[i] == "--height" && i + 1 < args.Length) form.Height = int.Parse(args[i + 1]);
            }
            Application.Run(form);
        }
    }
}
