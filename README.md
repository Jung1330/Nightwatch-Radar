<div align="center">

<br/>

# 🌙 NIGHTWATCH

**Real-time radar overlay for Albion Online**

<br/>

[![Windows](https://img.shields.io/badge/Windows_10%2F11-0a0a0a?style=for-the-badge&logo=windows11&logoColor=6C5CE7)](https://github.com/Jung1330/Nightwatch-Radar)
[![C#](https://img.shields.io/badge/C%23_.NET_8-0a0a0a?style=for-the-badge&logo=dotnet&logoColor=A29BFE)](https://dotnet.microsoft.com)
[![ImGui](https://img.shields.io/badge/Dear_ImGui-0a0a0a?style=for-the-badge&logo=imgui&logoColor=00CEC9)](https://github.com/ocornut/imgui)
[![Version](https://img.shields.io/badge/v1.5.5-0a0a0a?style=for-the-badge&logoColor=white)](#)
[![License](https://img.shields.io/badge/MIT-0a0a0a?style=for-the-badge)](#)

<br/>

<img src="https://github.com/user-attachments/assets/16a650bd-a754-4fc9-b9b1-2cf4fc999212" width="85%" alt="Nightwatch Radar Preview"/>

<br/><br/>

[`Features`](#features) ·
[`Data`](#data) ·
[`Install`](#installation) ·
[`Usage`](#usage) ·
[`Architecture`](#architecture) ·
[`Türkçe`](#türkçe)

<br/>

</div>

---

## 📖 About

**Nightwatch** is a transparent overlay that sits on top of your Albion Online game window. It passively captures and decodes network packets using **SharpPcap/Npcap** to extract real-time positional data — then renders a live minimap via **Dear ImGui** showing players, mobs, resources, mist portals, dungeons, and more.

No injection, no memory reading, no proxy: the game client is never touched. The whole radar is built on **passive packet analysis**.

---

## 🆕 What's New in v1.5.5

| | |
|:---|:---|
| 🗺️ | **Roads of Avalon** maps fully integrated and visible on the radar |
| 🧭 | **Compass directions** added to the overlay interface ([#26](https://github.com/Jung1330/Nightwatch-Radar/issues/26)) |
| 🏝️ | **106 new maps** extracted from the game client — islands, arenas, Crystal Realm, Conqueror's Hall |
| 🔍 | **51 maps upgraded** to higher resolution (1024×1024) |
| 🏰 | **Mists dungeon (Knightfall Abbey)** now renders with its real map |
| 🔧 | Fixed: "Chestnut Spirit" mob shown as "Chest" · Mist Cage detection/display · Tracker drawing a Crown icon |

---

## <a id="features"></a> ✨ Features

<table>
<tr>
<td width="50%">

### 🗺️ Radar & Minimap
Real-time minimap synced to the game world. Distance rings at 50m / 100m / 150m. Custom zoom, rotation, and calibration. Active sniff-range indicator.

### 🏝️ Map Archive (992 maps)
Every world region, island, arena, Crystal Realm and Conqueror's Hall — extracted straight from the game's own assets, so nothing is guessed. Includes the **Mists dungeon (Knightfall Abbey)** with its real static layout.

### 🐉 Mob Tracking
All hostile mobs on radar. Boss & Aspect mobs marked with crown icons. Built-in mob database browser (ID/name search). Blacklist system. Custom PNG icons for Crystal Spider, Fairy Dragon, Griffin, and Veil Weaver.

### ⛏️ Resource Mapping
Full support: Ore, Stone, Fiber, Hide, Logs. Tier (T1–T8) and enchant (.0–.4) filter matrix. Living resource detection (Elementals, Stags). Enchant-specific colored icons. Tracker Only mode.

</td>
<td width="50%">

### 🌫️ Mist & Portal Detection
Rarity-based coloring (Common → Legendary). Duo Mist support. Hidden Chest alerts. Open Mists instances are detected without drawing a wrong map — only the true static layouts (like Knightfall Abbey) are rendered.

### 🎯 Laser System
Directional lasers pointing to resources, VIP mobs, or normal mobs. Per-target color selection. Full X/Y scale and endpoint calibration.

### ⚙️ System
JSON config profiles with autoload. OBS Bypass / Streamer Mode (Win11). DPI-Aware (Per Monitor V2). System tray background mode. Customizable hotkeys. In-app **update check**. Three UI themes: **DeepSpace Black**, **Obsidian**, **BloodMoon**.

### 🛠️ Dev Tools
Mob & resource simulator. Raw packet parser/diff. Pointer scanner. Color-coded live UI console with log export.

</td>
</tr>
</table>

---

## <a id="data"></a> 📊 Data & Coverage

| Metric | Value |
|:---|:---|
| 🎮 Game events mapped | **702** |
| ⚡ Operation codes mapped | **784** |
| 🗺️ Map images | **992** (1024×1024, extracted from the game client) |
| 🧩 Entity / resource icons | **169** PNG |
| 🌍 Interface languages | **4** (EN · TR · RU · ZH) |
| 📦 Parameter table | 1192 code → field index + meaning, with *live verified* tags |

---

## <a id="installation"></a> 📦 Installation

#### Requirements

| | Requirement | Details |
|:---|:---|:---|
| 💻 | **OS** | Windows 10 / 11 (64-bit) |
| ⚡ | **Runtime** | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 🔌 | **Packet Driver** | [Npcap](https://npcap.com/#download) (latest) |
| 🔑 | **Privileges** | Administrator |

#### Steps

```
1.  Install Npcap  →  Enable "WinPcap API-compatible Mode" during setup
2.  Download latest release from GitHub Releases
3.  Extract & run Nightwatch.exe as Administrator
```

> [!IMPORTANT]
> Npcap must be installed with **WinPcap API-compatible Mode** checked, otherwise SharpPcap cannot bind to the network adapter.

> [!NOTE]
> On startup the app checks `App/version.txt` on the `Website` branch and notifies you when a newer build is published.

---

## <a id="usage"></a> 🖥️ Usage

1. Launch **Albion Online** and enter the game world
2. Run **Nightwatch.exe** as Administrator
3. The overlay attaches automatically — the radar begins capturing packets

#### Default Hotkeys

| Key | Action |
|:---|:---|
| `F12` | Toggle overlay menu |
| `INSERT` | Toggle sound alerts |

> [!TIP]
> Hotkeys are fully configurable in **Settings → Hotkey** (show/hide menu, mute, hide all).

#### VPN / ExitLag Users

If your traffic routes through a VPN or game booster:

1. Open overlay → **Device Info** tab
2. Click **Test Network Adapters**
3. Select the adapter marked **YES**
4. Click **Restart Application**

---

## <a id="architecture"></a> 🏗️ Architecture

<details>
<summary><b>Project Structure & Data Flow</b></summary>

<br/>

```
Nightwatch.sln
│
├── Nightwatch/                    ← Main overlay application (.NET 8, WinExe)
│   ├── Program.cs                 ← Entry point, engine bootstrap, update check
│   ├── PacketEngine.cs            ← SharpPcap capture → parser bridge
│   ├── Managers/
│   │   ├── GameStateManager.cs    ← Central state store (mobs, players, resources)
│   │   └── ErrorCodeSink.cs       ← Global error handler
│   ├── UserControls/
│   │   ├── AlbionOverlay.cs       ← Overlay window (ClickableTransparentOverlay)
│   │   ├── MentalityTheme.cs      ← ImGui theme engine (3 themes, animated widgets)
│   │   ├── UIConsole.cs           ← In-app color-coded console
│   │   └── AlbionOverlay/
│   │       ├── Core/              ← Core rendering logic
│   │       ├── Map/               ← Minimap rendering + map resolution
│   │       ├── Modules/           ← Config, Assets, Data modules
│   │       └── ViewModels/        ← UI state bindings
│   └── Assets/
│       ├── Helper/                ← Fonts + generated game data
│       ├── Language/              ← Localization (EN, TR, RU, ZH)
│       ├── Maps/                  ← 992 map images (1024×1024)
│       └── Resources/             ← 169 entity/resource PNG icons
│
├── AlbionDataHandlersNET8/        ← Packet event handlers
│   ├── Handlers/
│   │   ├── ChestHandler
│   │   ├── DungeonHandler
│   │   ├── HarvestableHandler
│   │   ├── MapHandler
│   │   ├── MobsHandler
│   │   ├── PlayersHandler
│   │   └── UnknownPacketHandler
│   └── Packets/                   ← AUTO-GENERATED models
│       ├── Events.g.cs            ← 702 event classes
│       ├── Operations.g.cs        ← 784 operation variants
│       └── PacketMeta.g.cs        ← code → field index → meaning table
│
├── BaseUtilsNET8/                 ← Shared utilities and base classes
│
└── Libs/                          ← Photon protocol parser (closed source DLLs)
    ├── PhotonPackageParser.dll
    ├── Protocol16.dll
    └── Protocol18.dll
```

<br/>

**Runtime Data Flow:**

```
Network Adapter (Npcap)
    │
    ▼
PacketEngine.cs ──── SharpPcap capture loop
    │
    ▼
AlbionDataParser ──── Photon protocol decode (Protocol16 / Protocol18)
    │
    ├──▶ MobsHandler         ──▶ GameStateManager.UpdateMobsState()
    ├──▶ PlayersHandler       ──▶ GameStateManager.UpdateLocalPlayer()
    │                              GameStateManager.UpdateOtherPlayers()
    ├──▶ HarvestableHandler   ──▶ GameStateManager.UpdateHarvestablesState()
    ├──▶ ChestHandler         ──▶ GameStateManager.UpdateChestsState()
    ├──▶ DungeonHandler       ──▶ GameStateManager.UpdateDungeonsState()
    └──▶ MapChangeHandler     ──▶ GameStateManager.SetCurrentMap()
                                    │
                                    ▼
                              AlbionOverlay (ImGui render loop)
                                    │
                                    ├── Minimap + distance rings + map background
                                    ├── Entity markers (mobs, players, resources, chests)
                                    ├── Laser lines to targets
                                    └── UI panels (config, console, dev tools)
```

**Model Generation Pipeline:**

```
Extra/dump.cs (IL2CPP)  ──▶  Extra/analiz.py  ──▶  Events.g.cs · Operations.g.cs · PacketMeta.g.cs
                                                   (code → parameter index → meaning, LIVE tags)

Update.py  ──▶  find dump → diff vs previous dump → tables → symbols → helper data → publish .exe
```

**Release Checklist:**

```
1.  Directory.Build.props          → bump <Version>            (single source of truth)
2.  dotnet publish -c Release      → build Nightwatch.exe
3.  Extra/Site/App/version.txt     → write the SAME number      (in-app update check)
4.  Extra/Site/index.html          → update version + changelog (4 languages)
5.  Push Extra/Site to the "Website" branch, publish the release
```

</details>

---

## 🎨 Themes

The UI ships with three hand-crafted ImGui themes, each with animated widgets, glow effects, and gradient separators:

| Theme | Accent | Style |
|:---|:---|:---|
| **DeepSpace Black** | `#6C5CE7` Purple | Ultra-dark, neon purple glow |
| **Obsidian** | `#FFB86C` Amber | Dark, warm amber highlights |
| **BloodMoon** | `#DC3545` Crimson | Deep red, aggressive |

---

## 🌍 Language Support

| Language | File | Status |
|:---|:---:|:---:|
| 🇬🇧 English | `EN.json` | ✅ |
| 🇹🇷 Türkçe | `TR.json` | ✅ |
| 🇷🇺 Русский | `RU.json` | ✅ |
| 🇨🇳 中文 | `ZH.json` | ✅ |

Change via **Settings → Language** in the overlay.

---

## <a id="türkçe"></a> 🇹🇷 Türkçe

<details>
<summary><b>Türkçe Dokümantasyon</b></summary>

<br/>

### Hakkında

**Nightwatch**, Albion Online için geliştirilmiş gerçek zamanlı bir radar ve overlay aracıdır. Oyun penceresinin üzerine şeffaf bir katman olarak yerleşir, ağ trafiğini **pasif olarak** dinleyip çözerek oyuncuları, yaratıkları, kaynakları, mist portallarını ve zindanları harita üzerinde gösterir.

**Injection yok, bellek okuma yok, proxy yok** — oyun istemcisine hiç dokunulmaz.

### 🆕 v1.5.5 Yenilikleri

- **Avalon Yolları** haritaları tamamen entegre ve radarda görünür
- Arayüze **pusula yönleri** eklendi (#26)
- Oyundan çıkarılan **106 yeni harita**: adalar, arenalar, Crystal Realm, Conqueror's Hall
- **51 harita 1024×1024'e** yükseltildi
- **Mists zindanı (Knightfall Abbey)** artık gerçek haritasıyla çiziliyor
- Düzeltmeler: "Chestnut Spirit" → "Chest" hatası · Mist kafesleri · Tracker'ın taç ikonu çizmesi

### 📊 Veri ve Kapsam

| Ölçüt | Değer |
|:---|:---|
| Eşlenen oyun olayı | **702** |
| Eşlenen operasyon kodu | **784** |
| Harita görseli | **992** (1024×1024, oyunun kendi asset'lerinden) |
| Varlık / kaynak ikonu | **169** PNG |
| Arayüz dili | **4** (EN · TR · RU · ZH) |
| Parametre tablosu | 1192 kod → alan indeksi + anlam (*CANLI doğrulanmış* etiketli) |

### Kurulum

| Gereksinim | Detay |
|:---|:---|
| İşletim Sistemi | Windows 10 / 11 (64-bit) |
| Runtime | .NET 8.0 Desktop Runtime |
| Sürücü | Npcap (son sürüm) |
| Yetki | Yönetici |

1. [npcap.com](https://npcap.com/#download) adresinden **Npcap** kurun — kurulumda **"WinPcap API-compatible Mode"** seçeneğini işaretleyin
2. [Releases](https://github.com/Jung1330/Nightwatch-Radar/releases) sayfasından son sürümü indirin
3. `Nightwatch.exe` dosyasını **Yönetici olarak** çalıştırın

> Uygulama açılışta yeni sürümü otomatik kontrol eder ve bildirir.

### Kullanım

1. **Albion Online**'ı açın ve oyuna girin
2. **Nightwatch.exe**'yi Yönetici olarak çalıştırın
3. Radar otomatik olarak trafiği algılar ve overlay oyun penceresinin üzerine yerleşir

| Kısayol | İşlev |
|:---|:---|
| `F12` | Menüyü göster/gizle |
| `INSERT` | Sesi aç/kapat |

> Kısayollar **Settings → Hotkey** bölümünden değiştirilebilir (menü, ses, tümünü gizle).

#### VPN / ExitLag Kullanıcıları

1. **Device Info** sekmesine gidin
2. **Test Network Adapters** butonuna basın
3. **YES** etiketli adaptörü seçin
4. **Restart Application** ile yeniden başlatın

### Özellikler

- **Radar & Minimap** — Gerçek zamanlı minimap, 50/100/150m mesafe halkaları, zoom, döndürme ve kalibrasyon
- **Harita Arşivi (992 harita)** — Tüm dünya bölgeleri, adalar, arenalar, Crystal Realm ve Conqueror's Hall; doğrudan oyunun asset'lerinden çıkarıldı. **Knightfall Abbey** gerçek statik layoutuyla çizilir
- **Mob Takibi** — Tüm yaratıklar, boss/aspect taç ikonu, mob veritabanı, kara liste, özel PNG ikonları
- **Kaynak Takibi** — Ore/Stone/Fiber/Hide/Logs, T1-T8 & enchant .0-.4 filtre matrisi, canlı kaynak tespiti
- **Mist & Portal** — Nadirlik renkli mist, duo mist, gizli sandık uyarısı. Açık Mists instance'ları için **yanlış harita çizilmez** (sunucuda üretiliyorlar)
- **Lazer Sistemi** — Kaynak/VIP/normal mob yönlendirme lazerleri, hedef başına renk, X/Y ölçek kalibrasyonu
- **Sistem** — JSON profil + autoconfig, OBS bypass (yayıncı modu), DPI-aware, sistem tepsisi, özelleştirilebilir kısayollar, **güncelleme kontrolü**, 3 tema (DeepSpace Black · Obsidian · BloodMoon)
- **Geliştirici Araçları** — Mob/kaynak simülatörü, ham paket parser/fark, pointer scanner, renkli canlı konsol

### Mimari (özet)

```
Nightwatch/                  → overlay uygulaması (Program.cs, PacketEngine.cs, Managers, UserControls, Assets)
AlbionDataHandlersNET8/      → 7 handler + OTOMATİK üretilen paket modelleri (Events.g.cs 702 · Operations.g.cs 784 · PacketMeta.g.cs)
BaseUtilsNET8/               → ortak yardımcılar
Libs/                        → PhotonPackageParser.dll · Protocol16.dll · Protocol18.dll
```

</details>

---

<div align="center">

<br/>

**Nightwatch** — *Karanlıkta Gören Göz*

🌙

<br/>

</div>
