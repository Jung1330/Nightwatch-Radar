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
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
#endregion

namespace Nightwatch
{
    public partial class AlbionOverlay
    {

        #region Yardımcılar ve Ayarlar (Helpers & Config)

        private static string SanitizeConfigName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            string safe = System.IO.Path.GetFileName(name.Trim());
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            return safe;
        }

        private void RefreshConfigList() { if (Directory.Exists(_configFolder)) _availableConfigs = Directory.GetFiles(_configFolder, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).ToArray(); }
        private void SaveConfig(string name)
        {
            try
            {
                string safeName = SanitizeConfigName(name);
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    Log(Lang.Get("Error_InvalidConfig") ?? "[HATA] Gecersiz config adi.", Nightwatch.LogLevel.Error);
                    return;
                }

                string fullPath = System.IO.Path.Combine(_configFolder, safeName + ".json");
                var cfg = new RadarConfig
                {
                    LastMapIDConfig = _gameStateManager.CurrentMapId ?? "0000",

                    Language = _selectedLangIndex switch { 0 => "TR", 1 => "EN", 2 => "RU", 3 => "ZH", _ => "EN" },

                    ShowMapBackground = _showMapBackground,
                    MapOpacity = _mapOpacity,
                    EnableBetaHeatmap = _enableBetaHeatmap,

                    SelectedTheme = _selectedTheme,

                    EnableSoundAlerts = _enableSoundAlerts,
                    EnableToastAlerts = _enableToastAlerts,
                    StreamModuleEnabled = _streamModuleEnabled,

                    TrackerEnableResources = _trackerEnableResources,
                    TrackerEnableVipMobs = _trackerEnableVipMobs,
                    TrackerEnableNormalMobs = _trackerEnableNormalMobs,
                    TrackerCustomMobs = _trackerCustomMobs,
                    TrackerLaserColorMobs = _trackerLaserColorMobs,
                    TrackerLaserColorResources = _trackerLaserColorResources,
                    TrackerPixelsPerUnit = 0f, // legacy
                    TrackerScaleX = _trackerScaleX,
                    TrackerScaleY = _trackerScaleY,
                    TrackerAngleOffset = _trackerAngleOffset,
                    TrackerLaserEndOffsetX = _trackerLaserEndOffsetX,
                    TrackerLaserEndOffsetY = _trackerLaserEndOffsetY,

                    EnableLogging = _enableLogging,
                    CrownBlacklist = _crownBlacklist,
                    ToggleKey = _toggleKey,
                    HideAllKey = _hideAllKey,
                    ShowResourceIcons = _showResourceIcons,
                    ShowDungeonIcons = _showDungeonIcons,
                    ShowSoloDungeons = _showSoloDungeons,
                    ShowSoloEnchantments = _showSoloEnchantments,
                    ShowSoloBossLair = _showSoloBossLair,
                    ShowGroupDungeons = _showGroupDungeons,
                    ShowGroupEnchantments = _showGroupEnchantments,
                    ShowGroupBossLair = _showGroupBossLair,
                    ShowCorruptedDungeons = _showCorruptedDungeons,
                    ShowHellgateDungeons = _showHellgateDungeons,
                    ShowPlayers = _showPlayers,
                    ShowEnemyMobs = _showEnemyMobs,
                    ShowResources = _showResources,
                    ShowMists = _showMists,
                    ShowBetaTracks = _showBetaTracks,
                    ShowBetaWisps = _showBetaWisps,
                    ShowBetaIndicators = _showBetaIndicators,
                    ShowBetaStructures = _showBetaStructures,
                    ShowBetaChests = _showBetaChests,
                    ShowExits = _showExits,
                    ShowWispCages = _showWispCages,
                    ShowSmugglers = _showSmugglers,
                    ShowTrackers = _showTrackers,
                    TrackBear = _trackBear,
                    TrackWolf = _trackWolf,
                    TrackPanther = _trackPanther,
                    TrackHumanoid = _trackHumanoid,
                    TrackElemental = _trackElemental,
                    TrackEnt = _trackEnt,
                    TrackImp = _trackImp,
                    TrackGolem = _trackGolem,
                    TrackWerewolf = _trackWerewolf,
                    ShowAvalonianDungeons = _showAvalonianDungeons,
                    ShowAvalonianTiers = _showAvalonianTiers,
                    ShowNormalMobs = _showNormalMobs,
                    ShowBosses = _showBosses,
                    ShowHiddenChests = _showHiddenChests,
                    ShowChestIds = _showChestIds,
                    ShowGuild = _showGuild,
                    ShowPlayerName = _showPlayerName,
                    ShowPlayerCount = _showPlayerCount,
                    ShowMobNames = _showMobNames,
                    DebugConsoleLog = _debugConsoleLog,
                    ShowWatermark = _showWatermark,
                    WatermarkMoveable = _watermarkMoveable,
                    WatermarkX = _watermarkX,
                    WatermarkY = _watermarkY,
                    DetachRadar = _detachRadar,
                    RadarMoveable = _radarMoveable,
                    RadarWinX = _radarWinX,
                    RadarWinY = _radarWinY,
                    RadarSize = _radarSize,
                    Zoom = _zoom,
                    GlobalIconSize = _globalIconSize,
                    BossIconSize = _bossIconSize,
                    RenderDistance = _renderDistance,
                    InvertX = _invertX,
                    InvertY = _invertY,
                    SwapXY = _swapXY,
                    RadarRotation = _radarRotation,
                    CustomPriorityMobs = _customPriorityMobs.ToList(),
                    IgnoredMobIds = _ignoredMobIds.ToList(),
                    ResourceMasterToggles = _resourceMasterToggles,
                    ShowPlayerList = _showPlayerList,
                    PlayerListMoveable = _playerListMoveable,
                    PlayerListX = _playerListX,
                    PlayerListY = _playerListY,
                    ShowItemIds = _showItemIds,
                    ShowDangerCompass = _showDangerCompass,
                    ShowEquipmentCards = _showEquipmentCards,
                    DetailInfo = _detailInfo,
                    ResourceTrackerOnlyMode = _resourceTrackerOnlyMode,
                    ResourceShowOnlyEnchanted = _resourceShowOnlyEnchanted,
                    EquipmentCardsMoveable = _equipmentCardsMoveable,
                    EquipmentCardsX = _equipmentCardsX,
                    EquipmentCardsY = _equipmentCardsY,
                    EquipmentCardsMaxSlots = _equipmentCardsMaxSlots,
                    EquipmentCardsMemorySeconds = _equipmentCardsMemorySeconds,
                    WhitelistImportSameGuild = _whitelistImportSameGuild,
                    WhitelistImportSameAlliance = _whitelistImportSameAlliance
                };
                foreach (var kvp in _resourceFilters)
                {
                    bool[][] jagged = new bool[8][];
                    for (int i = 0; i < 8; i++) { jagged[i] = new bool[4]; for (int j = 0; j < 4; j++) jagged[i][j] = kvp.Value[i, j]; }
                    cfg.ResourceFilters[kvp.Key.ToString()] = jagged;
                }
                string json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                File.WriteAllText(fullPath, json); RefreshConfigList();
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
        }

        private void LoadConfig(string name)
        {
            string safeName = SanitizeConfigName(name);
            if (string.IsNullOrWhiteSpace(safeName)) return;

            string fullPath = System.IO.Path.Combine(_configFolder, safeName + ".json");
            if (!File.Exists(fullPath)) return;
            try
            {
                string json = File.ReadAllText(fullPath);
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    Error = (sender, args) => { args.ErrorContext.Handled = true; }
                };
                var cfg = JsonConvert.DeserializeObject<RadarConfig>(json, settings);
                if (cfg == null) return;

                // YENİ EKLENDİ (CrownBlacklist Yükleme)
                if (cfg.CrownBlacklist != null) _crownBlacklist = new List<int>(cfg.CrownBlacklist);

                _showMapBackground = cfg.ShowMapBackground;
                _mapOpacity = cfg.MapOpacity;               
                _enableBetaHeatmap = cfg.EnableBetaHeatmap;

                if (!string.IsNullOrEmpty(cfg.LastMapIDConfig) && _gameStateManager != null) { _gameStateManager.SetCurrentMap(cfg.LastMapIDConfig); }

                _showResourceIcons = cfg.ShowResourceIcons; 
                _showDungeonIcons = cfg.ShowDungeonIcons; 
                _showSoloDungeons = cfg.ShowSoloDungeons;
                
                if (cfg.ShowSoloEnchantments != null && cfg.ShowSoloEnchantments.Length >= 5) _showSoloEnchantments = cfg.ShowSoloEnchantments;
                else { _showSoloEnchantments = new bool[5] { true, true, true, true, true }; if (cfg.ShowSoloEnchantments != null) Array.Copy(cfg.ShowSoloEnchantments, _showSoloEnchantments, Math.Min(5, cfg.ShowSoloEnchantments.Length)); }

                _showSoloBossLair = cfg.ShowSoloBossLair;
                _showGroupDungeons = cfg.ShowGroupDungeons;

                if (cfg.ShowGroupEnchantments != null && cfg.ShowGroupEnchantments.Length >= 5) _showGroupEnchantments = cfg.ShowGroupEnchantments;
                else { _showGroupEnchantments = new bool[5] { true, true, true, true, true }; if (cfg.ShowGroupEnchantments != null) Array.Copy(cfg.ShowGroupEnchantments, _showGroupEnchantments, Math.Min(5, cfg.ShowGroupEnchantments.Length)); }

                _showGroupBossLair = cfg.ShowGroupBossLair;
                _showCorruptedDungeons = cfg.ShowCorruptedDungeons;
                _showHellgateDungeons = cfg.ShowHellgateDungeons;
                _showPlayers = cfg.ShowPlayers; 
                _showEnemyMobs = cfg.ShowEnemyMobs; 
                _showBosses = cfg.ShowBosses;
                _showHiddenChests = cfg.ShowHiddenChests;
                _showChestIds = cfg.ShowChestIds;
                _showResources = cfg.ShowResources;
                _showMists = cfg.ShowMists;
                _showBetaTracks = cfg.ShowBetaTracks;
                _showBetaWisps = cfg.ShowBetaWisps;
                _showBetaIndicators = cfg.ShowBetaIndicators;
                _showBetaStructures = cfg.ShowBetaStructures;
                _showBetaChests = cfg.ShowBetaChests;
                _showExits = cfg.ShowExits;
                _showWispCages = cfg.ShowWispCages;
                _showSmugglers = cfg.ShowSmugglers;
                _showTrackers = cfg.ShowTrackers;
                _trackBear = cfg.TrackBear;
                _trackWolf = cfg.TrackWolf;
                _trackPanther = cfg.TrackPanther;
                _trackHumanoid = cfg.TrackHumanoid;
                _trackElemental = cfg.TrackElemental;
                _trackEnt = cfg.TrackEnt;
                _trackImp = cfg.TrackImp;
                _trackGolem = cfg.TrackGolem;
                _trackWerewolf = cfg.TrackWerewolf;
                _showAvalonianDungeons = cfg.ShowAvalonianDungeons;

                if (cfg.ShowAvalonianTiers != null && cfg.ShowAvalonianTiers.Length >= 9) _showAvalonianTiers = cfg.ShowAvalonianTiers;
                else { _showAvalonianTiers = new bool[9] { true, true, true, true, true, true, true, true, true }; if (cfg.ShowAvalonianTiers != null) Array.Copy(cfg.ShowAvalonianTiers, _showAvalonianTiers, Math.Min(9, cfg.ShowAvalonianTiers.Length)); }
                _showNormalMobs = cfg.ShowNormalMobs; _showBosses = cfg.ShowBosses; _showGuild = cfg.ShowGuild; _showPlayerName = cfg.ShowPlayerName; _showPlayerCount = cfg.ShowPlayerCount;
                _showMobNames = cfg.ShowMobNames; _debugConsoleLog = cfg.DebugConsoleLog; _showWatermark = cfg.ShowWatermark; _watermarkMoveable = cfg.WatermarkMoveable; _watermarkX = cfg.WatermarkX; _watermarkY = cfg.WatermarkY;
                _detachRadar = cfg.DetachRadar; _radarMoveable = cfg.RadarMoveable; _radarWinX = cfg.RadarWinX; _radarWinY = cfg.RadarWinY; _radarSize = cfg.RadarSize; _zoom = cfg.Zoom; _globalIconSize = cfg.GlobalIconSize; _bossIconSize = cfg.BossIconSize;
                _renderDistance = cfg.RenderDistance; _invertX = cfg.InvertX; _invertY = cfg.InvertY; _swapXY = cfg.SwapXY;
                _radarRotation = cfg.RadarRotation; // RadarRotation artık yükleniyor

                if (!string.IsNullOrEmpty(cfg.Language))
                {
                    string loadedLang = cfg.Language.ToUpper();
                    Lang.LoadLanguage(loadedLang);
                    _selectedLangIndex = loadedLang switch
                    {
                        "EN" => 1,
                        "RU" => 2,
                        "ZH" => 3,
                        _ => 0
                    };

                    // Sync language setting to lang.txt
                    try
                    {
                        string langPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "lang.txt");
                        System.IO.File.WriteAllText(langPath, loadedLang);
                    }
                    catch { }

                    // Apply the language font only if ImGui has initialized
                    if (_isFontReady)
                    {
                        ApplyLanguageFont(loadedLang);
                    }

                    // GÜNCELLEME: Haritada "Will o' Wisp", "Mists Portal" gibi isim bazlı arama eşleşmelerinin (Mists/Wisps/Cages)
                    // bozulmaması için, yüklenen yeni dile göre Mob veritabanlarını ve Mapper'ı hemen yeniden yükle!
                    AlbionDataHandlers.Mappers.MobMapper.Instance.Reload($"Assets/Helper/mobs_{loadedLang}_min.json");
                    CheckAndLoadDatabase();
                }
                _showPlayerList = cfg.ShowPlayerList; _playerListMoveable = cfg.PlayerListMoveable; _playerListX = cfg.PlayerListX; _playerListY = cfg.PlayerListY;
                _streamModuleEnabled = cfg.StreamModuleEnabled; _showDangerCompass = cfg.ShowDangerCompass; _showEquipmentCards = cfg.ShowEquipmentCards; _detailInfo = cfg.DetailInfo;
                _enableToastAlerts = cfg.EnableToastAlerts;
                _resourceTrackerOnlyMode = cfg.ResourceTrackerOnlyMode;
                _resourceShowOnlyEnchanted = cfg.ResourceShowOnlyEnchanted;
                _equipmentCardsMoveable = cfg.EquipmentCardsMoveable;
                _equipmentCardsX = cfg.EquipmentCardsX;
                _equipmentCardsY = cfg.EquipmentCardsY;
                _equipmentCardsMaxSlots = Math.Clamp(cfg.EquipmentCardsMaxSlots, 1, _equipCardSlots.Length);
                _equipmentCardsMemorySeconds = Math.Clamp(cfg.EquipmentCardsMemorySeconds, 0f, 30f);
                _whitelistImportSameGuild = cfg.WhitelistImportSameGuild;
                _whitelistImportSameAlliance = cfg.WhitelistImportSameAlliance;
                ApplyStreamModule();
                _trackerEnableResources = cfg.TrackerEnableResources;
                _trackerEnableVipMobs = cfg.TrackerEnableVipMobs;
                _trackerEnableNormalMobs = cfg.TrackerEnableNormalMobs;
                _trackerCustomMobs = cfg.TrackerCustomMobs ?? new HashSet<int>();
                _trackerLaserColorMobs = cfg.TrackerLaserColorMobs;
                _trackerLaserColorResources = cfg.TrackerLaserColorResources;
                // Migration: eski single-scale config varsa her ikisine de ata
                float legacyScale = cfg.TrackerScaleX > 0f ? cfg.TrackerScaleX : (cfg.TrackerPixelsPerUnit > 0f ? cfg.TrackerPixelsPerUnit : (cfg.TrackerLaserWorldScale > 0f ? cfg.TrackerLaserWorldScale * 7f : 7f));
                _trackerScaleX = cfg.TrackerScaleX > 0f ? cfg.TrackerScaleX : legacyScale;
                _trackerScaleY = cfg.TrackerScaleY > 0f ? cfg.TrackerScaleY : legacyScale;
                _trackerAngleOffset = cfg.TrackerAngleOffset;
                _trackerLaserEndOffsetX = cfg.TrackerLaserEndOffsetX;
                _trackerLaserEndOffsetY = cfg.TrackerLaserEndOffsetY;
                if (cfg.ToggleKey != 0) _toggleKey = cfg.ToggleKey;
                if (cfg.HideAllKey != 0) _hideAllKey = cfg.HideAllKey;

                // RADAR POZİSYONUNU ZORLA
                _shouldUpdateRadarPos = true;

                if (cfg.CustomPriorityMobs != null) _customPriorityMobs = new HashSet<int>(cfg.CustomPriorityMobs);
                if (cfg.IgnoredMobIds != null) _ignoredMobIds = new HashSet<int>(cfg.IgnoredMobIds);
                if (cfg.ResourceMasterToggles != null) _resourceMasterToggles = cfg.ResourceMasterToggles;
                if (cfg.ResourceFilters != null)
                {
                    foreach (var kvp in cfg.ResourceFilters)
                    {
                        if (Enum.TryParse(kvp.Key, out HarvestableCategory cat))
                        {
                            var matrix = new bool[8, 4];
                            for (int i = 0; i < 8; i++)
                            {
                                for (int j = 0; j < 4; j++)
                                {
                                    if (i < kvp.Value.Length && kvp.Value[i] != null && j < kvp.Value[i].Length)
                                        matrix[i, j] = kvp.Value[i][j];
                                }
                            }
                            _resourceFilters[cat] = matrix;
                        }
                    }
                }
                _configFileNameInput = safeName;
                
                // Tema ayarını yükle ve uygula (UI Thread'de çalıştırarak ImGui Crash'i engelle)
                _selectedTheme = cfg.SelectedTheme;
                EnqueueUi(() => Nightwatch.UserControls.MentalityTheme.SetTheme((Nightwatch.UserControls.ThemeType)_selectedTheme));
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
        }


        // DestroyIcon satırını tamamen kaldırdık, çünkü ikon bellekte kalmalı!
        private bool SetApplicationWindowIcon()
        {
            try
            {
                string nightwatchIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nightwatch.ico");
                string legacyIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nightwatch.ico");
                string iconPath = File.Exists(nightwatchIconPath) ? nightwatchIconPath : legacyIconPath;
                if (File.Exists(iconPath))
                {
                    IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                        hwnd = FindWindow(null, "Nightwatch Overlay");
                    if (hwnd == IntPtr.Zero)
                        hwnd = FindWindow(null, "Nightwatch");
                    if (hwnd == IntPtr.Zero)
                        return false;

                    // Eski ikonları serbest bırak (bellek sızıntısını önler)
                    if (_hIconBig != IntPtr.Zero) { DestroyIcon(_hIconBig); _hIconBig = IntPtr.Zero; }
                    if (_hIconSmall != IntPtr.Zero) { DestroyIcon(_hIconSmall); _hIconSmall = IntPtr.Zero; }

                    // Windows Görev Çubuğu (Taskbar) için uygun boyutları (32x32 ve 16x16) zorluyoruz
                    _hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                    _hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

                    if (_hIconBig != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);
                    if (_hIconSmall != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
                    return _hIconBig != IntPtr.Zero || _hIconSmall != IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                if (_debugConsoleLog)
                    Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
            return false;
        }

        private int GetItemPower(int id) { if (id <= 0) return 0; if (_itemDatabase.TryGetValue(id, out ItemInfo item)) return item.Power; return 0; }
        private string GetItemName(int id) { if (id <= 0) return "-"; if (_itemDatabase.TryGetValue(id, out ItemInfo item)) { if (_showItemIds) return $"{item.DisplayName} [{id}]"; return item.DisplayName; } if (_showItemIds) return $"[{id}]"; return "-"; }


        private static readonly Regex _cleanPrefixRegex = new Regex(
            @"@MOB_|@ITEMS_|T[1-8]_", RegexOptions.Compiled);

        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string clean = _cleanPrefixRegex.Replace(name, "").Replace("_", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean.ToLowerInvariant()).Trim();
        }

        private HarvestableCategory ParseCategoryFromString(string type) { if (string.IsNullOrEmpty(type)) return HarvestableCategory.None; string t = type.ToUpperInvariant(); if (t.Contains("AVALON") || t.Contains("DRONE")) { if (t.Contains("WOOD")) return HarvestableCategory.Log; if (t.Contains("ROCK")) return HarvestableCategory.Rock; if (t.Contains("HIDE")) return HarvestableCategory.Hide; if (t.Contains("FIBER")) return HarvestableCategory.Fiber; if (t.Contains("ORE")) return HarvestableCategory.Ore; return HarvestableCategory.None; /* Bilinmeyen Avalon/Drone türü Ã¢â‚¬â€ Ore yerine None döndür */ } if (t.Contains("LOG") || t.Contains("WOOD")) return HarvestableCategory.Log; if (t.Contains("ROCK") || t.Contains("STONE")) return HarvestableCategory.Rock; if (t.Contains("FIBER") || t.Contains("COTTON")) return HarvestableCategory.Fiber; if (t.Contains("HIDE") || t.Contains("SKIN")) return HarvestableCategory.Hide; if (t.Contains("ORE")) return HarvestableCategory.Ore; return HarvestableCategory.None; }
        private HarvestableCategory GetCategoryFromTypeId(int type)
        {
            // Albion Online Kaynak ID'leri (Güncellenmiş Kesin Aralıklar)
            if (type >= 0 && type <= 5) return HarvestableCategory.Log;
            if (type >= 6 && type <= 10) return HarvestableCategory.Rock;
            if (type >= 11 && type <= 15) return HarvestableCategory.Fiber;
            if (type >= 16 && type <= 22) return HarvestableCategory.Hide;
            if (type >= 23 && type <= 27) return HarvestableCategory.Ore; // 27'de biter

            return HarvestableCategory.None;
        }

        /// <summary>TypeID'nin harvestable kategoriye ait olup olmadığını kontrol eder.</summary>
        private bool IsHarvestableTypeId(int typeId)
        {
            // Harvestable TypeID aralıkları: Log(0-5), Rock(6-10), Fiber(11-15), Hide(16-22), Ore(23-27)
            return typeId >= 0 && typeId <= 27;
        }
        private int ParseTier(string n) { if (string.IsNullOrEmpty(n)) return 0; var m = _tierRegex.Match(n); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
        private int ParseEnchant(string n)
        {
            if (string.IsNullOrEmpty(n)) return 0;
            var m = _enchantRegex.Match(n);
            if (!m.Success) return 0;
            string val = m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : m.Groups[2].Value;
            return int.TryParse(val, out int result) ? result : 0;
        }
        private void FixLayoutWait()
        {
            // Tüm monitörleri kapsayan sanal ekran boyutunu alıyoruz
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1;

            // Performans: Birincil ekran boyutunu bir kez cache'le
            _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
            _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);

            IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
            if (h != IntPtr.Zero)
            {
                SetWindowPos(h, (IntPtr)HWND_TOPMOST, left, top, width, height, SWP_SHOWWINDOW);
                try { uint d = GetDpiForWindow(h); float s = d / 96.0f; if (s > 1.0f) ImGui.GetIO().DisplayFramebufferScale = new Vector2(s, s); }
                catch (Exception ex)
                {
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
                }
                _isSizeFixed = true;
            }
        }

        private void ApplyStreamModule()
        {
            try
            {
                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero) return;
                SetWindowDisplayAffinity(hwnd, _streamModuleEnabled ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                if (_debugConsoleLog) Log($"[StreamModule] {ex.Message}", Nightwatch.LogLevel.Error);
            }
        }

        private void SaveWhitelist()
        {
            try { File.WriteAllLines(_whitelistPath, _whitelist); }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
        }


        private void LoadWhitelist()
        {
            try { if (File.Exists(_whitelistPath)) foreach (var l in File.ReadAllLines(_whitelistPath)) _whitelist.Add(l.Trim()); }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
            }
        }
        #endregion

    }
}







