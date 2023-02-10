using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace JustAsPlanned
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread updateProgressThread;

        double displayProgress = 0;
        double actualProgress = 0;

        void UpdateProgress()
        {
            while (true)
            {
                if (displayProgress < actualProgress)
                {
                    displayProgress += 0.7;
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => bProgress.Width = (int)(displayProgress * Width / 100)));
                }
                Thread.Sleep(1);
            }
        }

        void UpdateStatus(string status) => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => lblStatus.Content = status));

        void DisplayCriticalFailture() => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
        {
            ColorAnimation ca = new ColorAnimation(Color.FromRgb(110, 5, 5), new Duration(TimeSpan.FromSeconds(2)));
            bMain.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        void DisplaySuccess() => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
        {
            ColorAnimation ca = new ColorAnimation(Color.FromRgb(5, 110, 5), new Duration(TimeSpan.FromSeconds(2)));
            bMain.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        private void Run()
        {
            Thread threadMain = new Thread(() =>
            {
                try
                {
                    RegistryKey steamRegKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam");
                    actualProgress = 10;
                    UpdateStatus("Locating Steam installation path...");
                    if (steamRegKey == null)
                    {
                        DisplayCriticalFailture();
                        UpdateStatus("Unable to start Muse Dash. Is Steam even installed?");
                        Thread.Sleep(5000);
                        Environment.Exit(0);
                    }
                    object steamInstallationPath = steamRegKey.GetValue("InstallPath");
                    actualProgress = 15;
                    UpdateStatus("Checking for Steam process...");
                    if (!Process.GetProcessesByName("Steam").Any())
                    {
                        DisplayCriticalFailture();
                        UpdateStatus("Unable to start Muse Dash. Steam is not running.");
                        Thread.Sleep(5000);
                        Environment.Exit(0);
                    }
                    if (steamInstallationPath == null)
                        steamInstallationPath = Process.GetProcessesByName("Steam").First().MainModule.FileName;
                    else
                        steamInstallationPath = System.IO.Path.Combine(steamInstallationPath.ToString(), "steam.exe");
                    actualProgress = 20;
                    UpdateStatus("Starting Muse Dash via Steam...");
                    if (File.Exists(steamInstallationPath as string))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = steamInstallationPath as string,
                            Arguments = "-applaunch 774171"
                        });
                    }
                    else
                    {
                        DisplayCriticalFailture();
                        UpdateStatus("How? Steam is running and doesn't exist?! Contact devs NOW.");
                        Thread.Sleep(5000);
                        Environment.Exit(0);
                    }
                    DateTime waitStart = DateTime.Now;
                    while (true)
                    {
                        TimeSpan waitRemaining = TimeSpan.FromSeconds(15) - (DateTime.Now - waitStart);
                        if (Process.GetProcessesByName("MuseDash").Any())
                        {
                            actualProgress = 30;
                            UpdateStatus("Process found, applying patch...");
                            int retries = 0;
                            while (Process.GetProcessesByName("MuseDash").Any() && !MuseDash.Exploit(Process.GetProcessesByName("MuseDash")?.First()) && ++retries < 100)
                            {
                                actualProgress = 30 + retries / 70;
                                UpdateStatus($"Failed to patch, retrying... Attempt #{retries}");
                                Thread.Sleep(50);
                            }
                            if (retries == 100)
                            {
                                DisplayCriticalFailture();
                                UpdateStatus("Failed to patch Muse Dash.");
                            }
                            else
                            {
                                actualProgress = 100;
                                DisplaySuccess();
                                UpdateStatus("Muse Dash patched successfully.");
                            }
                            Thread.Sleep(7500);
                            Environment.Exit(0);
                        }
                        else
                            UpdateStatus($"Waiting for Muse Dash process ({Math.Round(waitRemaining.TotalSeconds)}s remaining)...");
                        if (waitRemaining.TotalSeconds <= 0)
                        {
                            DisplayCriticalFailture();
                            UpdateStatus("Failed to patch Muse Dash. Process was not found.");
                            Thread.Sleep(7500);
                            Environment.Exit(0);
                        }
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    File.WriteAllText("jap-crash.txt", ex.ToString());
                    DisplayCriticalFailture();
                    UpdateStatus($"Failed to patch Muse Dash. Check jap-crash.txt");
                    Thread.Sleep(7500);
                    Environment.Exit(0);
                }
            });
            threadMain.Start();
        }

        public MainWindow()
        {
            InitializeComponent();
            Closed += OnClosedEvent;
            MouseDown += OnDragEvent;
            updateProgressThread = new Thread(UpdateProgress);
            updateProgressThread.Start();
            Run();
        }

        private void OnDragEvent(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void OnClosedEvent(object sender, EventArgs e)
        {
            updateProgressThread.Abort();
        }
    }
}
