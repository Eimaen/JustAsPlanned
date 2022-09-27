using JustAsPlanned.Properties;
using JustAsPlanned.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JustAsPlanned.Forms
{
    public partial class FormMain : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public FormMain()
        {
            CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
            Run();
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private void Run()
        {
            Thread threadMain = new Thread(() =>
            {
                try
                {
                    
                    RegistryKey steamRegKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam");
                    if (steamRegKey == null)
                    {
                        status = "Unable to start Muse Dash. Is Steam even installed?";
                        Thread.Sleep(7500);
                        Environment.Exit(0);
                    }
                    if (!Process.GetProcessesByName("Steam").Any())
                    {
                        status = "Unable to start Muse Dash. Steam is not running.";
                        Thread.Sleep(7500);
                        Environment.Exit(0);
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(steamRegKey.GetValue("InstallPath") as string, "steam.exe"),
                        Arguments = "-applaunch 774171"
                    });
                    DateTime waitStart = DateTime.Now;
                    while (!IsDisposed)
                    {
                        TimeSpan waitRemaining = TimeSpan.FromSeconds(15) - (DateTime.Now - waitStart);
                        if (Process.GetProcessesByName("MuseDash").Any())
                        {
                            status = "Process found, applying patch...";
                            int retries = 0;
                            while (Process.GetProcessesByName("MuseDash").Any() && !MuseDash.Exploit(Process.GetProcessesByName("MuseDash")?.First()) && ++retries < 100)
                            {
                                status = $"Failed to patch, retrying... Attempt #{retries}";
                                Thread.Sleep(50);
                            }
                            if (retries == 100)
                                status = "Failed to patch Muse Dash.";
                            else
                                status = "Muse Dash patched successfully.";
                            Thread.Sleep(7500);
                            Environment.Exit(0);
                        }
                        else
                            status = $"Waiting for Muse Dash process ({Math.Round(waitRemaining.TotalSeconds)}s remaining)...";
                        if (waitRemaining.TotalSeconds <= 0)
                        {
                            status = "Failed to patch Muse Dash. Process was not found.";
                            Thread.Sleep(7500);
                            Environment.Exit(0);
                        }
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    File.WriteAllText("jap-crash.txt", ex.ToString());
                    status = $"Failed to patch Muse Dash. Ex: {ex.Message}.";
                    Thread.Sleep(7500);
                    Environment.Exit(0);
                }
            });
            threadMain.Start();
        }

        private double hueShift = 0d;
        public string status = "Initializing...";

        private void Render(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

            hueShift += 0.05d;
            hueShift %= 360;

            Rectangle drawArea = e.ClipRectangle;
            drawArea.Inflate(2, 2);
            drawArea.Offset(-1, -1);
            Color gradientBegin = ColorManager.FromHSV(hueShift, 1d, 0.3d);
            Color gradientEnd = gradientBegin.SetBrightness(0.4d);
            LinearGradientBrush gradientBrush = new LinearGradientBrush(drawArea, gradientBegin, gradientEnd, 60);
            e.Graphics.FillRectangle(gradientBrush, drawArea);

            e.Graphics.DrawCenteredImage(Resources.banner, new Point(e.ClipRectangle.Width / 2, (int)(e.ClipRectangle.Height * 0.35)), 0.8f);

            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            Font font = new Font("Arial", 14);
            Brush textBrush = new SolidBrush(Color.White);
            StringFormat format = new StringFormat { Alignment = StringAlignment.Center };

            e.Graphics.DrawString(status, font, textBrush, new Point(e.ClipRectangle.Width / 2, (int)(e.ClipRectangle.Height * 0.8)), format);

            Invalidate();
        }
    }
}
