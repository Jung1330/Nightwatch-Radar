# Nightwatch To-Do List & Technical Debt

## 📊 Öncelik Sırası

### P0 - Kritik (Şu An Bozuk)
- **[TÜM P0 HATALAR ÇÖZÜLDÜ]** Artık P0 listesi boş.

### P1 - Yüksek (Kısa Vadede Düzeltilmeli)
- **[TÜM P1 HATALAR ÇÖZÜLDÜ]** Artık P1 listesi boş.

### P2 - Orta (Teknik Borç)
- **[TÜM UYGULANABİLİR P2 HATALAR ÇÖZÜLDÜ]** Dead Code ve Exception logları temizlendi.
- *XOR Encrypted Movement:* (Atlandı - Güncel şifre/opcode key'i eksik).

### P3 - Düşük (Uzun Vadeli Refactoring)
- *God Class problemi (`AlbionOverlay`)*: (Atlandı - Aşırı yüksek risk, projenin tamamen kırılma ihtimali var).
- *Magic number'ları sabitlere çevir (Map IDs, Colors)*: (Atlandı - Yüksek efor / Düşük getiri).
- *DTO/Immutable struct geçişi*: (İptal / Mevcut lock sistemi yeterli görüldü).

---

## 🛠️ Çözülmesi Gereken Detaylar

### 🏗️ Architecture & Refactoring (Technical Debt)

- **Dead Code Yükü:**
  - **Issue:** `ValidatePort()`, `DiscoverAlbionPort()`, `GetDistanceSquared()` vb. 7 metod ve büyük comment blokları (Eski Player render vb.) ölü kod.
  - **Fix:** Kullanılmayan metod ve field'ları tamamen temizle. (EncryptKey/DecryptKey de kullanılmıyorsa dead code temizliğine dahil edilmeli).
  - **Durum:** [ÇÖZÜLDÜ]

- **The "God Class" Problem (`AlbionOverlay`):**
  - **Issue:** 15+ partial class ile rendering, config, network hepsi iç içe.
  - **Fix:** `ConfigManager`, `RadarRenderer` gibi bağımsız servislere ayır.
  - **Durum:** [ATLANDI - RİSKLİ] IDE araçları olmadan manuel refactoring 10.000 satırlık projeyi kırabilir.

- **"Swallowed" Exceptions (Blind Catch Blocks):**
  - **Issue:** 50'den fazla `catch (Exception ex)` bloku hatayı gizliyor.
  - **Fix:** Hepsini merkezi `UIConsole.Log()` sistemine bağla.
  - **Durum:** [ÇÖZÜLDÜ] Script ile hepsi UIConsole'a bağlandı.

---

## ✅ Zaten Çözülenler (Tamamlananlar ve Kararlar)

- **[ÇÖZÜLDÜ] P1 - Mist/Open World Ghost Players (Timeout):**
  - **Yapılan:** `PlayersHandler.cs` (AlbionDataHandlersNET8) içerisine saniye bazlı son görülme (`_lastSeenById`) mantığı eklendi. 15 saniyeden uzun süre hareket etmez ve yeni veri gelmezse, sunucudan `Leave` paketi beklenmeden otomatik temizleniyor.

- **[ÇÖZÜLDÜ] P1 - Ekran Çözünürlüğü Değişince Bozulan Radar:**
  - **Yapılan:** `AlbionOverlay.Render.cs` içine sadece 2 saniyede bir çalışan ekran boyutu kontrolü (`GetSystemMetrics`) eklendi. Ekran boyutu değişirse UI layout'u anında yeniden hesaplanıyor.

- **[ÇÖZÜLDÜ] P1 - _resourceTruthMode Seçimi Hiçbir Şey Yapmıyor:**
  - **Yapılan:** `AlbionOverlay.ViewModels.Mobs.cs` ve `Harvestables.cs` üzerinde tier hesaplama kuralları `Name First / Network First / Metadata First` olacak şekilde ayrıldı.

- **[ÇÖZÜLDÜ] P1 - _trackerEnableNormalMobs Implement Edilmemiş:**
  - **Yapılan:** `AlbionOverlay.Minimap.cs`'deki `DrawRadarDot` çağrısına eksik olan `showOffScreenArrow` ve `hideMarker` parametreleri aktarıldı, normal moblar artık lazer tracker ile çalışıyor.

- **[ÇÖZÜLDÜ] P1 - Çift Harita Temizleme Problemi:**
  - **Yapılan:** `PacketEngine.cs` içindeki yedek ve sorunlu olan `InternalMapHandler` sınıfı tamamen silindi. Paket kuyruğunu boşaltma komutu (`PurgeQueue`), doğrudan `Program.cs` içindeki ana harita yöneticisine (`MapChangeHandler`) bağlandı. Artık harita değiştiğinde önce biriken paketler temizleniyor, sonra RAM boşaltılıyor. Çakışma sorunu bitti.

- **[ÇÖZÜLDÜ] P0 - _aspectBossIconPath Hatası:**
  - **Yapılan:** `AlbionOverlay.cs` constructor'ı içerisindeki yanlışlıkla yorum satırına alınan atama satırı düzeltildi. Artık World/Aspect Boss ikonları doğru çizilecek.

- **[ÇÖZÜLDÜ] P0 - Config Yüklenince Tema Uygulanmama Hatası:**
  - **Yapılan:** `LoadConfig()` (`AlbionOverlay.Config.cs`) sonuna `MentalityTheme.SetTheme()` tetiklemesi eklendi. Config yüklenince sadece Index numarası değil, uygulamanın rengi de anında değişecek.

- **[ÇÖZÜLDÜ] CurrentMapId Race Condition:**
  - **Yapılan:** `SetCurrentMap()` metodunun tamamı `_stateLock` içine alındı.

- **[ÇÖZÜLDÜ] _imageCache Memory Leak:**
  - **Yapılan:** `AlbionOverlay.Render.cs` içinde harita değişimi tetiklendiğinde `ClearImageCache()` çağrısı eklendi.
  - **Kalan Risk:** Cache'in harita değişimi dışında (aynı haritada çok uzun süre kalındığında) büyümesi hâlâ mümkün, periyodik temizlik yok.

- **[ÇÖZÜLDÜ] _playerTrails Memory Leak (Harita Değişimi):**
  - **Yapılan:** Harita değişince tamamen sıfırlanması sağlandı.
  - **Kalan Risk:** `RemovePlayer()` çağrıldığında trail temizlenmiyor. Aynı haritada portaldan geçip yeni oyuncular geldiğinde eski trail'ler birikmeye devam eder. (ToDo listesindeki Ghost Player Timeout mantığıyla beraber `RemovePlayer` güncellenmeli).

- **[ÇÖZÜLDÜ] AES Anahtarı:**
  - **Yapılan:** Sabit `_aesKey` silindi, DPAPI implementasyonu yazıldı.
  - **Kontrol Et:** `EncryptKey`/`DecryptKey` çağrıları gerçekten var mı yoksa dead code mu? (Eğer kullanılmıyorsa Dead Code listesine dahil edilmeli).

- **[ÇÖZÜLDÜ] _itemRenderCache Sınırsız Büyüyor:**
  - **Yapılan:** Maksimum 500 görsel limitine (LRU Cache mantığı) uyarlandı.

- **[BİLİNÇLİ KARAR] BlockingCollection Limitsiz:**
  - **Sebep:** Tüm paketlerin görülmesi isteniyor.
  - **Kabul Edilen Risk:** Yoğun trafikte RAM artışı.
  - **Önlem:** Üretim öncesi monitör edilmeli.
