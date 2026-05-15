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
        #region Private Fields

        // --- SYSTEM TRAY (SAÄz ALT Ä°KON) DEÄzÄ°ÅzKENÄ° ---
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private System.Windows.Forms.ContextMenuStrip _trayContextMenu;

        // GDI ikon handle'larÄ± â€” Dispose'da serbest bÄ±rakÄ±lacak
        private IntPtr _hIconBig = IntPtr.Zero;
        private IntPtr _hIconSmall = IntPtr.Zero;

        // Image Ram Cache (FPS Drop Ã‡Ã¶zÃ¼mÃ¼)
        private Dictionary<string, string> _resourceCache = new Dictionary<string, string>();

        // --- EKÄ°PMAN KARTI: Albion Render API Ã¶nbelleÄŸi ---
        // key = InternalName (Ã¶r. T5_HEAD_PLATE_SET1), value = disk yolu
        private readonly Dictionary<string, string> _itemRenderCache = new Dictionary<string, string>();
        // HalihazÄ±rda indirilen isimler â€" Ã§ift istek gÃ¶ndermemek iÃ§in
        private readonly HashSet<string> _itemRenderDownloading = new HashSet<string>();
        // Son başarısız indirme zamanları (kısa retry backoff)
        private readonly Dictionary<string, DateTime> _itemRenderFailedAt = new Dictionary<string, DateTime>();
        // Ekipman kartı özelliği açık/kapalı
        private bool _showEquipmentCards = true;
        private bool _resourceTrackerOnlyMode = false;
        private bool _equipmentCardsMoveable = false;
        private float _equipmentCardsX = -1f;
        private float _equipmentCardsY = 12f;
        private int _equipmentCardsMaxSlots = 5;
        private float _equipmentCardsMemorySeconds = 3f;
        private bool _whitelistImportSameGuild = true;
        private bool _whitelistImportSameAlliance = false;
        private readonly Dictionary<int, DateTime> _enemyLastSeenAt = new();
        private readonly Dictionary<int, Player> _enemyCardCache = new();
        private DateTime _lastItemRenderErrorAt = DateTime.MinValue;
        private DateTime _lastPacketParserErrorAt = DateTime.MinValue;
        // Kart slotları: null = boş | int = playerId
        private readonly int?[] _equipCardSlots = new int?[8];

        // Trackers Color
        private Vector4 _trackerLaserColorMobs = new Vector4(0.0f, 1.0f, 1.0f, 0.9f);
        private Vector4 _trackerLaserColorResources = new Vector4(0.8f, 0.0f, 1.0f, 0.8f);
        private float _trackerScreenOffsetX = 0f;
        private float _trackerScreenOffsetY = -96f; // VarsayÄ±lan olarak biraz yukarÄ± aldÄ±k
        private float _trackerStartGap = 0; // Karakterin iÃ§inden geÃ§memesi iÃ§in boÅŸluk
        private float _trackerScaleX = 7.0f;          // Piksel/birim â€” DÃ¼nya X ekseni (saÄŸ/sol)
        private float _trackerScaleY = 7.0f;          // Piksel/birim â€” DÃ¼nya Y ekseni (ileri/geri)
        private float _trackerAngleOffset = 0f;       // AÃ§Ä± ince ayarÄ± (derece, -45 â†’ +45)
        private float _trackerLaserEndOffsetX = 0f;   // Lazer ucu yatay ince ayar
        private float _trackerLaserEndOffsetY = 0f;   // Lazer ucu dikey ince ayar

        // Smooth player position (lerped in render loop for smooth laser)
        private float _smoothPlayerX;
        private float _smoothPlayerY;
        private bool _smoothPlayerInitialized;

        //--- Map iÃ§in lazÄ±mdÄ±
        private Dictionary<string, float> _mapSizes = new Dictionary<string, float>();
        // TypeId bazlı living-resource eşlemesi (deterministik kategori/tier)
        private readonly Dictionary<int, (HarvestableCategory category, int tier)> _livingResourceTypeMap = new();

        // --- HARÄ°TA ARKA PLAN DEÄzÄ°ÅzKENLERÄ° ---
        private bool _showMapBackground = true;
        private float _mapOpacity = 0.8f;
        private float _mapScale = 1.0f;
        private float _mapGlobalOffsetX = 0f;
        private float _mapGlobalOffsetY = 0f;
        private Dictionary<string, float> _zoneScales = new Dictionary<string, float>();
        private readonly Dictionary<string, string> _mapImagePathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // RESÄ°M Ã–NBELLEKLEME (FPS DROP Ã‡Ã–ZÃœMÃœ)
        private Dictionary<string, bool> _imageCache = new Dictionary<string, bool>();
        private void ClearImageCache() => _imageCache.Clear();
        private bool IsImageExistsCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (_imageCache.TryGetValue(path, out bool exists)) return exists;
            bool actualExists = File.Exists(path);
            // Hem var olan hem var olmayan yollarÄ± cache'le; frame baÅŸÄ±na IO maliyetini azalt
            _imageCache[path] = actualExists;
            return actualExists;
        }

        //TaÃ§ Blacklist
        private List<int> _crownBlacklist = new List<int>();
        private string _crownSearchQuery = "";
        private List<System.Collections.Generic.KeyValuePair<int, MobInfo>> _cachedCrownResults = null;
        private string _lastCrownSearchQuery = null;

        // Simulator Variables
        private int _simMobId = 15;
        private string _simMobSearch = ""; // Mob arama metni
        private int _simResType = 0;       // SeÃ§ili Resource Type ID
        private string _simResSearch = ""; // Resource arama metni
        private int _simResTier = 4;
        private int _simResEnchant = 0;    // Enchant (0,1,2,3)
        private int _simResCount = 5;
        private int _simResCap = 5;
        private float _simX = 5.0f;
        private float _simY = 5.0f;

        // Themes
        private int _selectedTheme = 1; // 0=Old 1=Main


        //Raw
        private static bool _autoRawDump = false;
        private DateTime _lastAutoRawDumpTime = DateTime.MinValue;

        // Show/Hide Button
        private bool _isChangingHotkey = false;

        private readonly GameStateManager _gameStateManager;
        private readonly object _dataLock = new object();
        private readonly List<Player> _playersBuffer = new();
        private readonly List<Mob> _mobBuffer = new();
        private readonly List<Harvestable> _harvestBuffer = new();

        // Oyuncu hareket yönü takibi: playerId -> (prevX, prevY, prevDistToLocal)
        private readonly Dictionary<int, (float x, float y, float dist)> _prevPlayerPos = new();
        // Hareket izi: playerId -> son N pozisyon (dünya koordinatları)
        private const int TrailMaxPoints = 10;
        private readonly Dictionary<int, Queue<(float x, float y)>> _playerTrails = new();
        // Waypoint sistemi: null = işaret yok
        private (float x, float y)? _waypoint = null;


        private readonly HashSet<int> _announcedChests = new HashSet<int>();


        // --- DEV TOOLS & OPTIMIZASYON DEGISKENLERI ---
        // Arama sonuÃ§larÄ±nÄ± burada tutacaÄŸÄ±z ki her karede (frame) tekrar hesaplamasÄ±n.
        private List<KeyValuePair<int, MobInfo>> _cachedDatabaseResults = new();
        private bool _searchRefreshNeeded = true; // Ä°lk aÃ§Ä±lÄ±ÅŸta veriyi Ã§ekmesi iÃ§in

        // Takip listesi (ConfigMobs) iÃ§in Ã¶nbellek
        private List<int> _cachedTrackedResults = new();

        // Parser Tab (kiÅŸi bazlÄ± ham parser verisi)
        private int _parserSelectedPlayerId = -1;
        private string _parserPlayerFilter = "";
        private string _parserEventFilter = "";
        private string _parserPayloadFilter = "";
        private string _parserNewCharFieldFilter = "";
        private bool _parserDiffOnlyChanged = true;
        private string _parserSnapshotAPayload = "";
        private string _parserSnapshotBPayload = "";
        private string _parserSnapshotALabel = "A: (bos)";
        private string _parserSnapshotBLabel = "B: (bos)";
        private string _parserByteDecodeStatus = "";
        private bool _parserOnlyNearby = true;
        private string _parserExportStatus = "";
        private string _parserActiveProfile = "Custom";
        private readonly Dictionary<int, string> _parserMobNameOverrides = new();
        private int _parserMobRenameTargetId = -1;
        private string _parserMobRenameInput = "";

        private sealed class PointerCandidateStat
        {
            public int Hits { get; set; }
            public float BestDistance { get; set; } = float.MaxValue;
            public float LastDistance { get; set; }
            public float LastX { get; set; }
            public float LastY { get; set; }
            public DateTime LastSeen { get; set; } = DateTime.MinValue;
            public string LastSource { get; set; } = "";
        }

        private bool _pointerScannerEnabled = false;
        private float _pointerScannerMaxDistance = 40f;
        private int _pointerScannerMaxOffset = 24;
        private float _pointerScannerIntervalMs = 250f;
        private DateTime _pointerScannerLastRun = DateTime.MinValue;
        private bool _pointerScannerUseManualTarget = false;
        private float _pointerScannerManualTargetX = 0f;
        private float _pointerScannerManualTargetY = 0f;
        private readonly Dictionary<string, PointerCandidateStat> _pointerScannerCandidates = new();
        private int _manualTargetUdpPortInput = 5056;
        private string _lastParserDumpPath = string.Empty;

        // UI State
        private int _activeTab = 6;
        private string[] _tabs = new string[7]; // Sabit 6 elemanlÄ± dizi (Ã‡Ã¶kmeyi engeller)
        private string _lastTabLanguage = null; // Performans: _tabs'Ä± sadece dil deÄŸiÅŸince yeniler
        private volatile bool _hideSettingsWindow = false;
        public Action OnLoginSuccess;
        private int _selectedLangIndex = 0; // 0 = TR, 1 = EN, 2 = RU, 3 = ZH
        private string[] _languages = { "Türkçe (TR)", "English (EN)", "Russian (RU)", "Chinese (ZH)" };

        // Device (Adaptör) Sekmesi Değişkenleri
        private List<string> _availableAdapters = new List<string>();
        private int _selectedAdapterIndex = 0;
        private bool _adaptersLoaded = false;

        // YENİ: Ağ Tanılama Aracı (Traffic Scanner) Değişkenleri
        private bool _isTestingAdapters = false;
        private Dictionary<string, bool> _adapterTestResults = new Dictionary<string, bool>();

        // --- UI CONSOLE (LOG) SÄ°STEMÄ° ---
        // Hem terminale hem UIConsole'a tarihli log atar
        private void Log(string mesaj, LogLevel level = LogLevel.Info)
        {
            string tamLog = $"[{DateTime.Now:HH:mm:ss}] {mesaj}";
            ConsoleColor renk = level switch
            {
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Logo => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray
            };
            Console.ForegroundColor = renk;
            Console.WriteLine(tamLog);
            Console.ResetColor();
            UIConsole.Log(tamLog, level);
        }

        public void AddUIConsoleLog(string message)
        {
            UIConsole.Log(message);
        }
        // --- TÃœRKÃ‡E FONT Ä°Ã‡Ä°N RAM SABÄ°TLEYÄ°CÄ° DEÄzÄ°ÅzKENLER ---
        private GCHandle _trRangesHandle;
        // TÃ¼m TÃ¼rkÃ§e karakterleri tek aralÄ±k olarak birleÅŸtirdik (0x011E-0x015F kapsar: Äz,ÄŸ,Ä°,Ä±,Åz,ÅŸ + Ã‡,Ã§,Ã–,Ã¶,Ãœ,Ã¼ zaten 0x00C7-0x00FC arasÄ±nda)
        private static ushort[] _trRanges = new ushort[] {
            0x0020, 0x00FF, // Ä°ngilizce, temel semboller + Ã‡,Ã§,Ã–,Ã¶,Ãœ,Ã¼
            0x011E, 0x015F, // Äz,ÄŸ,Ä°,Ä±,Åz,ÅŸ (tek birleÅŸik aralÄ±k)
            0               // Dizi bitiÅŸi (Zorunlu)
        };

        private int _toggleKey = 0x7B; // VarsayÄ±lan: F12
        private bool _lastKeyState = false;

        private int _muteToggleKey = 0x2D; // VarsayÄ±lan: INSERT
        private bool _lastMuteKeyState = false;
        private bool _isChangingMuteHotkey = false;

        private bool _isSizeFixed = false;
        private bool _isIconSet = false;
        private bool _shouldUpdateRadarPos = false; // Config yÃ¼klendiÄŸinde pozisyonu zorlamak iÃ§in

        // Config
        private bool _showResourceIcons = true;
        private bool _showPlayers = true;
        private bool _showEnemyMobs = true;
        private bool _showResources = true;
        private bool _showMists = true;
        private bool _showNormalMobs = true;
        private bool _showBosses = true;
        private bool _showGuild = true;
        private bool _showPlayerName = true;
        private bool _showPlayerCount = true;
        private bool _showMobNames = true;
        private bool _debugConsoleLog = false;
        private bool _debugMobs = false;
        private bool _debugLivingResources = false;
        private bool _debugStaticResources = false;
        private bool _enableLogging = false;

        private bool _enableSoundAlerts = true;

        // StreamModule â€” OBS / ekran yakalamadan gizleme
        private bool _streamModuleEnabled = false;

        private bool _showWatermark = true;
        private float _watermarkX = 100f;
        private float _watermarkY = 100f;
        private bool _watermarkMoveable = false;

        private bool _detachRadar = true;
        private bool _radarMoveable = false;
        private float _radarWinX = 300f;
        private float _radarWinY = 300f;
        private float _radarSize = 400f;

        private float _zoom = 1.0f;
        private float _radarOffsetX = 0.0f;
        private float _radarOffsetY = 0.0f;
        private float _globalIconSize = 28.0f;
        private float _renderDistance = 70.0f;

        private bool _invertX = false;
        private bool _invertY = false;
        private bool _swapXY = true;
        private float _radarRotation = -45.0f;

        private bool _showPlayerList = true;
        private bool _playerListMoveable = false;
        private float _playerListX = 300f;
        private float _playerListY = 600f;

        private bool _showItemIds = false;
        private bool _showChestIds = false;

        private float _discordBoxOffsetX = 200f;
        private float _discordBoxOffsetY = -5f;
        private float _licenseBoxOffsetX = 8f;
        private float _licenseBoxOffsetY = -5f;

        // Data & Lists
        private Dictionary<int, ItemInfo> _itemDatabase = new Dictionary<int, ItemInfo>();
        private string _lastMapId = "";
        private string _configFolder;
        private string _configFileNameInput = "Default";
        private string[] _availableConfigs = new string[0];
        private int _selectedConfigIndex = -1;

        private HashSet<int> _customPriorityMobs = new HashSet<int>();
        private HashSet<int> _ignoredMobIds = new HashSet<int>();

        // Trackers 
        private bool _trackerEnableResources = false;
        private bool _trackerEnableVipMobs = false;
        private bool _trackerEnableNormalMobs = false;
        private HashSet<int> _trackerCustomMobs = new HashSet<int>();
        private string _trackerSearchQuery = "";
        private int _selectedMobIdForTracker = -1;


        private string _mobSearchQuery = "";
        private int _selectedMobIdForAdd = -1;
        private string _blacklistSearchQuery = "";
        private int _selectedMobIdForBlacklist = -1;
        private string _trackedListFilter = "";
        private string _trackerListFilter = "";


        private string _whitelistInput = "";
        private HashSet<string> _whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _whitelistPath;

        // Assets
        private string _crownImagePath;
        private string _spiderImagePath;
        private string[] _mistImagePaths = new string[5];
        private string _feyDragonPath;
        private string _griffinPath;
        private string _veilWeaverPath;
        private string _aspectBossIconPath;

        private Dictionary<int, MobInfo> _mobDatabase = new();
        private Dictionary<HarvestableCategory, bool> _resourceMasterToggles = new();
        private Dictionary<HarvestableCategory, bool[,]> _resourceFilters = new();

        private static readonly Regex _tierRegex = new Regex(@"T(\d+)_", RegexOptions.Compiled);
        private static readonly Regex _enchantRegex = new Regex(@"LEVEL(\d+)|@(\d+)", RegexOptions.Compiled);
        private int _resourceTruthMode = 0; // 0=Name First, 1=Network First, 2=Metadata First
        private long _lastDebugTime = 0;
        private int _lastEnemyCount = 0;
        private DateTime _enemyCountLastUpdated = DateTime.MinValue;
        private float _enemyCountHoldSeconds = 1.5f;
        private DateTime _lastBeepTime = DateTime.MinValue;
        // Toast bildirim sistemi
        private readonly List<(string msg, DateTime time, uint color)> _toasts = new();
        // Yaklaşan düşman yön göstergesi (ekran kenarı ok)
        private bool _showDangerCompass = true;

        // Performans: Random sÄ±nÄ±f seviyesinde bir kez oluÅŸturulur
        private static readonly Random _rng = new Random();

        // Performans: GetSystemMetrics Ã§aÄŸrÄ±larÄ± cache'lenir (her frame sistem Ã§aĞrÄ±sÄ± yapÄ±lmaz)
        private int _cachedPrimaryScreenW = 0;
        private int _cachedPrimaryScreenH = 0;
        #endregion
    }
}


