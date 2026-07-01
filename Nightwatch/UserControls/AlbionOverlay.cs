#region Kütüphaneler (Using Directives)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Mappers;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
#endregion

namespace Nightwatch
{

    public partial class AlbionOverlay : Overlay, IDisposable
    {

        public AlbionOverlay(GameStateManager manager, bool isRunningAsAdmin) : base("Nightwatch Overlay")
        {
            _gameStateManager = manager;

            #region Sistem Tepsisi (System Tray)
            // --- SYSTEM TRAY (SAÃ„z ALT KÃ–Ã…zE İKONU) THREAD KURULUMU ---
            new System.Threading.Thread(() =>
            {
                try
                {
                    _trayIcon = new System.Windows.Forms.NotifyIcon();
                    string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nightwatch.ico");
                    if (File.Exists(iconPath)) _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                    else _trayIcon.Icon = System.Drawing.SystemIcons.Application;

                    _trayIcon.Text = Lang.Get("App_System_Tray");
                    _trayIcon.Visible = true;

                    // Çift tıklayınca menüyü geri açar
                    _trayIcon.MouseDoubleClick += (s, e) => { _hideSettingsWindow = false; };

                    // Sağ tık menüsü (Profesyonel dokunuş)
                    _trayContextMenu = new System.Windows.Forms.ContextMenuStrip();
                    _trayContextMenu.ShowImageMargin = false;
                    _trayContextMenu.Items.Add(Lang.Get("App_System_Show_Menu"), null, (s, e) => _hideSettingsWindow = false);
                    _trayContextMenu.Items.Add(Lang.Get("App_System_Exit"), null, (s, e) => { _trayIcon.Dispose(); _trayContextMenu?.Dispose(); Environment.Exit(0); });
                    _trayIcon.ContextMenuStrip = _trayContextMenu;

                    // EN ÖNEMLİ KISIM: İkonun tıklamaları algılaması için Windows Mesaj Döngüsünü başlatır!
                    System.Windows.Forms.Application.Run();
                }
                catch (Exception ex)
                {
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    Console.WriteLine($"[TRAY ERROR] {ex.Message}");
                }
            })
            { IsBackground = true }.Start();
            #endregion

            #region Base Directory ve Whitelist Yolu
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _whitelistPath = System.IO.Path.Combine(baseDir, "Assets", "Helper", "whitelist.txt");

            // KALICI DİL YÜKLEME - Veritabanları Yüklenmeden Önce Dili Oku!
            try
            {
                string langPath = System.IO.Path.Combine(baseDir, "Config", "lang.txt");
                if (File.Exists(langPath))
                {
                    string startupLang = File.ReadAllText(langPath).Trim().ToUpper();
                    Lang.LoadLanguage(startupLang);
                }
            }
            catch (Exception ex) { Log($"[HATA] Dil yüklenemedi: {ex.Message}", Nightwatch.LogLevel.Error); }

            LoadWhitelist();
            
            // MobMapper'ı ve diğer veritabanlarını başlat
            string initLang = Lang.CurrentLanguage ?? "EN";
            MobMapper.Instance.Reload($"Assets/Helper/mobs_{initLang}_min.json");
            
            CheckAndLoadDatabase();
            LoadZonesDatabase();
            LoadItemDatabaseTXT();
            #endregion

            #region Config Klassörü ve Dosyaları
            _configFolder = System.IO.Path.Combine(baseDir, "Config");
            if (!Directory.Exists(_configFolder)) Directory.CreateDirectory(_configFolder);
            RefreshConfigList();
            #endregion

            #region Resource Master Toggles ve Filtreleri (default olarak hepsi açık) - Yeni
            var categories = Enum.GetValues(typeof(HarvestableCategory)).Cast<HarvestableCategory>();
            foreach (var cat in categories)
            {
                if (cat == HarvestableCategory.None) continue;
                if (!_resourceMasterToggles.ContainsKey(cat)) _resourceMasterToggles[cat] = true;
                if (!_resourceFilters.ContainsKey(cat))
                {
                    var matrix = new bool[8, 4];
                    for (int i = 0; i < 8; i++) for (int j = 0; j < 4; j++) matrix[i, j] = true;
                    _resourceFilters[cat] = matrix;
                }
            }
            #endregion

            #region Asset Resim Yolları
            _crownImagePath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "crown.png");
            _spiderImagePath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "CRYSTALSPIDER.png");
            _aspectBossIconPath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "boss_icon.png");
            _feyDragonPath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "FAIRYDRAGON.png");
            _griffinPath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "GRIFFIN.png");
            _veilWeaverPath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "VEILWEAVER.png");
            _aspectBossIconPath = System.IO.Path.Combine(baseDir, "Assets", "Resources", "aspect.png");

            for (int i = 0; i < 5; i++)
                _mistImagePaths[i] = System.IO.Path.Combine(baseDir, "Assets", "Resources", $"mist_{i}.png");
            #endregion

            #region Oto Config Yakalama (Autoconfig.txt)
            // --- OTOMATİK CONFIG YÜKLEME (Autoconfig.txt) ---
            string defaultTxtPath = System.IO.Path.Combine(baseDir, "Assets", "Autoconfig.txt");
            string configToLoad = "Varsayilan"; // Dosya yoksa bunu yükler

            try
            {
                if (File.Exists(defaultTxtPath))
                {
                    string content = File.ReadAllText(defaultTxtPath).Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        if (content.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            content = System.IO.Path.GetFileNameWithoutExtension(content);
                        // GÜVENLİK: Path traversal önlemi Ã¢â‚¬â€ sadece dosya adı geçebilir
                        content = System.IO.Path.GetFileName(content);
                        if (!string.IsNullOrEmpty(content))
                            configToLoad = content;
                    }
                }
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
            LoadConfig(configToLoad);
            Log(string.Format(Lang.Get("ConfigLoaded"), configToLoad), LogLevel.Success);
            #endregion
        }

    }
}









