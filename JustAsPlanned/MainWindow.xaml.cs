using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
            ColorAnimation ca = new ColorAnimation(System.Windows.Media.Color.FromRgb(110, 5, 5), new Duration(TimeSpan.FromSeconds(2)));
            gridBackgroundBlur.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        void DisplayReset() => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
        {
            ColorAnimation ca = new ColorAnimation(System.Windows.Media.Color.FromRgb(0, 0, 0), new Duration(TimeSpan.FromSeconds(2)));
            gridBackgroundBlur.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        void DisplaySuccess() => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
        {
            ColorAnimation ca = new ColorAnimation(System.Windows.Media.Color.FromRgb(5, 110, 5), new Duration(TimeSpan.FromSeconds(2)));
            gridBackgroundBlur.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        void DisplayUpdateAvailable() => Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
        {
            ColorAnimation ca = new ColorAnimation(System.Windows.Media.Color.FromRgb(50, 100, 235), new Duration(TimeSpan.FromSeconds(2)));
            gridBackgroundBlur.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
        }));

        private string CheckGitHubNewerVersion()
        {
            Version currentVersion = Assembly.GetEntryAssembly().GetName().Version;

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JustAsPlanned", currentVersion.ToString()));
            var response = client.GetAsync($"https://api.github.com/repos/Eimaen/JustAsPlanned/releases/latest").GetAwaiter().GetResult();
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (response.IsSuccessStatusCode)
            {
                string pattern = "\"tag_name\"\\s*:\\s*\"([^\"]*)\"";
                Match match = Regex.Match(responseBody, pattern);

                if (match.Success)
                {
                    string tagValue = match.Groups[1].Value;
                    Version latestVersion = Version.Parse(tagValue);
                    Debug.WriteLine("Latest version tag: " + tagValue);

                    if (latestVersion > currentVersion)
                        return latestVersion.ToString();
                    return string.Empty;
                }
                else
                {
                    Debug.WriteLine("Failed to check for a new release. No match found for tag_name property.");
                }
            }
            else
            {
                Debug.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
            }

            return string.Empty;
        }

        private void Run()
        {
            Thread threadMain = new Thread(() =>
            {
                try
                {
                    string newestVersion = CheckGitHubNewerVersion();
                    if (newestVersion != string.Empty)
                    {
                        DisplayUpdateAvailable();
                        UpdateStatus("There's a new update available!");
                        Thread.Sleep(4000);
                    }
                }
                catch
                {
                    DisplayCriticalFailture();
                    UpdateStatus("Unable to check for updates!");
                    Thread.Sleep(2000);
                    DisplayReset();
                }

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
                        actualProgress = 20;
                        UpdateStatus("No Steam process found. Waiting for MuseDash...");
                        while (!Process.GetProcessesByName("MuseDash").Any())
                            Thread.Sleep(100);
                    }
                    else
                    {
                        if (steamInstallationPath == null)
                            steamInstallationPath = Process.GetProcessesByName("Steam").First().MainModule.FileName;
                        else
                            steamInstallationPath = System.IO.Path.Combine(steamInstallationPath.ToString(), "steam.exe");
                        actualProgress = 20;
                        UpdateStatus("Starting Muse Dash via Steam...");
                        Thread.Sleep(3000);
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
                            UpdateStatus("How? Steam is running and doesn't exist?! Contact devs.");
                            Thread.Sleep(5000);
                            Environment.Exit(0);
                        }
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
            threadMain.IsBackground = true;
            threadMain.Start();
        }

        private struct BackgroundImage
        {
            public string CopyrightMessage;
            public string SourceUrl;
            public Bitmap Image;
        }

        private List<BackgroundImage> backgroundImages = new List<BackgroundImage>
        {
            new BackgroundImage {
                SourceUrl = "https://www.pixiv.net/en/artworks/81691677",
                CopyrightMessage = "Artwork by U-Joe, Pixiv ID: 81691677",
                Image = Properties.Resources.Marisa
            },
            new BackgroundImage {
                SourceUrl = "https://www.pixiv.net/en/artworks/105672733",
                CopyrightMessage = "Artwork by Lanana, Pixiv ID: 105672733",
                Image = Properties.Resources.Koishi
            },
            new BackgroundImage {
                SourceUrl = "https://www.pixiv.net/en/artworks/64961553",
                CopyrightMessage = "Artwork by 鳥成, Pixiv ID: 64961553",
                Image = Properties.Resources.Youmu
            },
            new BackgroundImage {
                SourceUrl = "https://www.pixiv.net/en/artworks/47424126",
                CopyrightMessage = "Artwork by 雛見, Pixiv ID: 47424126",
                Image = Properties.Resources.Minecraft
            }
        };

        BackgroundImage currentBackgroundImageData;

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        public void LoadBackgroundImage()
        {
            currentBackgroundImageData = backgroundImages.OrderBy(x => Guid.NewGuid()).First();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                lblCopyright.Content = currentBackgroundImageData.CopyrightMessage;
                gridBackgroundImage.Background = new ImageBrush()
                {
                    ImageSource = ImageSourceFromBitmap(currentBackgroundImageData.Image),
                    Stretch = Stretch.UniformToFill
                };
            }));
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadBackgroundImage();
            Closed += OnClosedEvent;
            MouseDown += OnDragEvent;
            updateProgressThread = new Thread(UpdateProgress);
            updateProgressThread.Start();
            Run();
        }

        private void OpenCopyrightReference(object sender, MouseButtonEventArgs e)
        {
            Process.Start(currentBackgroundImageData.SourceUrl);
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
