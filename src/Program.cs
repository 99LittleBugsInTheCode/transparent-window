using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TransparentWindow
{
    internal static class Program
    {
        private static Mutex _instanceMutex;

        [STAThread]
        private static void Main()
        {
            bool created = true;
            try
            {
                _instanceMutex = new Mutex(true, "Local\\TransparentWindow.SingleInstance", out created);
            }
            catch
            {
                created = false;
            }
            if (!created)
            {
                IntPtr existing = Native.FindWindow(null, "窗口透明度");
                if (existing != IntPtr.Zero)
                {
                    Native.ShowWindow(existing, 9);
                    Native.SetForegroundWindow(existing);
                }
                return;
            }

            Native.SetProcessDPIAware();

            float scale = 1f;
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    scale = g.DpiX / 96f;
                }
            }
            catch
            {
                scale = 1f;
            }

            Ui.Init(scale);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            GC.KeepAlive(_instanceMutex);
        }
    }
}
