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
    #region Enums & Data Classes
    public enum RadarEntityType { Mob, Harvestable, Chest, Mist, Dungeon, Player }
    public enum HarvestableCategory { None, Fiber, Hide, Log, Ore, Rock }


    public class RadarConfig
    {
        // --- YENİ EKLENEN HARİTA CONFIG AYARLARI ---
        public bool ShowMapBackground { get; set; } = true;
        public float MapOpacity { get; set; } = 0.8f;

        public string LastMapIDConfig { get; set; }
        public int SelectedTheme { get; set; } = 1; // 0=Original, 1=Obsidian
        public string Language { get; set; } = "EN"; // VarsayÄ±lan dil TÃ¼rkÃ§e

        public bool TrackerEnableResources { get; set; } = false;
        public bool TrackerEnableVipMobs { get; set; } = false;
        public bool TrackerEnableNormalMobs { get; set; } = false;
        public HashSet<int> TrackerCustomMobs { get; set; } = new HashSet<int>();
        public Vector4 TrackerLaserColorMobs { get; set; } = new Vector4(1.0f, 0.0f, 0.0f, 0.8f);
        public Vector4 TrackerLaserColorResources { get; set; } = new Vector4(0.8f, 0.0f, 1.0f, 0.8f);
        public float TrackerLaserWorldScale { get; set; } = 0f; // migration only
        public float TrackerPixelsPerUnit { get; set; } = 0f;   // migration only
        public float TrackerScaleX { get; set; } = 0f;
        public float TrackerScaleY { get; set; } = 0f;
        public float TrackerAngleOffset { get; set; } = 0f;
        public float TrackerLaserEndOffsetX { get; set; } = 0f;
        public float TrackerLaserEndOffsetY { get; set; } = 0f;

        public bool EnableSoundAlerts { get; set; } = false;
        public bool StreamModuleEnabled { get; set; } = false;

        public List<int> CrownBlacklist { get; set; } = new List<int>();
        public int ToggleKey { get; set; } = 0x7B; // VarsayÄ±lan F12 (Hex 0x7B)
        public bool ShowResourceIcons { get; set; } = false;
        public bool EnableLogging { get; set; } = false;
        public bool ShowPlayers { get; set; } = true;
        public bool ShowEnemyMobs { get; set; } = true;
        public bool ShowResources { get; set; } = true;
        public bool ShowMists { get; set; } = true;
        public bool ShowNormalMobs { get; set; } = true;
        public bool ShowBosses { get; set; } = true;
        public bool ShowGuild { get; set; } = true;
        public bool ShowPlayerName { get; set; } = true;
        public bool ShowPlayerCount { get; set; } = true;
        public bool ShowMobNames { get; set; } = true;
        public bool DebugConsoleLog { get; set; } = false;

        public bool ShowPlayerList { get; set; } = true;
        public bool PlayerListMoveable { get; set; } = false;
        public float PlayerListX { get; set; } = 300f;
        public float PlayerListY { get; set; } = 600f;
        public bool ShowItemIds { get; set; } = false;
        public bool ShowChestIds { get; set; } = false;

        public bool ShowWatermark { get; set; } = true;
        public bool WatermarkMoveable { get; set; } = false;
        public float WatermarkX { get; set; } = 100f;
        public float WatermarkY { get; set; } = 100f;
        public bool ShowDangerCompass { get; set; } = true;
        public bool ShowEquipmentCards { get; set; } = true;
        public bool ResourceTrackerOnlyMode { get; set; } = false;
        public bool EquipmentCardsMoveable { get; set; } = false;
        public float EquipmentCardsX { get; set; } = -1f;
        public float EquipmentCardsY { get; set; } = 12f;
        public int EquipmentCardsMaxSlots { get; set; } = 5;
        public float EquipmentCardsMemorySeconds { get; set; } = 3f;
        public bool WhitelistImportSameGuild { get; set; } = true;
        public bool WhitelistImportSameAlliance { get; set; } = false;

        public bool DetachRadar { get; set; } = true;
        public float RadarWinX { get; set; } = 300f;
        public float RadarWinY { get; set; } = 300f;
        public bool RadarMoveable { get; set; } = false;
        public float RadarSize { get; set; } = 400f;

        public float Zoom { get; set; } = 1.0f;
        public float GlobalIconSize { get; set; } = 28.0f;
        public float RenderDistance { get; set; } = 70.0f;

        public bool InvertX { get; set; } = false;
        public bool InvertY { get; set; } = false;
        public bool SwapXY { get; set; } = true;
        public float RadarRotation { get; set; } = -45.0f;

        public List<int> CustomPriorityMobs { get; set; } = new List<int>();
        public List<int> IgnoredMobIds { get; set; } = new List<int>();

        public Dictionary<HarvestableCategory, bool> ResourceMasterToggles { get; set; } = new Dictionary<HarvestableCategory, bool>();
        public Dictionary<string, bool[][]> ResourceFilters { get; set; } = new Dictionary<string, bool[][]>();
    }

    public class ItemInfo
    {
        public string InternalName { get; set; }
        public string DisplayName { get; set; }
        public int Power { get; set; }
    }

    public class MobInfo
    {
        public string Name { get; set; }
        public int Tier { get; set; }
        public bool IsHarvestable { get; set; }
        public string HarvestType { get; set; }
    }
    #endregion
}


