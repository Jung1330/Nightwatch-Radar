using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AlbionDataHandlers;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Handlers.MapHandler;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
using System.Net.Http;

namespace Nightwatch
{
    class Program
    {
        // ========================================================
        // --- WINDOWS API (CMD GIZLEME TERCUMANLARI) ---
        // ========================================================
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // WinExe olarak calisirken --replay ciktisini ebeveyn konsola baglamak icin
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        // Main metodunun EN BAŞINA, her şeyden önce
        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern bool SDL_SetHint(string name, string value);

        const int SW_HIDE = 0;
        // ===========

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        // --- DLL IMPORTLARI (DPI ve Konsol Yonetimi icin) ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(int dpiContext);
        private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
        public static string CurrentUpdateKey = "UpdateChecking";
        public static string UpdateStatusText => Lang.Get(CurrentUpdateKey);
        public static System.Numerics.Vector4 UpdateStatusColor = new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f); // Başlangıçta gri

        // --- H2: SÜRÜM TEK KAYNAKTAN ---
        // Sürüm koda YAZILMAZ; kök klasördeki Directory.Build.props -> <Version> değerinden gelir.
        // Yayın adımları o dosyanın içinde yorum olarak yazılı (unutma diye).
        /// <summary>Uygulamanın gerçek sürümü (derleme sürümü). Örn: 1.0.9.0</summary>
        public static Version GetLocalVersion()
            => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        /// <summary>Arayüzde göstermek için kısa sürüm metni. Örn: v1.0.9</summary>
        public static string VersionText
        {
            get
            {
                Version v = GetLocalVersion();
                return $"v{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
            }
        }
        // --- ANA PROGRAM ---
        [STAThread]
        public static void Main(string[] args)
        {
            // --- TEST HARNESS: kaydedilmis paketleri OYUNSUZ oynat ---
            // Kullanim: Nightwatch.exe --replay log\packets_YYYYMMDD_HHMMSS.jsonl
            if (args != null && args.Length >= 2 && args[0].Equals("--replay", StringComparison.OrdinalIgnoreCase))
            {
                try { AttachConsole(-1); } catch { }   // WinExe -> ebeveyn konsola baglan (cikti gorunsun)
                Environment.ExitCode = Nightwatch.Managers.ReplayHarness.Run(args[1]);
                return;
            }

            ErrorCodeSink.Install();

            // Konsol açılır açılmaz arka planda güncellemeyi kontrol etsin
            Task.Run(() => CheckForUpdatesAsync());

            // Sonra Main içinde ilk satır olarak:
            TrySetSdlHint("SDL_WINDOW_UTILITY", "0");

            TrySetSdlHint("SDL_HINT_WINDOW_NO_TASKBAR", "0");
            Lang.LoadLanguage("EN");
            bool isRunningAsAdmin = IsAdministrator();

            try { SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch (System.Exception ex) { Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error); }

            try
            {
                var manager = new GameStateManager();
                var radar = new AlbionOverlay(manager, isRunningAsAdmin);
                radar.OnLoginSuccess = () =>
                {
                    try
                    {
                                                DrawLogo();
                        RenkliYaz(Lang.Get("SysLoading"), ConsoleColor.Cyan);

                        // Parser + handler baglantisi TEK yerde (PacketPipeline).
                        // Boylece --replay testi ile oyundaki kurulum AYNI olur.
                        PacketEngine engine = null;
                        var pipeline = new PacketPipeline(manager,
                            onMapChanged: (yeniMapId) =>
                            {
                                RenkliYaz(string.Format(Lang.Get("MapLoaded"), yeniMapId), ConsoleColor.Magenta);
                                engine?.PurgeQueue();
                            },
                            onDungeonDebug: (msg) => RenkliYaz(msg, ConsoleColor.Cyan));
                        RenkliYaz(Lang.Get("HandlersOK"), ConsoleColor.Green);
                        RenkliYaz(Lang.Get("EventsOK"), ConsoleColor.Green);

                        RenkliYaz(Lang.Get("EngineStart"), ConsoleColor.Yellow);
                        engine = new PacketEngine(pipeline.Parser, manager);

                        // Capture baslangici bloklayici olabildigi icin UI thread'i kilitlemesin.
                        Task.Run(() =>
                        {
                            try
                            {
                                engine.Start();
                                RenkliYaz(Lang.Get("ListenActive"), ConsoleColor.White);
                            }
                            catch (Exception ex)
                            {
                                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                                RenkliYaz($"{Lang.Get("EngineError")}: {ex.Message}", ConsoleColor.Red);
                            }
                        });

                        // Motorlar tam gaz calistiktan sonra CMD'yi gizle
                        new Thread(() =>
                        {
                            Thread.Sleep(100);
                            IntPtr handle = GetConsoleWindow();
                            if (handle != IntPtr.Zero) ShowWindow(handle, SW_HIDE);
                        })
                        { IsBackground = true }.Start();
                    }
                    catch (Exception ex)
                    {
                        Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    }
                };

                radar.OnLoginSuccess?.Invoke();
                radar.Run();
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                RenkliYaz($"{Lang.Get("FatalError")}: {ex.Message}", ConsoleColor.Red);
                Console.ReadLine();
            }
        }


        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Nightwatch-Updater");

                // REPO ADINI VE KULLANICI ADINI KENDİ GITHUB BİLGİLERİNE GÖRE DEĞİŞTİR
                string url = $"https://raw.githubusercontent.com/Jung1330/Nightwatch-Radar/refs/heads/Website/App/version.txt?t={DateTime.Now.Ticks}";

                string response = await client.GetStringAsync(url);

                // --- H2: SÜRÜM KARŞILAŞTIRMASI ---
                // Yerel sürüm koda yazılmaz; Directory.Build.props -> <Version> değerinden okunur.
                // GitHub'daki version.txt SADECE tek satır sürüm numarası içerir. Örn:  1.0.10
                //
                // YENİ SÜRÜM YAYINLARKEN:
                //   1) Directory.Build.props içindeki <Version> değerini artır  (1.0.9 -> 1.0.10)
                //   2) dotnet publish -c Release ile yeni exe'yi derle
                //   3) GitHub'daki App/version.txt dosyasına AYNI numarayı yaz (tek satır)
                //   4) Yayınla -> kullanıcılar açılışta güncelleme uyarısını görür
                Version localVersion = GetLocalVersion();
                string remoteText = response.Trim().TrimStart('v', 'V').Trim();

                if (Version.TryParse(remoteText, out Version remoteVersion))
                {
                    if (remoteVersion > localVersion)
                    {
                        // UI için Güncelleme Var durumunu ayarla (Kırmızı Renk)
                        CurrentUpdateKey = "UpdateAvailable";
                        UpdateStatusColor = new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1f);
                        RenkliYaz($"{Lang.Get("UpdateNotifyAvailable")} (v{localVersion} -> v{remoteVersion})", ConsoleColor.Cyan);
                    }
                    else
                    {
                        // UI için Güncel durumunu ayarla (Yeşil Renk)
                        CurrentUpdateKey = "Updated";
                        UpdateStatusColor = new System.Numerics.Vector4(0.2f, 1.0f, 0.2f, 1f);
                        RenkliYaz($"{Lang.Get("UpdateNotifyUpdated")} (v{localVersion})", ConsoleColor.DarkGray);
                    }
                }
                else
                {
                    // version.txt bozuk/okunamadı: sessizce "güncel" DEME, durumu bildir
                    CurrentUpdateKey = "UpdateFailed";
                    UpdateStatusColor = new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1f);
                    RenkliYaz($"{Lang.Get("UpdateNotifyFailed")} (version.txt: '{remoteText}')", ConsoleColor.DarkGray);
                }
            }
            catch (Exception)
            {
                // UI için Hata durumunu ayarla (Sarı Renk)
                CurrentUpdateKey = "UpdateFailed";
                UpdateStatusColor = new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1f);
                RenkliYaz(Lang.Get("UpdateNotifyFailed"), ConsoleColor.DarkGray);
            }
        }

        // --- YARDIMCI METOTLAR ---
        static void RenkliYaz(string mesaj, ConsoleColor renk)
        {
            string tamLog = $"[{DateTime.Now:HH:mm:ss}] {mesaj}";

            Console.ForegroundColor = renk;
            Console.WriteLine(tamLog);
            Console.ResetColor();

            LogLevel level = renk switch
            {
                ConsoleColor.Green => LogLevel.Success,
                ConsoleColor.Yellow => LogLevel.Warning,
                ConsoleColor.Red => Nightwatch.LogLevel.Error,
                ConsoleColor.DarkRed => Nightwatch.LogLevel.Error,
                ConsoleColor.Magenta => LogLevel.Logo,
                _ => LogLevel.Info
            };

            Nightwatch.UIConsole.Log(tamLog, level);
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void TrySetSdlHint(string name, string value)
        {
            try
            {
                SDL_SetHint(name, value);
            }
            catch (DllNotFoundException)
            {
                // SDL yüklenmeden önce hint set edilmek istenirse sessiz geç.
            }
            catch (EntryPointNotFoundException)
            {
                // SDL sürümü ilgili hint API'sini desteklemiyorsa sessiz geç.
            }
        }

        // Havali Logo Cizimi (Albion Ustte, Radar Altta)
        static void DrawLogo()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.ResetColor();

            Nightwatch.UIConsole.Log(Lang.Get("RadarStart"), LogLevel.Logo);
        }
    }
}








