using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        // ============================================================
        // V2 MAP SYSTEM - Test dosyası
        // Mevcut sistemi bozmadan bounds-tabanlı harita hizalama testi
        // ============================================================

        // --- V2 Ayrı Sözlükler (eski verilere dokunmaz) ---
        private Dictionary<string, float> _mapSizesV2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Vector2> _mapCentersV2 = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

        // --- V2 Toggle'lar ---
        private bool _useNewMapSystem = true;   // Eski/Yeni geçiş
        private bool _invertMapV = false;         // V eksenini ters çevir (test amaçlı)

        /// <summary>
        /// zones.json'dan bounds verisini okuyarak _mapSizesV2 ve _mapCentersV2'yi doldurur
        ///   extent = Math.max(bounds.max[0] - bounds.min[0], bounds.max[1] - bounds.min[1])
        ///   center = { x: (bounds.min[0] + bounds.max[0]) / 2, y: (bounds.min[1] + bounds.max[1]) / 2 }
        /// </summary>
        private void LoadZonesDatabaseV2()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "zones.json");
            if (!File.Exists(path))
            {
                Log("[MapV2] zones.json bulunamadı: " + path, LogLevel.Error);
                return;
            }

            try
            {
                _mapSizesV2.Clear();
                _mapCentersV2.Clear();

                string json = File.ReadAllText(path);
                JObject zonesObj = JObject.Parse(json);

                int boundsFound = 0;
                int fallbackUsed = 0;

                foreach (var property in zonesObj.Properties())
                {
                    string id = property.Name;
                    if (string.IsNullOrEmpty(id)) continue;

                    JToken item = property.Value;

                    // bounds verisini oku
                    JToken boundsToken = item["bounds"];
                    if (boundsToken != null)
                    {
                        JToken minToken = boundsToken["min"];
                        JToken maxToken = boundsToken["max"];

                        if (minToken != null && maxToken != null &&
                            minToken.Type == JTokenType.Array && maxToken.Type == JTokenType.Array)
                        {
                            var minArr = (JArray)minToken;
                            var maxArr = (JArray)maxToken;

                            if (minArr.Count >= 2 && maxArr.Count >= 2)
                            {
                                float minX = minArr[0].Value<float>();
                                float minY = minArr[1].Value<float>();
                                float maxX = maxArr[0].Value<float>();
                                float maxY = maxArr[1].Value<float>();

                                // OpenRadar formülü: extent = max(width, height)
                                float width = maxX - minX;
                                float height = maxY - minY;
                                float extent = Math.Max(width, height);

                                // OpenRadar formülü: center = midpoint
                                float centerX = (minX + maxX) / 2.0f;
                                float centerY = (minY + maxY) / 2.0f;

                                if (extent > 0)
                                {
                                    _mapSizesV2[id] = extent;
                                    _mapCentersV2[id] = new Vector2(centerX, centerY);
                                    boundsFound++;
                                    continue; // bounds bulundu, fallback'e gerek yok
                                }
                            }
                        }
                    }

                    // bounds yoksa veya geçersizse → eski sisteme benzer fallback
                    string type = item["type"]?.ToString().ToUpperInvariant() ?? "";
                    string name = item["name"]?.ToString().ToUpperInvariant() ?? "";

                    float size = 825.0f;
                    if (type.Contains("CITY") || name.Contains("CITY")) size = 800.0f;
                    else if (type.Contains("PORTAL") || name.Contains("PORTAL")) size = 800.0f;
                    else if (type.Contains("ISLAND") || name.Contains("ISLAND")) size = 500.0f;
                    else if (type.Contains("HIDEOUT") || name.Contains("HIDEOUT")) size = 400.0f;
                    else if (type.Contains("DUNGEON") || name.Contains("DNG") || name.Contains("HALL")) size = 350.0f;

                    _mapSizesV2[id] = size;
                    _mapCentersV2[id] = Vector2.Zero; // bounds olmayan haritalar (0,0) merkezli
                    fallbackUsed++;
                }

                Log($"[MapV2] Zones yüklendi: {boundsFound} bounds-tabanlı, {fallbackUsed} fallback", LogLevel.Success);
            }
            catch (Exception ex)
            {
                Log($"[MapV2] zones.json hata: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// V2 UV hesabı - OpenRadar'ın MapsDrawing.js mantığına uygun.
        /// 
        /// OpenRadar'da:
        ///   hX = lpX,  hY = -lpY   (Y ekseni ters)
        ///   adjX = (hX - center.x) * scaleFactor
        ///   adjY = (hY + center.y) * scaleFactor = (-lpY + center.y) * scaleFactor
        ///
        /// Bizde ScreenToWorld zaten swapXY/invertX/invertY uyguluyor, bu yüzden
        /// worldPos doğru dünya koordinatı. Sadece UV eşlemesi farklı.
        /// </summary>
        private Vector2 ScreenToWorldUV_V2(Vector2 screenPos, Vector2 center, Vector2 playerPos, float mapSize, Vector2 mapCenterOffset)
        {
            // ScreenToWorld ile aynı hesabı yapıyoruz (tam senkron kalması için)
            Vector2 rel = screenPos - center - new Vector2(_radarOffsetX, _radarOffsetY);
            rel /= _zoom;

            float angleRad = _radarRotation * (float)(Math.PI / 180.0);
            float invCos = (float)Math.Cos(-angleRad);
            float invSin = (float)Math.Sin(-angleRad);

            float unrotX = rel.X * invCos - rel.Y * invSin;
            float unrotY = rel.X * invSin + rel.Y * invCos;

            float dx = unrotX;
            float dy = unrotY;

            if (_invertY) dy = -dy;
            if (_invertX) dx = -dx;
            if (_swapXY) { float t = dx; dx = dy; dy = t; }

            float worldX = dx + playerPos.X;
            float worldY = dy + playerPos.Y;

            float worldMapSize = mapSize / _mapScale;

            // U hesabı: Her iki sistemde de aynı
            float u = ((worldX - mapCenterOffset.X - _mapGlobalOffsetX) / worldMapSize) + 0.5f;

            // V hesabı: Toggle'a göre eski veya ters
            float v;
            if (_invertMapV)
            {
                // OpenRadar stili: Y ekseni ters (hY = -lpY)
                // v = 0.5f - ((worldY - centerY) / size)
                v = 0.5f - ((worldY - mapCenterOffset.Y - _mapGlobalOffsetY) / worldMapSize);
            }
            else
            {
                // Mevcut (eski) formül
                v = ((worldY - mapCenterOffset.Y - _mapGlobalOffsetY) / worldMapSize) + 0.5f;
            }

            return new Vector2(u, v);
        }

        /// <summary>
        /// V2 sistemi için mapSize ve mapCenterOffset al.
        /// Toggle'a göre V1 veya V2 sözlüklerinden okur.
        /// </summary>
        private (float mapSize, Vector2 mapCenterOffset) GetMapParamsForCurrentSystem(string mapId)
        {
            if (_useNewMapSystem)
            {
                float size = _mapSizesV2.TryGetValue(mapId, out float s) ? s : 825.0f;
                Vector2 center = _mapCentersV2.TryGetValue(mapId, out Vector2 c) ? c : Vector2.Zero;
                return (size, center);
            }
            else
            {
                float size = _mapSizes.TryGetValue(mapId, out float s) ? s : 825.0f;
                Vector2 center = _mapCenters.TryGetValue(mapId, out Vector2 c) ? c : Vector2.Zero;
                return (size, center);
            }
        }
    }
}
