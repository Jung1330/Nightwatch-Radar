<div align="center">

<br/>

# 🌙 NIGHTWATCH

**Real-time radar overlay for Albion Online**

<br/>

[![Windows](https://img.shields.io/badge/Windows_10%2F11-0a0a0a?style=for-the-badge&logo=windows11&logoColor=6C5CE7)](https://github.com/Jung1330/Nightwatch-Radar)
[![C#](https://img.shields.io/badge/C%23_.NET4/.NET_8-0a0a0a?style=for-the-badge&logo=dotnet&logoColor=A29BFE)](https://dotnet.microsoft.com)
[![ImGui](https://img.shields.io/badge/Dear_ImGui-0a0a0a?style=for-the-badge&logo=imgui&logoColor=00CEC9)](https://github.com/ocornut/imgui)
[![Version](https://img.shields.io/badge/v1.5.2-0a0a0a?style=for-the-badge&logoColor=white)](#)
[![License](https://img.shields.io/badge/MIT-0a0a0a?style=for-the-badge)](#)

<br/>

<img src="https://github.com/user-attachments/assets/9e85a914-9d49-4783-929b-c30273c16e25" width="85%" alt="Nightwatch Radar Preview"/>

<br/><br/>

[`Features`](#features) ·
[`Install`](#installation) ·
[`Usage`](#usage) ·
[`Architecture`](#architecture) ·
[`Türkçe`](#türkçe)

<br/>

</div>

---

## 📖 About

**Nightwatch** is a transparent overlay that sits on top of your Albion Online game window. It passively captures and decodes network packets using **SharpPcap/Npcap** to extract real-time positional data — then renders a live minimap via **Dear ImGui** showing players, mobs, resources, mist portals, dungeons, and more.

Built as a multi-project C# solution targeting **.NET 8.0**, the application is structured around a clean event-driven pipeline: packet capture → protocol parsing → handler dispatch → state management → overlay rendering.

---

## <a id="features"></a> ✨ Features

<table>
<tr>
<td width="50%">

### 🗺️ Radar & Minimap
Real-time minimap synced to the game world. Distance rings at 50m / 100m / 150m. Custom zoom, rotation, and calibration. Active sniff-range indicator.

### 🐉 Mob Tracking
All hostile mobs on radar. Boss & Aspect mobs marked with crown icons. Built-in mob database browser (ID/name search). Blacklist system. Custom PNG icons for Crystal Spider, Fairy Dragon, Griffin, and Veil Weaver.

### ⛏️ Resource Mapping
Full support: Ore, Stone, Fiber, Hide, Logs. Tier (T1–T8) and enchant (.0–.4) filter matrix. Living resource detection (Elementals, Stags). Enchant-specific colored icons. Tracker Only mode.

</td>
<td width="50%">

### 🌫️ Mist & Portal Detection
Rarity-based coloring (Common → Legendary). Duo Mist support. Hidden Chest alerts.

### 🎯 Laser System
Directional lasers pointing to resources, VIP mobs, or normal mobs. Per-target color selection. Full X/Y scale and endpoint calibration.

### ⚙️ System
JSON config profiles with autoload. OBS Bypass / Streamer Mode (Win11). DPI-Aware (Per Monitor V2). System tray background mode. Customizable hotkeys. Three UI themes: **DeepSpace Black**, **Obsidian**, **BloodMoon**.

### 🛠️ Dev Tools
Mob & resource simulator. Raw packet parser/diff. Pointer scanner. Color-coded live UI console with log export.

</td>
</tr>
</table>

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
> Hotkeys are fully configurable in **Settings → Hotkey**.

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
│   ├── Program.cs                 ← Entry point, engine bootstrap
│   ├── PacketEngine.cs            ← SharpPcap capture → parser bridge
│   ├── Settings.cs                ← Runtime configuration
│   ├── Managers/
│   │   ├── GameStateManager.cs    ← Central state store (mobs, players, resources)
│   │   └── ErrorCodeSink.cs       ← Global error handler
│   ├── Entities/
│   │   └── PlayerEntity.cs        ← Player data model
│   ├── UserControls/
│   │   ├── AlbionOverlay.cs       ← Overlay window (ClickableTransparentOverlay)
│   │   ├── MentalityTheme.cs      ← ImGui theme engine (3 themes, animated widgets)
│   │   ├── UIConsole.cs           ← In-app color-coded console
│   │   └── AlbionOverlay/
│   │       ├── Core/              ← Core rendering logic
│   │       ├── Map/               ← Minimap rendering
│   │       ├── Modules/           ← Config, Assets, Data modules
│   │       └── ViewModels/        ← UI state bindings
│   ├── Mappers/                   ← Data mapping utilities
│   ├── ViewModels/                ← Shared view models
│   └── Assets/
│       ├── Helper/                ← Fonts (EN, RU, ZH)
│       ├── Language/              ← Localization (EN, TR, RU, ZH)
│       ├── Maps/                  ← Map image data
│       └── Resources/             ← 168 entity/resource PNG icons
│
├── AlbionDataHandlersNET8/        ← Packet event handlers
│   └── Handlers/
│       ├── MobsHandler
│       ├── PlayersHandler
│       ├── HarvestableHandler
│       ├── DungeonHandler
│       ├── MapHandler
│       └── UnknownPacketHandler
│
├── BaseUtilsNET8/                 ← Shared utilities and base classes
│
├── AOSnifferNET/                  ← Photon protocol parser (Protocol16/18)
│   └── PhotonPacketParser/        ← PhotonPackageParser.dll, Protocol16/18.dll
│
└── Extra/                         ← Legacy README archive
```

<br/>

**Data Flow:**

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
    ├──▶ DungeonHandler       ──▶ GameStateManager.UpdateDungeonsState()
    └──▶ MapChangeHandler     ──▶ GameStateManager.SetCurrentMap()
                                    │
                                    ▼
                              AlbionOverlay (ImGui render loop)
                                    │
                                    ├── Minimap + distance rings
                                    ├── Entity markers (mobs, players, resources)
                                    ├── Laser lines to targets
                                    └── UI panels (config, console, dev tools)
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

**Nightwatch**, Albion Online için geliştirilmiş gerçek zamanlı bir radar ve overlay aracıdır. Oyun penceresinin üzerine şeffaf bir katman olarak yerleşir, ağ trafiğini analiz ederek oyuncuları, yaratıkları, kaynakları, mist portallarını ve zindanları harita üzerinde gösterir.

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

### Kullanım

1. **Albion Online**'ı açın ve oyuna girin
2. **Nightwatch.exe**'yi Yönetici olarak çalıştırın
3. Radar otomatik olarak trafiği algılar ve overlay oyun penceresinin üzerine yerleşir

| Kısayol | İşlev |
|:---|:---|
| `F12` | Menüyü göster/gizle |
| `INSERT` | Sesi aç/kapat |

> Kısayollar **Settings → Hotkey** bölümünden değiştirilebilir.

#### VPN / ExitLag Kullanıcıları

1. **Device Info** sekmesine gidin
2. **Test Network Adapters** butonuna basın
3. **YES** etiketli adaptörü seçin
4. **Restart Application** ile yeniden başlatın

### Özellikler

- **Radar & Minimap** — Gerçek zamanlı minimap, mesafe halkaları, zoom ve kalibrasyon
- **Mob Takibi** — Tüm yaratıklar, boss/aspect taç ikonu, kara liste, özel PNG ikonları
- **Kaynak Takibi** — Ore/Stone/Fiber/Hide/Logs, T1-T8 & enchant 0-4 filtre matrisi
- **Mist & Portal** — Nadirlik renkli mist, duo mist, gizli sandık algılama
- **Lazer Sistemi** — Kaynak/mob yönlendirme lazerleri, bağımsız renk seçimi
- **Sistem** — JSON profil, autoconfig, OBS bypass, DPI-aware, system tray, 3 tema
- **Geliştirici Araçları** — Simülatör, paket parser, pointer scanner, UI konsol

</details>

---

<div align="center">

<br/>

**Nightwatch** — *Karanlıkta Gören Göz*

🌙

<br/>

</div>
