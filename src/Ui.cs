using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TransparentWindow
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(22, 24, 30);
        public static readonly Color Card = Color.FromArgb(33, 36, 46);
        public static readonly Color CardHover = Color.FromArgb(43, 47, 59);
        public static readonly Color Line = Color.FromArgb(45, 49, 61);
        public static readonly Color Text = Color.FromArgb(236, 238, 245);
        public static readonly Color Muted = Color.FromArgb(143, 149, 167);
        public static readonly Color Accent = Color.FromArgb(84, 110, 255);
        public static readonly Color AccentHover = Color.FromArgb(104, 128, 255);
        public static readonly Color AccentSoft = Color.FromArgb(52, 58, 92);
        public static readonly Color Danger = Color.FromArgb(255, 122, 122);
        public static readonly Color Good = Color.FromArgb(110, 220, 170);
        public static readonly Color CloseHover = Color.FromArgb(196, 64, 64);
        public static readonly Color White = Color.White;
    }

    internal static class Ui
    {
        public static float Scale = 1f;
        public static string Family = "Segoe UI";

        public static Font Title;
        public static Font Body;
        public static Font BodyBold;
        public static Font BodyLarge;
        public static Font Small;
        public static Font Tiny;
        public static Font Big;
        public static Font DialogTitle;
        public static Font ButtonFont;

        public const TextFormatFlags LeftMid =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        public const TextFormatFlags LeftTop =
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        public const TextFormatFlags CenterMid =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix;

        public const TextFormatFlags RightMid =
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix;

        public static int S(int value)
        {
            return (int)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
        }

        public static int S(float value)
        {
            return (int)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
        }

        public static void Init(float scale)
        {
            Scale = Math.Max(0.75f, Math.Min(3.0f, scale));
            Family = PickFamily();
            Title = new Font(Family, 10.5f, FontStyle.Bold, GraphicsUnit.Point);
            Body = new Font(Family, 9f, FontStyle.Regular, GraphicsUnit.Point);
            BodyBold = new Font(Family, 9f, FontStyle.Bold, GraphicsUnit.Point);
            BodyLarge = new Font(Family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            Small = new Font(Family, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
            Tiny = new Font(Family, 7.75f, FontStyle.Regular, GraphicsUnit.Point);
            Big = new Font(Family, 25f, FontStyle.Bold, GraphicsUnit.Point);
            DialogTitle = new Font(Family, 11f, FontStyle.Bold, GraphicsUnit.Point);
            ButtonFont = new Font(Family, 9f, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static string PickFamily()
        {
            try
            {
                foreach (FontFamily f in FontFamily.Families)
                {
                    if (string.Equals(f.Name, "Microsoft YaHei UI", StringComparison.OrdinalIgnoreCase))
                    {
                        return f.Name;
                    }
                }
                foreach (FontFamily f in FontFamily.Families)
                {
                    if (string.Equals(f.Name, "Microsoft YaHei", StringComparison.OrdinalIgnoreCase))
                    {
                        return f.Name;
                    }
                }
            }
            catch
            {
            }
            return "Segoe UI";
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d >= r.Width) d = r.Width;
            if (d >= r.Height) d = r.Height;
            if (d <= 2)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath path = RoundRect(r, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            Rectangle rr = new Rectangle(r.X, r.Y, Math.Max(1, r.Width - 1), Math.Max(1, r.Height - 1));
            using (GraphicsPath path = RoundRect(rr, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }
    }
}
