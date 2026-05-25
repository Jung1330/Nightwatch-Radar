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
        #region Map and Coordinate Conversion

        private string ResolveMapImagePath(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || mapId == "LEAVING_ZONE")
                mapId = "0000";

            if (_mapImagePathCache.TryGetValue(mapId, out var cached))
                return string.IsNullOrEmpty(cached) ? null : cached;

            string basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Maps", mapId + ".webp");
            if (IsImageExistsCached(basePath))
            {
                _mapImagePathCache[mapId] = basePath;
                return basePath;
            }

            int splitIdx = mapId.IndexOf('-');
            if (splitIdx > 0)
            {
                string prefix = mapId.Substring(0, splitIdx);
                string fallbackPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Maps", prefix + ".webp");
                if (IsImageExistsCached(fallbackPath))
                {
                    _mapImagePathCache[mapId] = fallbackPath;
                    return fallbackPath;
                }
            }

            _mapImagePathCache[mapId] = string.Empty;
            return null;
        }

        // EKRAN VE OYUN KOORDÄ°NATLARINI KUSURSUZ SENKRONÄ°ZE EDEN FONKSÄ°YONLAR
        private Vector2 WorldToScreen(Vector2 center, Vector2 worldPos, Vector2 playerPos)
        {
            // ORÄ°JÄ°NAL KODUNDAKÄ° SIRA GERÄ° GETÄ°RÄ°LDÄ° (SÄ°LÄ°NEN/DEÄzÄ°ÅzTÄ°RÄ°LEN KISIM DÃœZELTÄ°LDÄ°)
            float dx = worldPos.X - playerPos.X;
            float dy = worldPos.Y - playerPos.Y;

            if (_swapXY) { float t = dx; dx = dy; dy = t; }
            if (_invertX) dx = -dx;
            if (_invertY) dy = -dy;

            float angleRad = _radarRotation * (float)(Math.PI / 180.0);
            float s = (float)Math.Sin(angleRad);
            float c = (float)Math.Cos(angleRad);

            float rotX = dx * c - dy * s;
            float rotY = dx * s + dy * c;

            return center + new Vector2(rotX * _zoom, rotY * _zoom) + new Vector2(_radarOffsetX, _radarOffsetY);
        }

        private Vector2 ScreenToWorld(Vector2 screenPos, Vector2 center, Vector2 playerPos)
        {
            Vector2 rel = screenPos - center - new Vector2(_radarOffsetX, _radarOffsetY);
            rel /= _zoom;
            float angleRad = _radarRotation * (float)(Math.PI / 180.0);
            float iCos = (float)Math.Cos(-angleRad);
            float iSin = (float)Math.Sin(-angleRad);
            float unrotX = rel.X * iCos - rel.Y * iSin;
            float unrotY = rel.X * iSin + rel.Y * iCos;
            float dx = unrotX, dy = unrotY;
            if (_invertY) dy = -dy;
            if (_invertX) dx = -dx;
            if (_swapXY) { float t = dx; dx = dy; dy = t; }
            return new Vector2(dx + playerPos.X, dy + playerPos.Y);
        }

        private Vector2 ScreenToWorldUV(Vector2 screenPos, Vector2 center, Vector2 playerPos, float mapSize)
        {
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

            // HARÄ°TA BOYUTU ARTIK SABÄ°T 825 DEÄzÄ°L, DÄ°NAMÄ°K GELÄ°YOR
            float worldMapSize = mapSize / _mapScale;

            float u = ((worldX - _mapGlobalOffsetX) / worldMapSize) + 0.5f;
            float v = ((worldY - _mapGlobalOffsetY) / worldMapSize) + 0.5f;

            return new Vector2(u, v);
        }

        #endregion

        #region Draw Radar Methods
        private void DrawRadar(ImDrawListPtr drawList, Vector2 winPos, Vector2 winSize, Player mainPlayer)
        {
            Vector2 center = winPos + (winSize / 2.0f);
            float radiusLimit = (Math.Min(winSize.X, winSize.Y) / 2.0f) - 15.0f;

            /*drawList.AddLine(new Vector2(center.X, winPos.Y), new Vector2(center.X, winPos.Y + winSize.Y), 0x22FFFFFF);
            drawList.AddLine(new Vector2(winPos.X, center.Y), new Vector2(winPos.X + winSize.X, center.Y), 0x22FFFFFF);*/

            // --- YENÄ° DÄ°NAMÄ°K TEMA Ã‡Ä°ZGÄ°SÄ° ---
            Vector4 circleThemeCol = _selectedTheme == 1
                ? new Vector4(0.22f, 0.52f, 0.92f, 0.45f)  // Obsidian Blue
                : new Vector4(1.00f, 0.40f, 0.00f, 0.35f); // Original Turuncu

            // Ã‡emberi yeni rengiyle Ã§iziyoruz
            drawList.AddCircle(center, radiusLimit, ImGui.ColorConvertFloat4ToU32(circleThemeCol), 64, 2.0f);
            drawList.AddCircleFilled(center, radiusLimit, 0x09000000); // Çok hafif koyu zemin – oyun görünümü öncelikli

            // --- YENÄ° VE OPTÄ°MÄ°ZE EDÄ°LMÄ°Åz HARÄ°TA Ã‡Ä°ZÄ°MÄ° (KESÄ°N Ã‡Ã–ZÃœM) ---
            if (_showMapBackground && _gameStateManager != null)
            {
                var mp = _gameStateManager.GetPlayer();
                if (mp != null)
                {
                    string currentMapId = _gameStateManager.CurrentMapId ?? "0000";
                    if (currentMapId == "LEAVING_ZONE" || string.IsNullOrEmpty(currentMapId)) currentMapId = "0000";

                    string mapImagePath = ResolveMapImagePath(currentMapId);

                    if (!string.IsNullOrEmpty(mapImagePath))
                    {
                        try
                        {
                            AddOrGetImagePointer(mapImagePath, true, out IntPtr textureId, out uint imgW, out uint imgH);
                            if (textureId != IntPtr.Zero)
                            {
                                float currentMapSize = 825.0f; // Standart AÃ§Ä±k DÃ¼nya
                                string upperMapId = currentMapId.ToUpperInvariant();

                                // 1. Ã–NCELÄ°K: EÄŸer bu haritanÄ±n boyutu zones.json'dan okunduysa direkt onu kullan!
                                if (_mapSizes.TryGetValue(currentMapId, out float exactSize))
                                {
                                    currentMapSize = exactSize;
                                }
                                // 2. EÄzER JSON'DA YOKSA: Harita ismine bakarak tahmin et (Fallback - Ã‡Ã¶kme Ã–nleyici)
                                else
                                {
                                    if (upperMapId.StartsWith("DNG") || upperMapId.StartsWith("TNL") || upperMapId.StartsWith("PSG") || upperMapId.Contains("HALL"))
                                    {
                                        currentMapSize = 350.0f; // Zindan ve tÃ¼neller dar kalmalÄ±
                                    }
                                    else if (upperMapId.StartsWith("HIDEOUT"))
                                    {
                                        currentMapSize = 400.0f; // SÄ±ÄŸÄ±naklar biraz daha geniÅŸ
                                    }
                                    else if (upperMapId.Contains("CITY") || upperMapId.Contains("PORTAL"))
                                    {
                                        currentMapSize = 800.0f; // Ä°ÅzTE SENÄ°N Ã‡Ã–ZÃœMÃœN: Åzehir ve Portallar bÃ¼yÃ¼k kalacak, haritadan dÄ±ÅŸarÄ± taÅŸmayacaksÄ±n!
                                    }
                                }

                                Vector2 playerPos = new Vector2(mp.CurrentLerpedX, mp.CurrentLerpedY);
                                Vector2 centerUV = ScreenToWorldUV(center, center, playerPos, currentMapSize);

                                // Karakter haritanÄ±n iÃ§inde mi kontrolÃ¼
                                bool isCenterInside = centerUV.X >= 0.0f && centerUV.X <= 1.0f && centerUV.Y >= 0.0f && centerUV.Y <= 1.0f;

                                int num_segments = 64;
                                float angleStep = (float)(Math.PI * 2.0 / num_segments);

                                for (int i = 0; i < num_segments; i++)
                                {
                                    float a1 = i * angleStep;
                                    float a2 = (i + 1) * angleStep;

                                    Vector2 p1 = center + new Vector2((float)Math.Cos(a1) * radiusLimit, (float)Math.Sin(a1) * radiusLimit);
                                    Vector2 p2 = center + new Vector2((float)Math.Cos(a2) * radiusLimit, (float)Math.Sin(a2) * radiusLimit);

                                    Vector2 uv1 = ScreenToWorldUV(p1, center, playerPos, currentMapSize);
                                    Vector2 uv2 = ScreenToWorldUV(p2, center, playerPos, currentMapSize);

                                    if (isCenterInside)
                                    {
                                        // HARÄ°TA DIÅzINA TAÅzMAYI VE Ã‡OÄzALMAYI Ã–NLEYEN KUSURSUZ KESÄ°M MATEMATÄ°ÄzÄ°
                                        float t1 = 1.0f;
                                        float dx1 = uv1.X - centerUV.X; float dy1 = uv1.Y - centerUV.Y;
                                        if (dx1 > 0) t1 = Math.Min(t1, (1.0f - centerUV.X) / dx1);
                                        else if (dx1 < 0) t1 = Math.Min(t1, (0.0f - centerUV.X) / dx1);
                                        if (dy1 > 0) t1 = Math.Min(t1, (1.0f - centerUV.Y) / dy1);
                                        else if (dy1 < 0) t1 = Math.Min(t1, (0.0f - centerUV.Y) / dy1);

                                        float t2 = 1.0f;
                                        float dx2 = uv2.X - centerUV.X; float dy2 = uv2.Y - centerUV.Y;
                                        if (dx2 > 0) t2 = Math.Min(t2, (1.0f - centerUV.X) / dx2);
                                        else if (dx2 < 0) t2 = Math.Min(t2, (0.0f - centerUV.X) / dx2);
                                        if (dy2 > 0) t2 = Math.Min(t2, (1.0f - centerUV.Y) / dy2);
                                        else if (dy2 < 0) t2 = Math.Min(t2, (0.0f - centerUV.Y) / dy2);

                                        t1 = Math.Max(0.0f, t1);
                                        t2 = Math.Max(0.0f, t2);

                                        Vector2 clippedP1 = center + (p1 - center) * t1;
                                        Vector2 clippedP2 = center + (p2 - center) * t2;
                                        Vector2 clippedUV1 = centerUV + new Vector2(dx1 * t1, dy1 * t1);
                                        Vector2 clippedUV2 = centerUV + new Vector2(dx2 * t2, dy2 * t2);

                                        // --- PREMIUM SAYDAMLIK MOTORU ---
                                        // RGB renkleri beyaz (1.0f) kalÄ±r, Alpha (SaydamlÄ±k) deÄŸeri Slider'dan gelir!
                                        uint dynamicMapColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, _mapOpacity));

                                        drawList.AddImageQuad(textureId,
                                            center, clippedP1, clippedP2, clippedP2,
                                            centerUV, clippedUV1, clippedUV2, clippedUV2,
                                            dynamicMapColor); // 0xFFFFFFFF yerine dynamicMapColor kullanÄ±ldÄ±
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Error Code : 55 | {ex.Message}");
                            Log($"[HATA] {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }

            // --- SNIFF RANGE Ã‡EMBERÄ° (HaritanÄ±n Ã¼stÃ¼nde, entity'lerin altÄ±nda) ---
            float sniffRadiusPx = _renderDistance * _zoom;
            if (sniffRadiusPx < radiusLimit)
            {
                uint sniffFill = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1.0f, 0.2f, 0.08f));
                uint sniffBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1.0f, 0.2f, 0.55f));
                drawList.AddCircleFilled(center, sniffRadiusPx, sniffFill);
                drawList.AddCircle(center, sniffRadiusPx, sniffBorder, 64, 1.5f);

                /*string sniffLabel = $"Sniff: {_renderDistance:0}u";
                var sniffTs = ImGui.CalcTextSize(sniffLabel);
                drawList.AddText(
                    center + new Vector2(sniffRadiusPx - sniffTs.X - 4, -sniffTs.Y / 2),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1.0f, 0.2f, 0.80f)),
                    sniffLabel
                );*/
            }

            // --- MESAFE HALKALARI (50m / 100m / 150m) ---
            {
                float[] ringDistances = { 50f, 100f, 150f };
                uint ringCol  = 0x22FFFFFF; // %13 opak beyaz çizgi
                uint ringText = 0x55FFFFFF; // %33 opak beyaz etiket
                foreach (float rw in ringDistances)
                {
                    float rPx = rw * _zoom;
                    if (rPx < radiusLimit - 2f)
                    {
                        drawList.AddCircle(center, rPx, ringCol, 48, 1.0f);
                        Vector2 labelPos = center + new Vector2(4, -rPx - 10);
                        drawList.AddText(labelPos, ringText, $"{rw:0}m");
                    }
                }
            }

            if (mainPlayer != null)
            {
                drawList.AddCircleFilled(center, 5.0f, 0xFF00FFFF);

                // --- OYUNCULAR ---
                if (_showPlayers)
                {
                    lock (_dataLock)
                    {
                        bool parserFocusMode = _parserSelectedPlayerId > 0;
                        foreach (var p in _playersBuffer)
                        {
                            if (parserFocusMode && p.Id != _parserSelectedPlayerId) continue;

                            bool isFriend = _whitelist.Contains(p.Name);
                            uint color = isFriend ? 0xFF00FF00 : ((p.Faction == 255) ? COL_PLAYER : 0xFFFFFFFF);
                            string label = _showPlayerName ? p.Name : "";
                            if (_showPlayerName && _showGuild && !string.IsNullOrEmpty(p.Guild)) label += $" [{p.Guild}]";

                            // --- HAREKET İZİ (TRAIL) ---
                            if (!_playerTrails.TryGetValue(p.Id, out var trail))
                            {
                                trail = new Queue<(float, float)>(TrailMaxPoints + 1);
                                _playerTrails[p.Id] = trail;
                            }
                            var trailArr = trail.ToArray();
                            // Son pozisyondan belirgin farklıysa ekle (0.3 dünya birimi)
                            if (trailArr.Length == 0 || MathF.Pow(p.CurrentLerpedX - trailArr[^1].x, 2) + MathF.Pow(p.CurrentLerpedY - trailArr[^1].y, 2) > 0.09f)
                            {
                                if (trail.Count >= TrailMaxPoints) trail.Dequeue();
                                trail.Enqueue((p.CurrentLerpedX, p.CurrentLerpedY));
                                trailArr = trail.ToArray();
                            }
                            // Trail noktalarını soluk çiz (eski › yeni, alfa azalıyor)
                            for (int ti = 0; ti < trailArr.Length - 1; ti++)
                            {
                                float tAlpha = (ti + 1f) / trailArr.Length; // 0›1 eskiden yeniye
                                uint trailCol = (uint)(tAlpha * 0x88) << 24 | (color & 0x00FFFFFF);
                                Vector2 ts1 = WorldToScreen(center, new Vector2(trailArr[ti].x, trailArr[ti].y), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                Vector2 ts2 = WorldToScreen(center, new Vector2(trailArr[ti + 1].x, trailArr[ti + 1].y), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                if ((ts1 - center).Length() <= radiusLimit && (ts2 - center).Length() <= radiusLimit)
                                    drawList.AddLine(ts1, ts2, trailCol, 1.2f);
                            }

                            DrawRadarDot(drawList, center, mainPlayer, p.CurrentLerpedX, p.CurrentLerpedY, color, label, radiusLimit, showOffScreenArrow: true);
                        }
                    }
                }

                // --- MOBLAR ---
                lock (_dataLock)
                {
                    _mobBuffer.Clear();
                    _gameStateManager.GetMobs(_mobBuffer);

                    foreach (var m in _mobBuffer)
                    {
                        if (_ignoredMobIds.Contains(m.TypeId)) continue;
                        MobInfo info = null;
                        _mobDatabase.TryGetValue(m.TypeId, out info);

                        string displayName = "";
                        if (info != null && !string.IsNullOrEmpty(info.Name))
                        {
                            // Kesin kural: isim kaynağı TypeId metadata (deterministik)
                            displayName = info.Name;
                        }
                        else if (!string.IsNullOrEmpty(m.Name))
                        {
                            // Sadece metadata yoksa network ismi fallback
                            displayName = CleanName(m.Name);
                        }
                        else
                        {
                            displayName = $"TypeId:{m.TypeId}";
                        }

                        // --- İSİM İÇİNDEKİ GEREKSİZ TAKILARI SİLME ---
                        if (displayName != "Unknown")
                        {
                            // "Mob Mists Griffin" gibi isimlerin içinden "Mob " ve "Enemy " kelimelerini söküp atar
                            displayName = displayName.Replace("Mob ", "").Replace("Enemy ", "").Trim();

                            // Eğer geriye hiçbir şey kalmadıysa Unknown yap (Çizim aşamasındaki gişe bunu tamamen gizleyecektir)
                            if (string.IsNullOrEmpty(displayName)) displayName = "Unknown";
                        }
                        // ----------------------------------------------

                        string upperName = displayName.ToUpperInvariant();
                        bool isAspectOrWorldBoss = upperName.Contains("ASPECT") || upperName.Contains("WORLD_BOSS") || upperName.Contains("WORLD BOSS") || upperName.Contains("TITAN") || upperName.Contains("GUARDIAN");

                        // ÖZEL İKONLAR (Sniffers, Bosses, Crystals, Drones)
                        string specificIcon = null;
                        if (upperName.Contains("FAIRY") || (upperName.Contains("FEY") && upperName.Contains("DRAGON"))) specificIcon = _feyDragonPath;
                        else if (upperName.Contains("GRIFFIN")) specificIcon = _griffinPath;
                        else if (upperName.Contains("VEIL") && upperName.Contains("WEAVER")) specificIcon = _veilWeaverPath;
                        else if (isAspectOrWorldBoss && IsImageExistsCached(_aspectBossIconPath)) specificIcon = _aspectBossIconPath;
                        else if ((GetMobCategory(displayName, info?.Tier ?? 0) == "Crystals") || (upperName.Contains("SPIDER") && upperName.Contains("CRYSTAL"))) specificIcon = _spiderImagePath;
                        else if (m.TypeId >= 908 && m.TypeId <= 923) specificIcon = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "AVALONMINIONCHEST.png");
 
                        // TAKÄ°P LÄ°STESÄ° VEYA Ã–ZEL TRACKER LÄ°STESÄ°
                        bool isPriority = _customPriorityMobs.Contains(m.TypeId);
                        bool isTrackerCustom = _trackerCustomMobs.Contains(m.TypeId);

                        if ((isPriority || isTrackerCustom) && _showEnemyMobs)
                        {
                            string iconToUse = !string.IsNullOrEmpty(specificIcon) ? specificIcon : _crownImagePath;
                            bool doEdgeClamp = _trackerEnableVipMobs && (isTrackerCustom || isPriority);
                            uint mobLaserCol = isTrackerCustom && isPriority ? 0xE6FFFF00 : 0xE6FF8C00;

                            if (isTrackerCustom && isPriority)
                                mobLaserCol = 0xE6FFFF00; // Parlak sarÄ± (her iki listede de)
                            else if (isTrackerCustom)
                                mobLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorMobs); // KullanÄ±cÄ± rengi (Tracker listesi)
                            else
                                mobLaserCol = 0xE6FF8C00; // Turuncu (sadece priority/taÃ§ listesi)

                            DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, iconToUse, COL_SPECIAL, displayName, radiusLimit, _globalIconSize + 10, doEdgeClamp, mobLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                            continue;
                        }

                        var typeInfo = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                        HarvestableCategory mobCategory = HarvestableCategory.None;
                        int resolvedLivingTier = 0;
                        bool isLivingResource = false;

                        if (string.IsNullOrEmpty(specificIcon)) // <-- ÖNEMLİ KISIM BURASI
                        {
                            if (_livingResourceTypeMap.TryGetValue(m.TypeId, out var livingMap))
                            {
                                mobCategory = livingMap.category;
                                resolvedLivingTier = livingMap.tier;
                            }
                            else if (info?.IsHarvestable == true && !string.IsNullOrEmpty(info.HarvestType))
                            {
                                mobCategory = ParseCategoryFromString(info.HarvestType);
                                resolvedLivingTier = info.Tier;
                            }
                            else
                            {
                                string livingNameSource = typeInfo?.UniqueName ?? typeInfo?.Name ?? m.Name ?? displayName;
                                mobCategory = ParseCategoryFromString(livingNameSource);
                                if (mobCategory != HarvestableCategory.None)
                                {
                                    resolvedLivingTier = info?.Tier ?? typeInfo?.LootTier ?? (int)(typeInfo?.Tier ?? 0);
                                    if (resolvedLivingTier <= 0)
                                        resolvedLivingTier = ParseTier(livingNameSource);
                                }
                            }
                            isLivingResource = (mobCategory != HarvestableCategory.None);
                        }

                        if (_debugMobs) { DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, 0xFFFFFFFF, $"[{m.TypeId}] {displayName}", radiusLimit); continue; }
                        // GÄ°ZLÄ° SANDIK ID LÄ°STESÄ° â€” static readonly alan olarak taÅŸÄ±ndÄ± (_hiddenChestIds)
                        // CHEST
                        if ((upperName.Contains("CHEST") || upperName.Contains("TREASURE") || upperName.Contains("LOOT") || upperName.Contains("COFFER") || _hiddenChestIds.Contains(m.TypeId)) && !upperName.Contains("MINION"))
                        {
                            // Sadece Hidden Chest çiz (diğer chest rarity/normal chest kapalı)
                            if (upperName.Contains("HIDDEN") || _hiddenChestIds.Contains(m.TypeId))
                            {
                                uint chestColor = 0xFFFFFFFF;
                                string chestLabel = "Hidden Chest";
                                float size = 5.0f;

                                if (_showChestIds) chestLabel += $" [{m.TypeId}]";
                                DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, chestColor, chestLabel, radiusLimit, true, size);
                            }

                            continue;
                        }

                        // MIST
                        if (!isLivingResource && (upperName.Contains("MIST") || upperName.Contains("WISP") || upperName.Contains("PORTAL")))
                        {
                            if (_showMists)
                            {
                                int rarity = m.EnchantmentLevel; if (rarity > 4) rarity = 4; if (rarity < 0) rarity = 0;
                                bool isDuo = upperName.Contains("DUO");

                                string rarityLabel = rarity switch
                                {
                                    0 => "Common",
                                    1 => "Uncommon",
                                    2 => "Rare",
                                    3 => "Epic",
                                    4 => "Legendary",
                                    _ => ""
                                };

                                uint rarityColor = rarity switch
                                {
                                    0 => 0xFFAAAAAA, // Gri - Common
                                    1 => 0xFF00FF00, // YeÅŸil - Uncommon
                                    2 => 0xFF00BFFF, // Mavi - Rare
                                    3 => 0xFFFF00FF, // Mor - Epic
                                    4 => 0xFF00D7FF, // AltÄ±n - Legendary
                                    _ => COL_MIST
                                };

                                float mistIconSize = 22f;
                                string mistLabel = isDuo ? $"Duo[{rarityLabel}]" : rarityLabel;
                                DrawMistDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, _mistImagePaths[rarity], rarityColor, mistLabel, radiusLimit, mistIconSize);
                            }
                            continue; // Mist Ã§izildi, dÃ¼ÅŸman mob bloÄŸuna girme!
                        }

                        // LIVING RESOURCES (CANLI KAYNAKLAR - Geyik, Elemental vb.)
                        if (isLivingResource)
                        {
                            bool renderedAsResource = false;
                            if (_showResources && _resourceMasterToggles[mobCategory])
                            {
                                // ==============================================================
                                // --- EVRENSEL BALYOZ YÖNTEMİ (TÜM CANLI KAYNAKLAR İÇİN) ---
                                // ==============================================================
                                int tier = 0;

                                // 1. YENİ SİSTEMDEN MOB BİLGİSİNİ ÇEKİYORUZ (Hatanın çözüldüğü yer)
                                // 2. OYUNUN GİZLİ KODUNU (UniqueName) KULLANARAK KESİN TIER BULMA
                                string uName = typeInfo?.UniqueName ?? "";
                                if (!string.IsNullOrEmpty(uName))
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(uName, @"T(\d+)");
                                    if (match.Success && int.TryParse(match.Groups[1].Value, out int matchedTier))
                                    {
                                        tier = matchedTier; // Bingo! Gizli isimden %100 Doğru Tier.
                                    }
                                }

                                // 3. Eğer olur da gizli isimden bulamazsa, standart yöntemlerle bulmaya çalış
                                if (tier == 0)
                                {
                                    // Eski 'info' yerine artık kendi 'typeInfo'muzu kullanıyoruz (LootTier da burada!)
                                    if (typeInfo != null && typeInfo.LootTier > 0) tier = typeInfo.LootTier;
                                    else if (resolvedLivingTier > 0) tier = resolvedLivingTier;
                                    else if (typeInfo != null && (int)typeInfo.Tier > 0) tier = (int)typeInfo.Tier;
                                    else if (m.NetworkTier > 0) tier = m.NetworkTier;
                                    else tier = ParseTier(displayName);
                                }
                                // ==============================================================

                                if (tier <= 0)
                                    tier = 1;

                                if (tier >= 1)
                                {
                                    int enchant = ParseEnchant(m.Name);
                                    if (enchant <= 0) enchant = m.EnchantmentLevel;
                                    int tierIndex = Math.Max(0, Math.Min(tier - 1, 7)); int enchantIndex = Math.Min(enchant, 3);
                                    if (_resourceFilters[mobCategory][tierIndex, enchantIndex])
                                    {
                                        // Lang.Get kullanarak dili JSON'dan çekiyoruz. (JSON'da "WOOD": "Odun" vs. olmalı)
                                        string translatedName = Lang.Get(mobCategory.ToString());
                                        string resName = translatedName != mobCategory.ToString() ? translatedName : (_resourceMobNames.TryGetValue(mobCategory, out var n) ? n : mobCategory.ToString());

                                        // 1. AYRIM: İsmin başına koca bir [MOB] (veya [CANLI]) etiketi ekliyoruz ki düz maden sanıp üstüne koşma!
                                        string label = (enchant > 0) ? $"T{tier}.{enchant} {resName}" : $"T{tier} {resName}";

                                        // YENİ: ELEMENTAL İKONUNU VE RENGİNİ BULMA
                                        uint tCol = GetTierEnchantColor(tier, enchant);
                                        string imgPath = GetResourceImagePath(mobCategory, tier, enchant);
                                        bool iconExists = !string.IsNullOrEmpty(imgPath) && IsImageExistsCached(imgPath);

                                        // 2. AYRIM: Düz kaynakların lazeri yeşil/sarı iken, yürüyen canavarların lazerini KIRMIZI (Düşman) yapıyoruz!
                                        uint resLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorResources);

                                        if (!_resourceTrackerOnlyMode)
                                        {
                                            if (_showResourceIcons && iconExists)
                                            {
                                                DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, showTrackerIcon: _trackerShowResourceIcons);
                                            }
                                            else
                                            {
                                                string tIcon = (_trackerEnableResources && _trackerShowResourceIcons && iconExists) ? imgPath : null;
                                                DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, tCol, label, radiusLimit, false, 4.0f, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, hideMarker: false, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                            }
                                        }
                                        else if (_trackerEnableResources)
                                        {
                                            if (_showResourceIcons && iconExists)
                                            {
                                                DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, true, resLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowResourceIcons);
                                            }
                                            else
                                            {
                                                string tIcon = (_trackerShowResourceIcons && iconExists) ? imgPath : null;
                                                DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, 0x00000000, label, radiusLimit, false, 0.1f, true, resLaserCol, showOffScreenArrow: true, hideMarker: true, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                            }
                                        }
                                        renderedAsResource = true;
                                    }
                                }
                            }
                            // Filtrede seçili değilse normal düşman mob akışına düşsün (kırmızı nokta).
                            if (renderedAsResource)
                                continue;
                        }

                        // DÜŞMAN MOBLAR
                        if (_showEnemyMobs)
                        {
                            // --- PREMIUM KUKLA (DUMMY) FÄ°LTRESÄ° ---
                            // Mist ve PortallarÄ±n ortasÄ±ndaki o sinsi kÄ±rmÄ±zÄ± noktalarÄ± kalÄ±cÄ± olarak siler!
                            // Ä°smi boÅŸ olan, Unknown olan veya ID: ile baÅŸlayan o sahte moblarÄ± es geÃ§er.
                            if (string.IsNullOrEmpty(displayName) || displayName == "Unknown" || displayName.StartsWith("ID:")) continue;

                            if (!string.IsNullOrEmpty(specificIcon))
                            {
                                if (_showBosses) DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, specificIcon, COL_SPECIAL, displayName, radiusLimit, _globalIconSize + 10);
                                continue;
                            }

                            bool isBigBoss = upperName.Contains("BOSS") || upperName.Contains("ASPECT") || upperName.Contains("TITAN") || upperName.Contains("GUARDIAN") || upperName.Contains("OLD_WHITE");

                            // --- YENÄ° KURAL: EÄŸer otomatik taÃ§ takÄ±lacaksa ama listede yasaklÄ±ysa, tacÄ± Ã§Ä±kar ---
                            if (isBigBoss && _crownBlacklist.Contains(m.TypeId) && !isAspectOrWorldBoss)
                            {
                                isBigBoss = false;
                            }

                            string label = null;
                            if (_showMobNames) { if (!string.IsNullOrEmpty(displayName) && displayName != "Unknown" && !displayName.StartsWith("ID:")) label = displayName; else label = "Enemy"; }

                            // Boss â†’ AltÄ±n lazer; Normal mob â†’ kullanÄ±cÄ± seÃ§imine gÃ¶re kÄ±rmÄ±zÄ± lazer
                            if (isBigBoss)
                            {
                                if (_showBosses)
                                {
                                    string bossIcon = isAspectOrWorldBoss && IsImageExistsCached(_aspectBossIconPath) ? _aspectBossIconPath : _crownImagePath;
                                    float bossSize = isAspectOrWorldBoss ? _globalIconSize + 8f : _globalIconSize;
                                    uint bossLaser = isAspectOrWorldBoss ? 0xE600FFFF : 0xE6FFD700;

                                    // Değişiklik 2'nin detaylı hali burada:
                                    DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, bossIcon, COL_GOLD, label, radiusLimit, bossSize, _trackerEnableVipMobs, bossLaser, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                                }
                            }
                            else { if (_showNormalMobs)
                                    DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, COL_RED, label, radiusLimit, false, 3.0f, _trackerEnableNormalMobs, 0xCC4466FF); 
                            }
                        }

                    }
                }

                // --- KAYNAKLAR (HARVESTABLES) ---
                if (_showResources)
                {
                    lock (_dataLock)
                    {
                        _harvestBuffer.Clear();
                        _gameStateManager.GetHarvestables(_harvestBuffer);
                        float renderDistanceSq = _renderDistance * _renderDistance;

                        foreach (var h in _harvestBuffer)
                        {
                            if (_ignoredMobIds.Contains(h.Type)) continue;
                            if (h.Count <= 0) continue;

                            float rdx = h.CurrentLerpedX - mainPlayer.CurrentLerpedX;
                            float rdy = h.CurrentLerpedY - mainPlayer.CurrentLerpedY;
                            float rdistSq = rdx * rdx + rdy * rdy;
                            if (rdistSq > renderDistanceSq) continue;

                            // --- DEĞİŞKENLERİ BURADA TANIMLIYORUZ ---
                            var cat = GetCategoryFromTypeId(h.Type);
                            int tier = h.Tier;
                            int enchant = h.EnchantmentLevel;
                            uint tCol = GetTierEnchantColor(tier, enchant);
                            uint resLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorResources); // Tracker lazerini ekledik
                            // -------------------------------------------------------------

                            // Sonra ekrana bastÄ±rma iÅŸlemini yapÄ±yoruz
                            if (_debugStaticResources) { DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, 0xFFFFFFFF, $"[{h.Type}] {cat} T{h.Tier}.{h.EnchantmentLevel}", radiusLimit); continue; }

                            if (cat != HarvestableCategory.None && _resourceMasterToggles[cat])
                            {
                                int tierIndex = Math.Max(0, Math.Min(tier - 1, 7));
                                int enchantIndex = Math.Min(enchant, 3);
                                if (_resourceFilters[cat][tierIndex, enchantIndex])
                                {
                                    // 1. Önce senin çeviri sisteminden (JSON) ismini arıyoruz
                                    string translatedName = Lang.Get(cat.ToString());

                                    // 2. ESKİ KODUNDAKİ GİBİ: Çeviri yoksa _resourceMobNames SÖZLÜĞÜNÜ KULLANMA, direkt kategorinin kendi adını (Ore, Wood) yaz!
                                    string resName = translatedName != cat.ToString() ? translatedName : cat.ToString();

                                    // 3. Ekrana bas (İstersen sonuna eski kodundaki gibi ({h.Count}) ekleyip içindeki miktarı da gösterebilirsin)
                                    string label = _showResourceLabels ? ((enchant > 0) ? $"T{tier}.{enchant} {resName}" : $"T{tier} {resName}") : "";
                                    bool iconDrawn = false;
                                    string imgPath = GetResourceImagePath(cat, tier, enchant);
                                    bool iconExists = !string.IsNullOrEmpty(imgPath) && IsImageExistsCached(imgPath);

                                    if (!_resourceTrackerOnlyMode)
                                    {
                                        if (_showResourceIcons && iconExists)
                                        {
                                            DrawImageOrDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, showTrackerIcon: _trackerShowResourceIcons);
                                            iconDrawn = true;
                                        }
                                        else
                                        {
                                            string tIcon = (_trackerEnableResources && _trackerShowResourceIcons && iconExists) ? imgPath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, tCol, label, radiusLimit, false, 4.0f, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, hideMarker: false, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }
                                    else if (_trackerEnableResources)
                                    {
                                        if (_showResourceIcons && iconExists)
                                        {
                                            DrawImageOrDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, true, resLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowResourceIcons);
                                            iconDrawn = true;
                                        }
                                        else
                                        {
                                            string tIcon = (_trackerShowResourceIcons && iconExists) ? imgPath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, 0x00000000, label, radiusLimit, false, 0.1f, true, resLaserCol, showOffScreenArrow: true, hideMarker: true, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }

                                    // --- KAYNAK DOLULUK BARI ---
                                    if (!_resourceTrackerOnlyMode && h.Capacity > 0)
                                    {
                                        Vector2 hScreen = WorldToScreen(center, new Vector2(h.CurrentLerpedX, h.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                        if ((hScreen - center).Length() <= radiusLimit)
                                        {
                                            float ratio = Math.Max(0f, Math.Min(1f, (float)h.Count / h.Capacity));
                                            float bW = _showResourceIcons ? MathF.Max(16f, _globalIconSize) : 16f;
                                            float bH = 3f;
                                            float yOff = _showResourceIcons && iconDrawn ? (_globalIconSize / 2f + 3f) : 8f;
                                            Vector2 bMin = hScreen + new Vector2(-bW / 2f, yOff);
                                            Vector2 bMax = bMin + new Vector2(bW, bH);
                                            drawList.AddRectFilled(bMin, bMax, 0x99000000);
                                            drawList.AddRectFilled(bMin, bMin + new Vector2(bW * ratio, bH), tCol);
                                            drawList.AddRect(bMin, bMax, 0x44FFFFFF, 0, ImDrawFlags.None, 0.5f);
                                        }
                                    }

                                    // ======================================================================
                                    // --- SESSÄ°Z LOGLAMA: EKRANDA GÃ–STERÄ°LEN (FÄ°LTREDEN GEÃ‡EN) KAYNAKLAR ---
                                    // ======================================================================
                                    if (_enableLogging)
                                    {
                                        string curMap = _gameStateManager.CurrentMapId ?? "0000";
                                        RadarLogger.LogResource(curMap, cat.ToString(), $"T{tier}.{enchant}", h.Count.ToString(), h.CurrentLerpedX, h.CurrentLerpedY);
                                    }
                                }


                            }
                        }
                    }
                }

                // --- WAYPOINT ÇİZİMİ ---
                if (_waypoint.HasValue)
                {
                    Vector2 wpScreen = WorldToScreen(center, new Vector2(_waypoint.Value.x, _waypoint.Value.y), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                    bool wpOnRadar = (wpScreen - center).Length() <= radiusLimit;
                    Vector2 wpDraw = wpOnRadar ? wpScreen : center + Vector2.Normalize(wpScreen - center) * (radiusLimit - 2f);
                    // Çarpı işareti simgesi
                    float cs = 7f;
                    drawList.AddLine(wpDraw + new Vector2(-cs, -cs), wpDraw + new Vector2(cs, cs), 0xFF00FFFF, 2.0f);
                    drawList.AddLine(wpDraw + new Vector2(cs, -cs), wpDraw + new Vector2(-cs, cs), 0xFF00FFFF, 2.0f);
                    drawList.AddCircle(wpDraw, cs + 2f, 0x8800FFFF, 16, 1.0f);
                    // Mesafe etiketi
                    float wpDist = Vector2.Distance(new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY), new Vector2(_waypoint.Value.x, _waypoint.Value.y));
                    string wpLbl = $"WP {wpDist:F0}m";
                    DrawLaserLabel(drawList, wpDraw + new Vector2(cs + 3, -8), wpLbl, 0xFF00FFFF);
                    // Oyuncudan waypoint'e ince çizgi (radar içindeyse)
                    if (wpOnRadar)
                        drawList.AddLine(center, wpScreen, 0x4400FFFF, 1.0f);
                    else
                        DrawOffScreenArrow(drawList, center, radiusLimit, Vector2.Normalize(wpScreen - center), 0xFF00FFFF);
                }
            }

            // --- SAĞ TIKLA WAYPOINT EKLE ---
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !ImGui.IsAnyItemHovered() && mainPlayer != null)
            {
                Vector2 mousePos = ImGui.GetMousePos();
                if ((mousePos - center).Length() <= radiusLimit)
                {
                    var wpWorld = ScreenToWorld(mousePos, center, new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                    _waypoint = (wpWorld.X, wpWorld.Y);
                }
                else
                {
                    _waypoint = null; // Radar dışına sağ tıklama › waypoint kaldır
                }
            }
        }
        #endregion

        #region Draw Logic Helpers

        private int CalculateEnemyCount(Player mainPlayer)
        {
            if (mainPlayer == null) return 0;
            int rawCount = 0;
            lock (_dataLock)
            {
                foreach (var p in _playersBuffer)
                {
                    if (_whitelist.Contains(p.Name)) continue;
                    rawCount++;
                }
            }

            var now = DateTime.UtcNow;
            if (rawCount >= _lastEnemyCount)
            {
                _lastEnemyCount = rawCount;
                _enemyCountLastUpdated = now;
                return _lastEnemyCount;
            }

            TimeSpan enemyCountHold = TimeSpan.FromSeconds(Math.Max(0.05f, _enemyCountHoldSeconds));
            if (_enemyCountLastUpdated != DateTime.MinValue && (now - _enemyCountLastUpdated) < enemyCountHold)
                return _lastEnemyCount;

            _lastEnemyCount = rawCount;
            _enemyCountLastUpdated = now;
            return _lastEnemyCount;
        }

        private int CalculateResourceCount()
        {
            lock (_dataLock) { return _harvestBuffer.Count(h => h.Count > 0); }
        }

        private void DrawImageOrDot(ImDrawListPtr dl, Vector2 center, Player p, float tx, float ty, string imgPath, uint fallbackCol, string lbl, float lim, float size, bool edgeClamp = false, uint laserCol = 0, bool showOffScreenArrow = false, bool showTrackerIcon = true)
        {
            Vector2 final = WorldToScreen(center, new Vector2(tx, ty), new Vector2(p.CurrentLerpedX, p.CurrentLerpedY));
            Vector2 dir = final - center;
            float dist = dir.Length();

            bool isOffScreen = dist > lim;
            if (isOffScreen && !edgeClamp)
            {
                if (showOffScreenArrow) DrawOffScreenArrow(dl, center, lim, Vector2.Normalize(dir), fallbackCol, lbl);
                return;
            }

            Vector2 drawPos = isOffScreen ? center + (Vector2.Normalize(dir) * (lim - 2f)) : final;
            float currentSize = isOffScreen ? size * 0.7f : size;

            // --- ANA EKRAN ESP LAZERÄ° ---
            if (edgeClamp)
            {
                if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                Vector2 screenCenter = new Vector2(_cachedPrimaryScreenW / 2f, _cachedPrimaryScreenH / 2f)
                                     + new Vector2(_trackerScreenOffsetX, _trackerScreenOffsetY);

                // Smooth pozisyon kullan â€” exp-decay lerp sayesinde hem akÄ±cÄ± hem anlÄ±k
                float ldx = tx - p.CurrentLerpedX;
                float ldy = ty - p.CurrentLerpedY;
                if (_swapXY) { float lt = ldx; ldx = ldy; ldy = lt; }
                if (_invertX) ldx = -ldx;
                if (_invertY) ldy = -ldy;
                // Gerçek İzometrik 45 derece Kamera Açısı (Minimap'ten bağımsız, oyuna tam kilitli)
                float laserAngle = (-45.0f + _trackerAngleOffset) * (float)(Math.PI / 180.0);
                float las = (float)Math.Sin(laserAngle);
                float lac = (float)Math.Cos(laserAngle);
                // DÃ¶ndÃ¼r
                float rdx = ldx * lac - ldy * las;
                float rdy = ldx * las + ldy * lac;
                // AyrÄ± X/Y Ã¶lÃ§ek: izometrik projeksiyon skew dÃ¼zeltmesi
                Vector2 laserVec = new Vector2(rdx * _trackerScaleX, rdy * _trackerScaleY);

                if (laserVec.LengthSquared() > 0.0001f)
                {
                    Vector2 laserNorm = Vector2.Normalize(laserVec);
                    Vector2 targetOnScreen = screenCenter + laserVec + new Vector2(_trackerLaserEndOffsetX, _trackerLaserEndOffsetY);
                    var fgDrawList = ImGui.GetForegroundDrawList();
                    uint finalLaserCol = laserCol == 0 ? 0xAA0000FF : laserCol;

                    // YENİ: EĞER AYAR KAPALIYSA İKONU LAZERE GÖNDERME
                    string laserIcon = showTrackerIcon ? imgPath : null;
                    DrawCompassIndicator(fgDrawList, screenCenter, laserNorm, targetOnScreen, finalLaserCol, lbl, _cachedPrimaryScreenW, _cachedPrimaryScreenH, laserIcon, size);
                }
            }
            // -----------------------------------------------------------

            bool iconDrawn = false;
            if (IsImageExistsCached(imgPath))
            {
                try
                {
                    AddOrGetImagePointer(imgPath, true, out IntPtr textureId, out uint imgWidth, out uint imgHeight);
                    if (textureId != IntPtr.Zero)
                    {
                        dl.AddImage(textureId, drawPos - new Vector2(currentSize / 2), drawPos + new Vector2(currentSize / 2));
                        iconDrawn = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 36 | {ex.Message}");
                    if (_debugConsoleLog) Log($"[HATA] İkon Çizilemedi: {ex.Message}", LogLevel.Warning);
                    iconDrawn = false;
                }
            }

            if (!iconDrawn)
            {
                dl.AddRectFilled(drawPos - new Vector2(6, 6), drawPos + new Vector2(6, 6), fallbackCol);
                dl.AddRect(drawPos - new Vector2(6, 6), drawPos + new Vector2(6, 6), 0xFF000000);
            }

            if (!isOffScreen && !string.IsNullOrEmpty(lbl))
            {
                var ts = ImGui.CalcTextSize(lbl);
                float yOffset = iconDrawn ? (-currentSize / 2 - 4) : -20;
                Vector2 labelCenter = drawPos + new Vector2(0, yOffset - ts.Y / 2);
                DrawLaserLabel(dl, labelCenter - new Vector2(ts.X / 2, 0), lbl, fallbackCol);
            }
        }

        // --- MIST Ä°Ã‡Ä°N Ã–ZEL Ã‡Ä°ZÄ°CÄ°: Label ikona binmeden ALTINDA gÃ¶sterilir ---
        private void DrawMistDot(ImDrawListPtr dl, Vector2 center, Player p, float tx, float ty, string imgPath, uint fallbackCol, string lbl, float lim, float size)
        {
            Vector2 final = WorldToScreen(center, new Vector2(tx, ty), new Vector2(p.CurrentLerpedX, p.CurrentLerpedY));
            Vector2 dir = final - center;
            float dist = dir.Length();

            if (dist > lim) return; // Mist her zaman ekran iÃ§inde gÃ¶sterilsin, kenar clamp yok

            bool iconDrawn = false;
            if (IsImageExistsCached(imgPath))
            {
                try
                {
                    AddOrGetImagePointer(imgPath, true, out IntPtr textureId, out uint imgWidth, out uint imgHeight);
                    if (textureId != IntPtr.Zero)
                    {
                        dl.AddImage(textureId, final - new Vector2(size / 2), final + new Vector2(size / 2));
                        iconDrawn = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 37 | {ex.Message}");
                    if (_debugConsoleLog) Log($"[HATA] Mist ikonu Çizilemedi: {ex.Message}", LogLevel.Warning);
                    iconDrawn = false;
                }
            }

            if (!iconDrawn)
            {
                dl.AddCircleFilled(final, size / 2, fallbackCol);
                dl.AddCircle(final, size / 2 + 1, 0xFF000000);
            }

            // Label: ikonun ÃœSTÃœNDE gÃ¶sterilir (negatif Y = yukarÄ±)
            if (!string.IsNullOrEmpty(lbl))
            {
                var ts = ImGui.CalcTextSize(lbl);
                Vector2 textStart = final + new Vector2(-ts.X / 2, -(size / 2 + ts.Y + 2));
                dl.AddText(textStart + new Vector2(1, 1), 0xFF000000, lbl); // gÃ¶lge
                dl.AddText(textStart, fallbackCol, lbl);
            }
        }

        private void DrawRadarDot(ImDrawListPtr dl, Vector2 center, Player p, float tx, float ty, uint col, string lbl, float lim, bool isSquare = false, float size = 4.0f, bool edgeClamp = false, uint laserCol = 0, bool showOffScreenArrow = false, bool hideMarker = false, string trackerIcon = null, float trackerIconSize = 16f)
        {
            Vector2 final = WorldToScreen(center, new Vector2(tx, ty), new Vector2(p.CurrentLerpedX, p.CurrentLerpedY));
            Vector2 dir = final - center;
            float dist = dir.Length();

            bool isOffScreen = dist > lim;
            if (isOffScreen && !edgeClamp)
            {
                if (showOffScreenArrow) DrawOffScreenArrow(dl, center, lim, Vector2.Normalize(dir), col, lbl);
                return;
            }

            Vector2 drawPos = isOffScreen ? center + (Vector2.Normalize(dir) * (lim - 2f)) : final;

            // --- ANA EKRAN ESP LAZERÄ° (MULTI-MONITOR FIX) ---
            if (edgeClamp)
            {
                if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                Vector2 screenCenter = new Vector2(_cachedPrimaryScreenW / 2f, _cachedPrimaryScreenH / 2f)
                                     + new Vector2(_trackerScreenOffsetX, _trackerScreenOffsetY);

                // Smooth pozisyon kullan â€” exp-decay lerp sayesinde hem akÄ±cÄ± hem anlÄ±k
                float ldx = tx - p.CurrentLerpedX;
                float ldy = ty - p.CurrentLerpedY;
                if (_swapXY) { float lt = ldx; ldx = ldy; ldy = lt; }
                if (_invertX) ldx = -ldx;
                if (_invertY) ldy = -ldy;
                // Gerçek İzometrik 45 derece Kamera Açısı (Minimap'ten bağımsız, oyuna tam kilitli)
                float laserAngle = (-45.0f + _trackerAngleOffset) * (float)(Math.PI / 180.0);
                float las = (float)Math.Sin(laserAngle);
                float lac = (float)Math.Cos(laserAngle);
                // DÃ¶ndÃ¼r
                float rdx = ldx * lac - ldy * las;
                float rdy = ldx * las + ldy * lac;
                // AyrÄ± X/Y Ã¶lÃ§ek: izometrik projeksiyon skew dÃ¼zeltmesi
                Vector2 laserVec = new Vector2(rdx * _trackerScaleX, rdy * _trackerScaleY);

                if (laserVec.LengthSquared() > 0.0001f)
                {
                    Vector2 laserNorm      = Vector2.Normalize(laserVec);
                    Vector2 targetOnScreen  = screenCenter + laserVec + new Vector2(_trackerLaserEndOffsetX, _trackerLaserEndOffsetY);
                    var fgDrawList         = ImGui.GetForegroundDrawList();
                    uint finalLaserCol     = laserCol == 0 ? 0xAA0000FF : laserCol;
                    DrawCompassIndicator(fgDrawList, screenCenter, laserNorm, targetOnScreen, finalLaserCol, lbl, _cachedPrimaryScreenW, _cachedPrimaryScreenH, trackerIcon, trackerIconSize);
                }
            }
            // -----------------------------------------------------------


            if (hideMarker)
            {
                return;
            }

            if (isSquare)
            {
                dl.AddRectFilled(drawPos - new Vector2(size, size), drawPos + new Vector2(size, size), col);
                dl.AddRect(drawPos - new Vector2(size, size), drawPos + new Vector2(size, size), 0xFF000000);
            }
            else
            {
                dl.AddCircleFilled(drawPos, size, col);
                dl.AddCircle(drawPos, size + 1, 0xFF000000);
            }

            if (!isOffScreen && !string.IsNullOrEmpty(lbl))
            {
                var ts = ImGui.CalcTextSize(lbl);
                Vector2 labelPos2 = drawPos + new Vector2(-ts.X / 2, -(size + ts.Y + 6));
                DrawLaserLabel(dl, labelPos2, lbl, col);
            }
        }

        // Off-screen ok: radar çemberinin kenarında dolgu üçgen ok çizer
        // lbl verilirse etiketi okun ARKASINDA (merkeze doğru) – okun ucuyla çakışmaz
        private void DrawOffScreenArrow(ImDrawListPtr dl, Vector2 center, float radius, Vector2 normalizedDir, uint color, string lbl = "")
        {
            const float arrowSize = 6f;
            Vector2 tip   = center + normalizedDir * (radius - 2f);
            Vector2 perp  = new Vector2(-normalizedDir.Y, normalizedDir.X);
            Vector2 left  = tip - normalizedDir * (arrowSize * 1.8f) + perp * arrowSize;
            Vector2 right = tip - normalizedDir * (arrowSize * 1.8f) - perp * arrowSize;
            dl.AddTriangleFilled(tip, left, right, color);
            dl.AddTriangle(tip, left, right, 0xBB000000, 1.2f);

            // Etiket: okun tabanından merkeze doğru (radar içinde, okla çakışmaz)
            if (!string.IsNullOrEmpty(lbl))
            {
                var ts = ImGui.CalcTextSize(lbl);
                float inset = arrowSize * 1.8f + ts.Y + 10f;  // ok tabanından içeriye mesafe
                Vector2 lblAnchor = tip - normalizedDir * inset - new Vector2(ts.X / 2f, ts.Y / 2f);
                // Radar dairesinin içinde kalmasını garantile
                float maxR = radius - inset - 2f;
                if ((lblAnchor + new Vector2(ts.X / 2f, ts.Y / 2f) - center).Length() < radius)
                    DrawLaserLabel(dl, lblAnchor, lbl, color);
            }
        }

        private void DrawCompassIndicator(ImDrawListPtr dl, Vector2 center, Vector2 dir,
              Vector2 targetPos, uint accentCol, string lbl, float scrW, float scrH, string iconPath = null, float iconSize = 24f)
        {
            float startGap = 40f;
            Vector2 lineStart = center + dir * startGap;
            Vector2 lineEnd = targetPos;

            if (float.IsNaN(lineEnd.X) || float.IsNaN(lineEnd.Y) || float.IsInfinity(lineEnd.X) || float.IsInfinity(lineEnd.Y))
                return;

            float maxLen = MathF.Max(120f, MathF.Min(scrW, scrH) * 0.9f);
            Vector2 fromCenter = lineEnd - center;
            if (fromCenter.LengthSquared() > maxLen * maxLen)
            {
                fromCenter = Vector2.Normalize(fromCenter) * maxLen;
                lineEnd = center + fromCenter;
            }

            lineEnd.X = Math.Clamp(lineEnd.X, 8f, Math.Max(8f, scrW - 8f));
            lineEnd.Y = Math.Clamp(lineEnd.Y, 8f, Math.Max(8f, scrH - 8f));

            if (Vector2.Distance(center, lineEnd) <= startGap)
            {
                lineStart = lineEnd;
            }

            dl.AddLine(lineStart, lineEnd, accentCol, 1.5f);

            // --- YENİ EKLENEN: LAZERİN UCUNA İKON ÇİZİMİ ---
            bool iconDrawn = false;
            if (!string.IsNullOrEmpty(iconPath) && IsImageExistsCached(iconPath))
            {
                try
                {
                    AddOrGetImagePointer(iconPath, true, out IntPtr tex, out uint iw, out uint ih);
                    if (tex != IntPtr.Zero)
                    {
                        dl.AddImage(tex, lineEnd - new Vector2(iconSize / 2f), lineEnd + new Vector2(iconSize / 2f));
                        iconDrawn = true;
                    }
                }
                catch { }
            }

            if (!iconDrawn)
            {
                dl.AddCircleFilled(lineEnd, 3f, accentCol);
            }

            if (!string.IsNullOrEmpty(lbl))
            {
                string uLbl = lbl.ToUpperInvariant();
                if (uLbl != "TN" && uLbl != "MOB" && uLbl != "ENEMY" && uLbl != "UNKNOWN" && !uLbl.StartsWith("ID:"))
                {
                    var ts = ImGui.CalcTextSize(lbl);
                    // İkon varsa yazıyı ikonun altına it, yoksa yuvarlağın altına
                    float textYOffset = iconDrawn ? (iconSize / 2f + 2f) : 6f;
                    Vector2 textAnchor = lineEnd + new Vector2(-ts.X / 2f, textYOffset);

                    textAnchor.X = Math.Clamp(textAnchor.X, 8f, scrW - ts.X - 8f);
                    textAnchor.Y = Math.Clamp(textAnchor.Y, 8f, scrH - ts.Y - 8f);

                    DrawLaserLabel(dl, textAnchor, lbl, accentCol);
                }
            }
        }

        // Pill-box label Ã§izici: yarÄ±-ÅŸeffaf koyu zemin + renkli Ã§erÃ§eve + beyaz yazÄ±
        private void DrawLaserLabel(ImDrawListPtr dl, Vector2 pos, string text, uint accentCol)
        {
            var ts = ImGui.CalcTextSize(text);
            float padX = 5f;
            float padY = 2f;
            Vector2 boxMin = pos - new Vector2(padX, padY);
            Vector2 boxMax = pos + new Vector2(ts.X + padX, ts.Y + padY);
            // Koyu yarÄ±-ÅŸeffaf arka plan
            dl.AddRectFilled(boxMin, boxMax, 0xCC0B0D10, 4f);
            // Aksan rengi Ã§erÃ§eve (lazer rengiyle eÅŸleÅŸir)
            uint borderCol = (accentCol & 0x00FFFFFF) | 0xAA000000;
            dl.AddRect(boxMin, boxMax, borderCol, 4f, ImDrawFlags.None, 1.0f);
            // Beyaz metin
            dl.AddText(pos, 0xFFFFFFFF, text);
        }

        #endregion

    }
}


