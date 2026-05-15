#region Using Directives
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

        #region Helpers & Config

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
                    Log("[HATA] Gecersiz config adi.", LogLevel.Error);
                    return;
                }

                string fullPath = System.IO.Path.Combine(_configFolder, safeName + ".json");
                var cfg = new RadarConfig
                {
                    LastMapIDConfig = _gameStateManager.CurrentMapId ?? "0000",

                    Language = _selectedLangIndex switch { 0 => "TR", 1 => "EN", 2 => "RU", 3 => "ZH", _ => "TR" },

                    ShowMapBackground = _showMapBackground,
                    MapOpacity = _mapOpacity,

                    SelectedTheme = _selectedTheme,

                    EnableSoundAlerts = _enableSoundAlerts,
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
                    ShowResourceIcons = _showResourceIcons,
                    ShowPlayers = _showPlayers,
                    ShowEnemyMobs = _showEnemyMobs,
                    ShowResources = _showResources,
                    ShowMists = _showMists,
                    ShowNormalMobs = _showNormalMobs,
                    ShowBosses = _showBosses,
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
                    ShowChestIds = _showChestIds,
                    ShowDangerCompass = _showDangerCompass,
                    ShowEquipmentCards = _showEquipmentCards,
                    ResourceTrackerOnlyMode = _resourceTrackerOnlyMode,
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
                System.Console.WriteLine($"Error Code : 56 | {ex.Message}");
                Log($"[HATA] {ex.Message}", LogLevel.Error);
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
                var cfg = JsonConvert.DeserializeObject<RadarConfig>(json);
                if (cfg == null) return;

                // YENÄ° EKLENDÄ° (CrownBlacklist YÃ¼kleme)
                if (cfg.CrownBlacklist != null) _crownBlacklist = new List<int>(cfg.CrownBlacklist);

                _showMapBackground = cfg.ShowMapBackground;
                _mapOpacity = cfg.MapOpacity;               

                if (!string.IsNullOrEmpty(cfg.LastMapIDConfig) && _gameStateManager != null) { _gameStateManager.SetCurrentMap(cfg.LastMapIDConfig); }

                _showResourceIcons = cfg.ShowResourceIcons; _showPlayers = cfg.ShowPlayers; _showEnemyMobs = cfg.ShowEnemyMobs; _showResources = cfg.ShowResources; _showMists = cfg.ShowMists;
                _showNormalMobs = cfg.ShowNormalMobs; _showBosses = cfg.ShowBosses; _showGuild = cfg.ShowGuild; _showPlayerName = cfg.ShowPlayerName; _showPlayerCount = cfg.ShowPlayerCount;
                _showMobNames = cfg.ShowMobNames; _debugConsoleLog = cfg.DebugConsoleLog; _showWatermark = cfg.ShowWatermark; _watermarkMoveable = cfg.WatermarkMoveable; _watermarkX = cfg.WatermarkX; _watermarkY = cfg.WatermarkY;
                _detachRadar = cfg.DetachRadar; _radarMoveable = cfg.RadarMoveable; _radarWinX = cfg.RadarWinX; _radarWinY = cfg.RadarWinY; _radarSize = cfg.RadarSize; _zoom = cfg.Zoom; _globalIconSize = cfg.GlobalIconSize;
                _renderDistance = cfg.RenderDistance; _invertX = cfg.InvertX; _invertY = cfg.InvertY; _swapXY = cfg.SwapXY;
                _radarRotation = cfg.RadarRotation; // RadarRotation artÄ±k yÃ¼kleniyor

                if (!string.IsNullOrEmpty(cfg.Language))
                {
                    Lang.LoadLanguage(cfg.Language);
                    _selectedLangIndex = cfg.Language.ToUpper() switch
                    {
                        "EN" => 1,
                        "RU" => 2,
                        "ZH" => 3,
                        _ => 0
                    };
                }
                _showPlayerList = cfg.ShowPlayerList; _playerListMoveable = cfg.PlayerListMoveable; _playerListX = cfg.PlayerListX; _playerListY = cfg.PlayerListY;
                _showItemIds = cfg.ShowItemIds; _showChestIds = cfg.ShowChestIds; _enableLogging = cfg.EnableLogging; _enableSoundAlerts = cfg.EnableSoundAlerts; _selectedTheme = cfg.SelectedTheme;
                _streamModuleEnabled = cfg.StreamModuleEnabled; _showDangerCompass = cfg.ShowDangerCompass; _showEquipmentCards = cfg.ShowEquipmentCards;
                _resourceTrackerOnlyMode = cfg.ResourceTrackerOnlyMode;
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

                // RADAR POZÄ°SYONUNU ZORLA
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
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 57 | {ex.Message}");
                Log($"[HATA] {ex.Message}", LogLevel.Error);
            }
        }


        // DestroyIcon satÄ±rÄ±nÄ± tamamen kaldÄ±rdÄ±k, Ã§Ã¼nkÃ¼ ikon bellekte kalmalÄ±!
        private void SetApplicationWindowIcon()
        {
            try
            {
                string nightwatchIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nightwatch.ico");
                string legacyIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nightwatch.ico");
                string iconPath = File.Exists(nightwatchIconPath) ? nightwatchIconPath : legacyIconPath;
                if (File.Exists(iconPath))
                {
                    IntPtr hwnd = FindWindow(null, "Nightwatch Overlay");
                    if (hwnd == IntPtr.Zero)
                        hwnd = FindWindow(null, "Nightwatch Overlay");

                    // Eski ikonlarÄ± serbest bÄ±rak (bellek sÄ±zÄ±ntÄ±sÄ±nÄ± Ã¶nler)
                    if (_hIconBig != IntPtr.Zero) { DestroyIcon(_hIconBig); _hIconBig = IntPtr.Zero; }
                    if (_hIconSmall != IntPtr.Zero) { DestroyIcon(_hIconSmall); _hIconSmall = IntPtr.Zero; }

                    // Windows GÃ¶rev Ã‡ubuÄŸu (Taskbar) iÃ§in uygun boyutlarÄ± (32x32 ve 16x16) zorluyoruz
                    _hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                    _hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

                    if (_hIconBig != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);
                    if (_hIconSmall != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 58 | {ex.Message}");
                if (_debugConsoleLog)
                    Log($"[HATA] {ex.Message}", LogLevel.Error);
            }
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

        private HarvestableCategory ParseCategoryFromString(string type) { if (string.IsNullOrEmpty(type)) return HarvestableCategory.None; string t = type.ToUpperInvariant(); if (t.Contains("AVALON") || t.Contains("DRONE")) { if (t.Contains("WOOD")) return HarvestableCategory.Log; if (t.Contains("ROCK")) return HarvestableCategory.Rock; if (t.Contains("HIDE")) return HarvestableCategory.Hide; if (t.Contains("FIBER")) return HarvestableCategory.Fiber; if (t.Contains("ORE")) return HarvestableCategory.Ore; return HarvestableCategory.None; /* Bilinmeyen Avalon/Drone tÃ¼rÃ¼ â€” Ore yerine None dÃ¶ndÃ¼r */ } if (t.Contains("LOG") || t.Contains("WOOD")) return HarvestableCategory.Log; if (t.Contains("ROCK") || t.Contains("STONE")) return HarvestableCategory.Rock; if (t.Contains("FIBER") || t.Contains("COTTON")) return HarvestableCategory.Fiber; if (t.Contains("HIDE") || t.Contains("SKIN")) return HarvestableCategory.Hide; if (t.Contains("ORE")) return HarvestableCategory.Ore; return HarvestableCategory.None; }
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
        private int ParseTier(string n) { var m = _tierRegex.Match(n); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
        private int ParseEnchant(string n)
        {
            var m = _enchantRegex.Match(n);
            if (!m.Success) return 0;
            string val = m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : m.Groups[2].Value;
            return int.TryParse(val, out int result) ? result : 0;
        }
        private void FixLayoutWait()
        {
            // TÃ¼m monitÃ¶rleri kapsayan sanal ekran boyutunu alÄ±yoruz
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
                    System.Console.WriteLine($"Error Code : 59 | {ex.Message}");
                    Log($"[HATA] {ex.Message}", LogLevel.Error);
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
                System.Console.WriteLine($"Error Code : 60 | {ex.Message}");
                if (_debugConsoleLog) Log($"[StreamModule] {ex.Message}", LogLevel.Error);
            }
        }

        private void SaveWhitelist()
        {
            try { File.WriteAllLines(_whitelistPath, _whitelist); }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 61 | {ex.Message}");
                Log($"[HATA] {ex.Message}", LogLevel.Error);
            }
        }

        #region Key GÃ¼venliÄŸi â€” AES Åzifreleme/Ã‡Ã¶zme
        // Makineye Ã¶zgÃ¼ entropi: HWID yerine sabit bir uygulama anahtarÄ±.
        // GerÃ§ek bir Ã¼rÃ¼n iÃ§in DPAPI (ProtectedData) tercih edilir.
        private static readonly byte[] _aesKey = new byte[]
        {
            0x4E, 0x69, 0x67, 0x68, 0x74, 0x77, 0x61, 0x74,
            0x63, 0x68, 0x52, 0x61, 0x64, 0x61, 0x72, 0x4B,
            0x65, 0x79, 0x32, 0x30, 0x32, 0x34, 0x53, 0x65,
            0x63, 0x75, 0x72, 0x65, 0x41, 0x45, 0x53, 0x21
        }; // 32 bayt = AES-256

        private static string EncryptKey(string plainText)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _aesKey;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            byte[] data = System.Text.Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = enc.TransformFinalBlock(data, 0, data.Length);
            // IV + ÅŸifreli veri birleÅŸik olarak Base64'e Ã§evrilir
            byte[] combined = new byte[aes.IV.Length + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
            Buffer.BlockCopy(encrypted, 0, combined, aes.IV.Length, encrypted.Length);
            return Convert.ToBase64String(combined);
        }

        private static string DecryptKey(string cipherBase64)
        {
            byte[] combined = Convert.FromBase64String(cipherBase64);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _aesKey;
            byte[] iv = new byte[aes.BlockSize / 8];
            byte[] encrypted = new byte[combined.Length - iv.Length];
            Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(combined, iv.Length, encrypted, 0, encrypted.Length);
            aes.IV = iv;
            using var dec = aes.CreateDecryptor();
            byte[] plain = dec.TransformFinalBlock(encrypted, 0, encrypted.Length);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        #endregion
        private void LoadWhitelist()
        {
            try { if (File.Exists(_whitelistPath)) foreach (var l in File.ReadAllLines(_whitelistPath)) _whitelist.Add(l.Trim()); }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 62 | {ex.Message}");
                Log($"[HATA] {ex.Message}", LogLevel.Error);
            }
        }
        #endregion

    }
}


