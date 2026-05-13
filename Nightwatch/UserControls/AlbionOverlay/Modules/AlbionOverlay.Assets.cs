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
        #region Resource Icon Management

        private string GetResourceImagePath(HarvestableCategory cat, int tier, int enchant)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string prefix = "";

            switch (cat)
            {
                case HarvestableCategory.Log: prefix = "log"; break;
                case HarvestableCategory.Ore: prefix = "ore"; break;
                case HarvestableCategory.Rock: prefix = "rock"; break;
                case HarvestableCategory.Fiber: prefix = "fiber"; break;
                case HarvestableCategory.Hide: prefix = "hide"; break;
                default: return "";
            }

            // Dosya adÄ± formatÄ±: Logs_4_0.png veya ore_5_1.png
            string fileName = $"{prefix}_{tier}_{enchant}.png";
            return System.IO.Path.Combine(baseDir, "Assets", "Resources", fileName);
        }

        #endregion

        #region Temp Picture


        private string GetResourceToTemp(System.Drawing.Bitmap resource, string name)
        {

            if (_resourceCache.TryGetValue(name, out string cachedPath))
                return cachedPath;

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Nightwatch_{name}.png");

            if (!File.Exists(tempPath))
            {
                resource.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                File.SetAttributes(tempPath, FileAttributes.Hidden);
            }

            _resourceCache[name] = tempPath;

            return tempPath;
        }


        #endregion

        #region Albion Render API – Item Görselleri
        // -----------------------------------------------------------------------
        // Her item için önce disk önbelleðine bakar.
        // Önbellekte yoksa arka planda indirir; indirme bitmeden null döner
        // (ImGui o frame'de placeholder/fallback çizer, sonraki frame'de hazýr olur).
        // URL: https://render.albiononline.com/v1/item/{InternalName}.png?size=64&quality=0
        // -----------------------------------------------------------------------
        private const string RenderApiBase = "https://render.albiononline.com/v1/item/";
        private static readonly string _itemRenderCacheDir = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "ItemRenderCache");
        private static readonly System.Net.Http.HttpClient _renderHttpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        /// <summary>
        /// Verilen InternalName için disk yolunu döndürür.
        /// Dosya yoksa arka planda API'den indirir; hazýr deðilken null döner.
        /// </summary>
        private string GetItemRenderPath(string internalName)
        {
            if (string.IsNullOrEmpty(internalName) || internalName == "-") return null;

            if (!string.IsNullOrEmpty(internalName) &&
                (_itemRenderFailedAt.TryGetValue(internalName, out var lastFail)) &&
                (DateTime.Now - lastFail).TotalSeconds < 8)
            {
                return null;
            }

            // 1. Bellekte var mý?
            lock (_itemRenderCache)
            {
                if (_itemRenderCache.TryGetValue(internalName, out string cached)) return cached;
            }

            // 2. Disk önbelleðinde var mý?
            Directory.CreateDirectory(_itemRenderCacheDir);
            string diskPath = System.IO.Path.Combine(_itemRenderCacheDir, internalName + ".png");
            if (File.Exists(diskPath))
            {
                lock (_itemRenderCache)
                {
                    _itemRenderCache[internalName] = diskPath;
                }
                return diskPath;
            }

            // 3. Henüz indirilmiyorsa baþlat
            bool shouldStartDownload;
            lock (_itemRenderDownloading)
            {
                shouldStartDownload = _itemRenderDownloading.Add(internalName);
            }

            if (shouldStartDownload)
            {
                string url = $"{RenderApiBase}{internalName}.png?size=64&quality=0";
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        byte[] data = await _renderHttpClient.GetByteArrayAsync(url);
                        File.WriteAllBytes(diskPath, data);
                        lock (_itemRenderCache) { _itemRenderCache[internalName] = diskPath; }
                    }
                    catch (Exception ex)
                    {
                        lock (_itemRenderFailedAt) { _itemRenderFailedAt[internalName] = DateTime.Now; }
                        if ((DateTime.Now - _lastItemRenderErrorAt).TotalSeconds >= 5)
                        {
                            _lastItemRenderErrorAt = DateTime.Now;
                            System.Console.WriteLine($"Error Code : 39 | {ex.Message}");
                        }
                        /* Ýndirme baþarýsýz › bir sonraki frame tekrar denenecek */
                    }
                    finally { lock (_itemRenderDownloading) { _itemRenderDownloading.Remove(internalName); } }
                });
            }

            return null; // Ýndirme tamamlanmadý, bu frame fallback kullan
        }

        /// <summary>
        /// Equipment array indeksi için InternalName döndürür.
        /// p.Equipment[idx] == 0 ise null döner.
        /// </summary>
        private string GetEquipInternalName(Player p, int idx)
        {
            if (p?.Equipment == null || p.Equipment.Length <= idx) return null;
            int id = p.Equipment[idx];
            if (id <= 0) return null;
            if (_itemDatabase.TryGetValue(id, out ItemInfo info) && !string.IsNullOrEmpty(info.InternalName))
                return info.InternalName;
            return null;
        }

        #endregion

        #region Tier Color
        // ========================================================
        // --- ALBION TIER RENK MOTORU ---
        // ========================================================
        private uint GetTierColor(int tier) => GetTierEnchantColor(tier, 0);

        /// <summary>Her tier+enchant kombinasyonu iÃ§in benzersiz renk dÃ¶ndÃ¼rÃ¼r.</summary>
        private uint GetTierEnchantColor(int tier, int enchant)
        {
            // Her satÄ±r: (tier, enchant) â†’ renk
            // Enchant arttÄ±kÃ§a ton kayar â€” rahatÃ§a ayÄ±rt edilebilir
            Vector4 c = (tier, Math.Min(enchant, 3)) switch
            {
                // T1: Gri tonlarÄ±
                (1, 0) => new Vector4(0.67f, 0.67f, 0.67f, 1f), // T1.0 koyu gri
                (1, 1) => new Vector4(0.80f, 0.80f, 0.80f, 1f), // T1.1 aÃ§Ä±k gri
                (1, 2) => new Vector4(0.88f, 0.88f, 1.00f, 1f), // T1.2 mavi-beyaz
                (1, 3) => new Vector4(1.00f, 1.00f, 1.00f, 1f), // T1.3 saf beyaz
                // T2: Kahve tonlarÄ±
                (2, 0) => new Vector4(0.55f, 0.40f, 0.26f, 1f), // T2.0 kahverengi
                (2, 1) => new Vector4(0.70f, 0.50f, 0.30f, 1f), // T2.1 aÃ§Ä±k kahve
                (2, 2) => new Vector4(0.82f, 0.62f, 0.38f, 1f), // T2.2 tan
                (2, 3) => new Vector4(0.94f, 0.80f, 0.53f, 1f), // T2.3 wheat/buÄŸday
                // T3: YeÅŸil tonlarÄ±
                (3, 0) => new Vector4(0.20f, 0.80f, 0.20f, 1f), // T3.0 yeÅŸil
                (3, 1) => new Vector4(0.40f, 0.90f, 0.25f, 1f), // T3.1 sarÄ±-yeÅŸil
                (3, 2) => new Vector4(0.00f, 1.00f, 0.50f, 1f), // T3.2 bahar yeÅŸili
                (3, 3) => new Vector4(0.75f, 1.00f, 0.00f, 1f), // T3.3 lime / neon yeÅŸil
                // T4: Mavi tonlarÄ±
                (4, 0) => new Vector4(0.12f, 0.56f, 1.00f, 1f), // T4.0 mavi
                (4, 1) => new Vector4(0.00f, 0.75f, 1.00f, 1f), // T4.1 aÃ§Ä±k mavi
                (4, 2) => new Vector4(0.00f, 0.88f, 1.00f, 1f), // T4.2 cyan-mavi
                (4, 3) => new Vector4(0.00f, 1.00f, 1.00f, 1f), // T4.3 tam cyan
                // T5: KÄ±rmÄ±zÄ± â†’ Turuncu â†’ Pembe â†’ Magenta
                (5, 0) => new Vector4(1.00f, 0.20f, 0.20f, 1f), // T5.0 kÄ±rmÄ±zÄ±
                (5, 1) => new Vector4(1.00f, 0.50f, 0.00f, 1f), // T5.1 turuncu
                (5, 2) => new Vector4(1.00f, 0.20f, 0.65f, 1f), // T5.2 rose/pembe
                (5, 3) => new Vector4(1.00f, 0.00f, 1.00f, 1f), // T5.3 magenta
                // T6: Turuncu â†’ BaÅŸka tonlar
                (6, 0) => new Vector4(1.00f, 0.50f, 0.00f, 1f), // T6.0 turuncu
                (6, 1) => new Vector4(1.00f, 0.65f, 0.20f, 1f), // T6.1 aÃ§Ä±k turuncu
                (6, 2) => new Vector4(1.00f, 0.82f, 0.00f, 1f), // T6.2 altÄ±n-turuncu
                (6, 3) => new Vector4(1.00f, 0.95f, 0.30f, 1f), // T6.3 sarÄ±-altÄ±n
                // T7: SarÄ± tonlarÄ±
                (7, 0) => new Vector4(1.00f, 0.90f, 0.00f, 1f), // T7.0 sarÄ±
                (7, 1) => new Vector4(1.00f, 0.95f, 0.35f, 1f), // T7.1 parlak sarÄ±
                (7, 2) => new Vector4(0.95f, 1.00f, 0.00f, 1f), // T7.2 sarÄ±-lime
                (7, 3) => new Vector4(0.78f, 1.00f, 0.00f, 1f), // T7.3 lime-sarÄ±
                // T8: Beyaz â†’ Mor tonlarÄ±
                (8, 0) => new Vector4(1.00f, 1.00f, 1.00f, 1f), // T8.0 saf beyaz
                (8, 1) => new Vector4(1.00f, 0.78f, 1.00f, 1f), // T8.1 pembe-beyaz
                (8, 2) => new Vector4(0.80f, 0.60f, 1.00f, 1f), // T8.2 lavanta
                (8, 3) => new Vector4(0.65f, 0.40f, 1.00f, 1f), // T8.3 mor
                _     => new Vector4(1.00f, 0.00f, 1.00f, 1f),  // Bilinmeyen: Mor
            };
            return ImGui.ColorConvertFloat4ToU32(c);
        }
        #endregion

    }
}


