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
        public static string UpdateStatusText = "Sürüm Kontrol Ediliyor...";
        public static System.Numerics.Vector4 UpdateStatusColor = new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f); // Başlangıçta gri
        // --- ANA PROGRAM ---
        [STAThread]
        public static void Main(string[] args)
        {



            ErrorCodeSink.Install();

            // Konsol açılır açılmaz arka planda güncellemeyi kontrol etsin
            Task.Run(() => CheckForUpdatesAsync());

            // Sonra Main içinde ilk satır olarak:
            TrySetSdlHint("SDL_WINDOW_UTILITY", "0");

            TrySetSdlHint("SDL_HINT_WINDOW_NO_TASKBAR", "0");
            Lang.LoadLanguage("EN");
            bool isRunningAsAdmin = IsAdministrator();

            try { SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch (System.Exception ex) { System.Console.WriteLine($"Error Code : 27 | {ex.Message}"); }

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

                        var parser = new AlbionDataParser();
                        var mobsHandler = new MobsHandler();
                        var playersHandler = new PlayersHandler();
                        var harvestableHandler = new HarvestableHandler();

                        var mapHandler = new MapChangeHandler((yeniMapId) =>
                        {
                            RenkliYaz(string.Format(Lang.Get("MapLoaded"), yeniMapId), ConsoleColor.Magenta);
                            manager.SetCurrentMap(yeniMapId);
                        });

                        parser.RegisterEventHandler(mobsHandler);
                        parser.RegisterEventHandler(playersHandler);
                        parser.RegisterEventHandler(harvestableHandler);
                        parser.RegisterEventHandler(mapHandler);
                        RenkliYaz(Lang.Get("HandlersOK"), ConsoleColor.Green);

                        mobsHandler.Mobs.Subscribe(manager.UpdateMobsState);
                        harvestableHandler.Harvestables += manager.UpdateHarvestablesState;
                        playersHandler.LocalPlayerPosition += manager.UpdateLocalPlayer;
                        playersHandler.OtherPlayersDetected += manager.UpdateOtherPlayers;
                        playersHandler.PlayerLeft += manager.RemovePlayer;

                        RenkliYaz(Lang.Get("EventsOK"), ConsoleColor.Green);

                        RenkliYaz(Lang.Get("EngineStart"), ConsoleColor.Yellow);
                        var engine = new PacketEngine(parser, manager);

                        // Capture başlangıcı bloklayıcı olabildiği için UI thread'i kilitlemesin.
                        Task.Run(() =>
                        {
                            try
                            {
                                engine.Start();
                                RenkliYaz(Lang.Get("ListenActive"), ConsoleColor.White);
                            }
                            catch (Exception ex)
                            {
                                System.Console.WriteLine($"Error Code : 511 | {ex.Message}");
                                RenkliYaz($"ENGINE START ERROR: {ex.Message}", ConsoleColor.Red);
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
                        System.Console.WriteLine($"Error Code : 510 | {ex.Message}");
                    }
                };

                radar.OnLoginSuccess?.Invoke();
                radar.Run();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 51 | {ex.Message}");
                RenkliYaz($"FATAL ERROR: {ex.Message}", ConsoleColor.Red);
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
                string url = "https://raw.githubusercontent.com/Jung1330/Nightwatch-Radar/refs/heads/Website/App/version.txt?t={DateTime.Now.Ticks}";

                string response = await client.GetStringAsync(url);
                int currentVersion = 2; // Senin belirlediğin şu anki sürüm numarası

                if (int.TryParse(response.Trim(), out int latestVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        // UI için Güncelleme Var durumunu ayarla (Kırmızı Renk)
                        UpdateStatusText = $"[Güncelleme Var]";
                        UpdateStatusColor = new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1f);
                        RenkliYaz($"[BİLDİRİM] Yeni Bir Güncelleme Mevcut! Lütfen GitHub'dan güncelleyin.", ConsoleColor.Cyan);
                    }
                    else
                    {
                        // UI için Güncel durumunu ayarla (Yeşil Renk)
                        UpdateStatusText = $"[Güncel]";
                        UpdateStatusColor = new System.Numerics.Vector4(0.2f, 1.0f, 0.2f, 1f);
                        RenkliYaz($"[BİLDİRİM] Uygulama Güncel.", ConsoleColor.DarkGray);
                    }
                }
            }
            catch (Exception)
            {
                // UI için Hata durumunu ayarla (Sarı Renk)
                UpdateStatusText = "[Sürüm Kontrol Edilemedi]";
                UpdateStatusColor = new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1f);
                RenkliYaz("[UYARI] Sürüm kontrolü yapılamadı (Bağlantı veya Link Hatası).", ConsoleColor.DarkGray);
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
                ConsoleColor.Red => LogLevel.Error,
                ConsoleColor.DarkRed => LogLevel.Error,
                ConsoleColor.Magenta => LogLevel.Logo,
                _ => LogLevel.Info
            };

            UIConsole.Log(tamLog, level);
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

            UIConsole.Log(Lang.Get("RadarStart"), LogLevel.Logo);
        }
    }
}


