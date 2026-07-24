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
        #region Private Fields

        // --- SYSTEM TRAY (SAÃ„z ALT İKON) DEÃ„zİÃ…zKENİ ---
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private System.Windows.Forms.ContextMenuStrip _trayContextMenu;

        // GDI ikon handle'ları Ã¢â‚¬â€ Dispose'da serbest bırakılacak
        private IntPtr _hIconBig = IntPtr.Zero;
        private IntPtr _hIconSmall = IntPtr.Zero;

        // Image Ram Cache (FPS Drop Çözümü)
        private Dictionary<string, string> _resourceCache = new Dictionary<string, string>();

        // --- EKİPMAN KARTI: Albion Render API önbelleği ---
        // key = InternalName (ör. T5_HEAD_PLATE_SET1), value = disk yolu
        private readonly Dictionary<string, string> _itemRenderCache = new Dictionary<string, string>();
        // Halihazırda indirilen isimler Ã¢â‚¬" çift istek göndermemek için
        private readonly HashSet<string> _itemRenderDownloading = new HashSet<string>();
        // Son başarısız indirme zamanları (kısa retry backoff)
        private readonly Dictionary<string, DateTime> _itemRenderFailedAt = new Dictionary<string, DateTime>();
        // Ekipman kartı özelliği açık/kapalı
        private bool _showEquipmentCards = true;
        private bool _resourceTrackerOnlyMode = false;
        private bool _resourceShowOnlyEnchanted = false;
        private bool _equipmentCardsMoveable = false;
        private float _equipmentCardsX = -1f;
        private bool _enableBetaHeatmap = false;
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
        private List<int> _crownWhitelist = new List<int>();
        // Trackers Color
        private Vector4 _trackerLaserColorMobs = new Vector4(0.0f, 1.0f, 1.0f, 0.9f);
        private Vector4 _trackerLaserColorResources = new Vector4(0.8f, 0.0f, 1.0f, 0.8f);
        private float _trackerScreenOffsetX = 0f;
        private float _trackerScreenOffsetY = -96f; // Varsayılan olarak biraz yukarı aldık
        private float _trackerScaleX = 7.0f;          // Piksel/birim Ã¢â‚¬â€ Dünya X ekseni (sağ/sol)
        private float _trackerScaleY = 7.0f;          // Piksel/birim Ã¢â‚¬â€ Dünya Y ekseni (ileri/geri)
        private float _trackerAngleOffset = 0f;       // Açı ince ayarı (derece, -45 Ã¢â€ â€™ +45)
        private float _trackerLaserEndOffsetX = 0f;   // Lazer ucu yatay ince ayar
        private float _trackerLaserEndOffsetY = 0f;   // Lazer ucu dikey ince ayar

        // Smooth player position (lerped in render loop for smooth laser)
        private float _smoothPlayerX;
        private float _smoothPlayerY;
        private bool _smoothPlayerInitialized;

        //--- Map için lazımdı
        private Dictionary<string, float> _mapSizes = new Dictionary<string, float>();
        // TypeId bazlı living-resource eşlemesi (deterministik kategori/tier)
        private readonly Dictionary<int, (HarvestableCategory category, int tier)> _livingResourceTypeMap = new();

        // --- HARİTA ARKA PLAN DEÃ„zİÃ…zKENLERİ ---
        private bool _showMapBackground = true;
        private float _mapOpacity = 0.8f;
        private float _mapScale = 1.0f;
        private float _mapGlobalOffsetX = 0f;
        private float _mapGlobalOffsetY = 0f;
        private Dictionary<string, float> _zoneScales = new Dictionary<string, float>();
        private readonly Dictionary<string, string> _mapImagePathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedMapPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // RESİM ÖNBELLEKLEME (FPS DROP ÇÖZÜMÜ)
        private Dictionary<string, bool> _imageCache = new Dictionary<string, bool>();
        private void ClearImageCache() => _imageCache.Clear();
        private bool IsImageExistsCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (_imageCache.TryGetValue(path, out bool exists)) return exists;
            bool actualExists = File.Exists(path);
            // Hem var olan hem var olmayan yolları cache'le; frame başına IO maliyetini azalt
            _imageCache[path] = actualExists;
            return actualExists;
        }

        //Taç Blacklist
        private List<int> _crownBlacklist = new List<int>();
        private string _crownSearchQuery = "";

        // Simulator Variables
        private int _simMobId = 15;
        private string _simMobSearch = ""; // Mob arama metni
        private int _simResType = 0;       // Seçili Resource Type ID
        private string _simResSearch = ""; // Resource arama metni
        private int _simResTier = 4;
        private int _simResEnchant = 0;    // Enchant (0,1,2,3)
        private int _simResCount = 5;
        private int _simResCap = 5;

        // Themes
        private int _selectedTheme = 0;
        //Resource Label
        public bool _showResourceLabels = true;
        private bool _detailInfo = false;
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
        private readonly List<Dungeon> _dungeonBuffer = new();
        

        // Oyuncu hareket yönü takibi: playerId -> (prevX, prevY, prevDistToLocal)
        private readonly Dictionary<int, (float x, float y, float dist)> _prevPlayerPos = new();
        // Waypoint sistemi: null = işaret yok
        private (float x, float y)? _waypoint = null;




        // --- DEV TOOLS & OPTIMIZASYON DEGISKENLERI ---
        // Arama sonuçlarını burada tutacağız ki her karede (frame) tekrar hesaplamasın.
        private List<KeyValuePair<int, MobInfo>> _cachedDatabaseResults = new();
        private bool _searchRefreshNeeded = true; // İlk açılışta veriyi çekmesi için

        // Takip listesi (ConfigMobs) için önbellek
        private List<int> _cachedTrackedResults = new();

        // Parser Tab (kişi bazlı ham parser verisi)
        private int _parserSelectedTab = 0; // 0=Player, 1=Mob/Resource, 2=Dungeon
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
        private bool _parserOnlyNearby = true;
        private string _parserExportStatus = "";
        private string _parserActiveProfile = "Custom";
        private readonly Dictionary<int, string> _parserMobNameOverrides = new();
        private readonly List<(int off, int ia, int ib)> _parserByteDecodeResults = new();
        private int _parserMobRenameTargetId = -1;
        private string _parserMobRenameInput = "";

        private int _manualTargetUdpPortInput = 5056;
        private string _lastParserDumpPath = string.Empty;

        // UI State
        private int _activeTab = 6;
        private string[] _tabs = new string[7]; // Sabit 6 elemanlı dizi (Çökmeyi engeller)
        private string _lastTabLanguage = null; // Performans: _tabs'ı sadece dil değişince yeniler
        private volatile bool _hideSettingsWindow = false;
        public Action OnLoginSuccess;
        private int _selectedLangIndex = 1; // 0 = TR, 1 = EN, 2 = RU, 3 = ZH
        private string[] _languages = { "Türkçe (TR)", "English (EN)", "Russian (RU)", "Chinese (ZH)" };

        // Device (Adaptör) Sekmesi Değişkenleri
        private List<string> _availableAdapters = new List<string>();
        private int _selectedAdapterIndex = 0;
        private bool _adaptersLoaded = false;

        // Ağ Tanılama Aracı (Traffic Scanner) Değişkenleri
        private bool _isTestingAdapters = false;
        private Dictionary<string, bool> _adapterTestResults = new Dictionary<string, bool>();

        // --- UI CONSOLE (LOG) SİSTEMİ ---
        // Hem terminale hem UIConsole'a tarihli log atar
        private void Log(string mesaj, LogLevel level = LogLevel.Info)
        {
            string tamLog = $"[{DateTime.Now:HH:mm:ss}] {mesaj}";
            ConsoleColor renk = level switch
            {
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                Nightwatch.LogLevel.Error => ConsoleColor.Red,
                LogLevel.Logo => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray
            };
            Console.ForegroundColor = renk;
            Console.WriteLine(tamLog);
            Console.ResetColor();
            Nightwatch.UIConsole.Log(tamLog, level);
        }

        public void AddUIConsoleLog(string message)
        {
            Nightwatch.UIConsole.Log(message);
        }
        // --- TÜRKÇE FONT İÇİN RAM SABİTLEYİCİ DEÃ„zİÃ…zKENLER ---
        private GCHandle _trRangesHandle;
        // Tüm Türkçe karakterleri tek aralık olarak birleştirdik (0x011E-0x015F kapsar: Ã„z,ğ,İ,ı,Ã…z,ş + Ç,ç,Ö,ö,Ü,ü zaten 0x00C7-0x00FC arasında)
        private static ushort[] _trRanges = new ushort[] {
            0x0020, 0x00FF, // İngilizce, temel semboller + Ç,ç,Ö,ö,Ü,ü
            0x011E, 0x015F, // Ã„z,ğ,İ,ı,Ã…z,ş (tek birleşik aralık)
            0               // Dizi bitişi (Zorunlu)
        };

        private int _toggleKey = 0x7B; // Varsayılan: F12
        private bool _lastKeyState = false;

        private int _muteToggleKey = 0x2D; // Varsayılan: INSERT
        private bool _lastMuteKeyState = false;
        private bool _isChangingMuteHotkey = false;

        private int _hideAllKey = 0x7A; // Varsayılan: F11
        private bool _lastHideAllKeyState = false;
        private bool _hideAllMenus = false;
        private bool _isChangingHideAllHotkey = false;
        private bool _isFontReady = false;

        private bool _isSizeFixed = false;
        private bool _isIconSet = false;
        private bool _shouldUpdateRadarPos = false; // Config yüklendiğinde pozisyonu zorlamak için

        // Config
        private bool _showResourceIcons = true;
        private bool _showPlayers = true;
        private bool _showEnemyMobs = true;
        private bool _showResources = true;
        private bool _showMists = false;
        private bool _showBetaTracks = false;
        private bool _showBetaWisps = false;
        private bool _showBetaIndicators = false;
        private bool _showBetaStructures = false;
        private bool _showBetaChests = false;
        private bool _showExits = true;
        private bool _showWispCages = true;
        private bool _showSmugglers = true;
        private bool _showTrackers = true;
        private bool _trackBear = true;
        private bool _trackWolf = true;
        private bool _trackPanther = true;
        private bool _trackHumanoid = true;
        private bool _trackElemental = true;
        private bool _trackEnt = true;
        private bool _trackImp = true;
        private bool _trackGolem = true;
        private bool _trackWerewolf = true;
        private bool _showAvalonianDungeons = true;
        private bool[] _showAvalonianTiers = { true, true, true, true, true, true, true, true, true };
        

        // --- HARVESTABLES ---
        private bool _showDungeonIcons = true;
        private bool _showSoloDungeons = true;
        private bool[] _showSoloEnchantments = new bool[] { true, true, true, true, true };
        private bool _showSoloBossLair = true;
        private bool _showGroupDungeons = true;
        private bool[] _showGroupEnchantments = new bool[] { true, true, true, true, true };
        private bool _showGroupBossLair = true;
        private bool _showCorruptedDungeons = true;
        private bool _showHellgateDungeons = true;

        private bool _showNormalMobs = true;
        private bool _showBosses = true;
        private bool _showHiddenChests = true;
        private bool _showChestIds = false;
        private static readonly HashSet<int> _hiddenChestIds = new HashSet<int> { 795, 798, 800, 2637 };
        private bool _showGuild = true;

        private bool IsWhitelisted(Player p, Player mainPlayer)
        {
            if (p == null) return false;
            if (mainPlayer != null && p.Id == mainPlayer.Id) return true;
            return _whitelist.Contains(p.Name);
        }
        private bool _showPlayerName = true;
        private bool _showPlayerCount = true;
        private bool _showMobNames = true;
        private int _developer = 0;  // Developer tabs: 0 = hidden, 1 = visible
        private bool _debugConsoleLog = false;
        private bool _debugMobs = false;
        private bool _debugStaticResources = false;
        private bool _enableLogging = false;

        private bool _enableSoundAlerts = true;
        private bool _enableToastAlerts = true;

        // StreamModule OBS / ekran yakalamadan gizleme
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

        private float _zoom = 2.50f;
        private float _radarOffsetX = 0.0f;
        private float _radarOffsetY = 0.0f;
        private float _globalIconSize = 28.0f;
        private float _bossIconSize = 36.0f;
        private float _renderDistance = 70.0f;

        private bool _invertX = false;
        private bool _invertY = false;
        private bool _swapXY = true;
        private float _radarRotation = -45.0f;

        private bool _showPlayerList = false;
        private bool _playerListMoveable = false;
        private float _playerListX = 300f;
        private float _playerListY = 600f;

        private bool _showItemIds = false;

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
        private bool _trackerShowMobIcons = true;      // Mob lazerinin ucunda ikon
        private bool _trackerShowResourceIcons = true; // Kaynak lazerinin ucunda ikon
        private HashSet<int> _trackerCustomMobs = new HashSet<int>();
        private string _trackerSearchQuery = "";
        private int _selectedMobIdForTracker = -1;


        private string _mobSearchQuery = "";
        private string _blacklistSearchQuery = "";
        private int _selectedMobIdForBlacklist = -1;
        private string _trackedListFilter = "";


        private string _whitelistInput = "";
        private HashSet<string> _whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _whitelistPath;

        // Assets
        private string _crownImagePath;
        private string _cageImagePath;
        private string _aspectBossIconPath;
        private string _spiderImagePath;
        private string[] _mistImagePaths = new string[5];
        private string _feyDragonPath;
        private string _griffinPath;
        private string _veilWeaverPath;

        internal Dictionary<int, MobInfo> _mobDatabase = new();
        private Dictionary<HarvestableCategory, bool> _resourceMasterToggles = new();
        private Dictionary<HarvestableCategory, bool[,]> _resourceFilters = new();

        private static readonly Regex _tierRegex = new Regex(@"T(\d+)_", RegexOptions.Compiled);
        private static readonly Regex _enchantRegex = new Regex(@"LEVEL(\d+)|@(\d+)", RegexOptions.Compiled);
        private int _resourceTruthMode = 0; // 0=Name First, 1=Network First, 2=Metadata First
        private int _lastEnemyCount = 0;
        private DateTime _enemyCountLastUpdated = DateTime.MinValue;
        private float _enemyCountHoldSeconds = 1.5f;
        private DateTime _lastBeepTime = DateTime.MinValue;
        // Toast bildirim sistemi
        private readonly List<(string msg, DateTime time, uint color)> _toasts = new();
        // Yaklaşan düşman yön göstergesi (ekran kenarı ok)
        private bool _showDangerCompass = true;

        // Performans: Random sınıf seviyesinde bir kez oluşturulur
        private static readonly Random _rng = new Random();

        // Performans: GetSystemMetrics çağrıları cache'lenir (her frame sistem çaÄrısı yapılmaz)
        private int _cachedPrimaryScreenW = 0;
        private int _cachedPrimaryScreenH = 0;
        #endregion
    }
}







