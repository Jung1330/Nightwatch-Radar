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
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Handlers;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
using Nightwatch.UserControls.AlbionOverlay.ViewModels;
#endregion

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        private string _selectedSubCategory = "";

        private static readonly HashSet<int> _hiddenChestIds = new HashSet<int>
        {
            795, 796, 797, 798, 799, 800, 801, 802, 803, 804, 805, 806, 807, 808, 809,
            810, 811, 812, 813, 814, 815, 816, 817, 818, 819, 820, 821, 822, 823, 824,
            2637, 2638, 2639, 2640, 2641, 2642 // Corrupted Dungeon gizli sandıkları
        };

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

        // EKRAN VE OYUN KOORDÃâ€Â°NATLARINI KUSURSUZ SENKRONÃâ€Â°ZE EDEN FONKSÃâ€Â°YONLAR
        private Vector2 WorldToScreen(Vector2 center, Vector2 worldPos, Vector2 playerPos)
        {
            // ORÃâ€Â°JÃâ€Â°NAL KODUNDAKÃâ€Â° SIRA GERÃâ€Â° GETÃâ€Â°RÃâ€Â°LDÃâ€Â° (SÃâ€Â°LÃâ€Â°NEN/DEÃâ€zÃâ€Â°Ãâ€¦zTÃâ€Â°RÃâ€Â°LEN KISIM DÃÆ’Ã…â€œZELTÃâ€Â°LDÃâ€Â°)
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

            // HARÃâ€Â°TA BOYUTU ARTIK SABÃâ€Â°T 825 DEÃâ€zÃâ€Â°L, DÃâ€Â°NAMÃâ€Â°K GELÃâ€Â°YOR
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

            // --- YENÃâ€Â° DÃâ€Â°NAMÃâ€Â°K TEMA ÃÆ’Ã¢â‚¬Â¡Ãâ€Â°ZGÃâ€Â°SÃâ€Â° ---
            Vector4 circleThemeCol = _selectedTheme == 1
                ? new Vector4(0.22f, 0.52f, 0.92f, 0.45f)  // Obsidian Blue
                : new Vector4(1.00f, 0.40f, 0.00f, 0.35f); // Original Turuncu

            // ÃÆ’Ã¢â‚¬Â¡emberi yeni rengiyle çiziyoruz
            drawList.AddCircle(center, radiusLimit, ImGui.ColorConvertFloat4ToU32(circleThemeCol), 64, 2.0f);
            drawList.AddCircleFilled(center, radiusLimit, 0x09000000); // Çok hafif koyu zemin Ã¢â‚¬â€œ oyun görünümü öncelikli

            // --- YENÃâ€Â° VE OPTÃâ€Â°MÃâ€Â°ZE EDÃâ€Â°LMÃâ€Â°Ãâ€¦z HARÃâ€Â°TA ÃÆ’Ã¢â‚¬Â¡Ãâ€Â°ZÃâ€Â°MÃâ€Â° (KESÃâ€Â°N ÃÆ’Ã¢â‚¬Â¡ÃÆ’Ã¢â‚¬â€œZÃÆ’Ã…â€œM) ---
            if (_showMapBackground && _gameStateManager != null)
            {
                var mp = mainPlayer;
                if (mp != null)
                {
                    string currentMapId = _gameStateManager.CurrentMapId ?? "0000";
                    if (currentMapId == "LEAVING_ZONE" || string.IsNullOrEmpty(currentMapId)) currentMapId = "0000";

                    string mapImagePath = ResolveMapImagePath(currentMapId);

                    if (!string.IsNullOrEmpty(mapImagePath) && !_failedMapPaths.Contains(mapImagePath))
                    {
                        try
                        {
                            AddOrGetImagePointer(mapImagePath, true, out IntPtr textureId, out uint imgW, out uint imgH);
                            if (textureId != IntPtr.Zero)
                            {
                                float currentMapSize = 825.0f; // Standart AÃÂ§Ãâ€Â±k Dünya
                                string upperMapId = currentMapId.ToUpperInvariant();

                                // 1. ÃÆ’Ã¢â‚¬â€œNCELÃâ€Â°K: EÃâ€Ã…Â¸er bu haritanÃâ€Â±n boyutu zones.json'dan okunduysa direkt onu kullan!
                                if (_mapSizes.TryGetValue(currentMapId, out float exactSize))
                                {
                                    currentMapSize = exactSize;
                                }
                                // 2. EÃâ€zER JSON'DA YOKSA: Harita ismine bakarak tahmin et (Fallback - ÃÆ’Ã¢â‚¬Â¡ökme ÃÆ’Ã¢â‚¬â€œnleyici)
                                else
                                {
                                    if (upperMapId.StartsWith("DNG") || upperMapId.StartsWith("TNL") || upperMapId.StartsWith("PSG") || upperMapId.Contains("HALL"))
                                    {
                                        currentMapSize = 350.0f; // Zindan ve tüneller dar kalmalÃâ€Â±
                                    }
                                    else if (upperMapId.StartsWith("HIDEOUT"))
                                    {
                                        currentMapSize = 400.0f; // SÃâ€Â±Ãâ€Ã…Â¸Ãâ€Â±naklar biraz daha geniÃâ€¦Ã…Â¸
                                    }
                                    else if (upperMapId.Contains("CITY") || upperMapId.Contains("PORTAL"))
                                    {
                                        currentMapSize = 800.0f; // Ãâ€Â°Ãâ€¦zTE SENÃâ€Â°N ÃÆ’Ã¢â‚¬Â¡ÃÆ’Ã¢â‚¬â€œZÃÆ’Ã…â€œMÃÆ’Ã…â€œN: Ãâ€¦zehir ve Portallar büyük kalacak, haritadan dÃâ€Â±Ãâ€¦Ã…Â¸arÃâ€Â± taÃâ€¦Ã…Â¸mayacaksÃâ€Â±n!
                                    }
                                }

                                Vector2 playerPos = new Vector2(mp.CurrentLerpedX, mp.CurrentLerpedY);
                                Vector2 centerUV = ScreenToWorldUV(center, center, playerPos, currentMapSize);

                                // Karakter haritanÃâ€Â±n içinde mi kontrolü
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
                                        // HARÃâ€Â°TA DIÃâ€¦zINA TAÃâ€¦zMAYI VE ÃÆ’Ã¢â‚¬Â¡OÃâ€zALMAYI ÃÆ’Ã¢â‚¬â€œNLEYEN KUSURSUZ KESÃâ€Â°M MATEMATÃâ€Â°Ãâ€zÃâ€Â°
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
                                        // RGB renkleri beyaz (1.0f) kalÃâ€Â±r, Alpha (SaydamlÃâ€Â±k) deÃâ€Ã…Â¸eri Slider'dan gelir!
                                        uint dynamicMapColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, _mapOpacity));

                                        drawList.AddImageQuad(textureId,
                                            center, clippedP1, clippedP2, clippedP2,
                                            centerUV, clippedUV1, clippedUV2, clippedUV2,
                                            dynamicMapColor); // 0xFFFFFFFF yerine dynamicMapColor kullanÃâ€Â±ldÃâ€Â±
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _failedMapPaths.Add(mapImagePath);
                            Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                            Log(string.Format(Lang.Get("Error_General") ?? "[HATA] {0}", ex.Message), Nightwatch.LogLevel.Error);
                        }
                    }
                }
            }

            // --- SNIFF RANGE ÃÆ’Ã¢â‚¬Â¡EMBERÃâ€Â° (HaritanÃâ€Â±n üstünde, entity'lerin altÃâ€Â±nda) ---
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

            // --- ZINDANLAR (DUNGEONS) ---
            {
                lock (_dataLock)
                {
                    float renderDistanceSq = _renderDistance * _renderDistance;
                    foreach (var d in _dungeonBuffer)
                    {
                        float dx = d.PositionX - mainPlayer.CurrentLerpedX;
                        float dy = d.PositionY - mainPlayer.CurrentLerpedY;
                        if ((dx * dx + dy * dy) > renderDistanceSq) continue;

                        int enchLevel = Math.Clamp((int)d.EnchantmentLevel, 0, 4);
                        if (d.Type == "1" && (!_showSoloDungeons || !_showSoloEnchantments[enchLevel])) continue;
                        if (d.Type == "5" && !_showSoloBossLair) continue;
                        if (d.Type == "2" && (!_showGroupDungeons || !_showGroupEnchantments[enchLevel])) continue;
                        if (d.Type == "6" && !_showGroupBossLair) continue;
                        if (d.Type == "3" && !_showCorruptedDungeons) continue;
                        if (d.Type == "4" && !_showHellgateDungeons) continue;
                        if (d.Type == "7" && !_showMists) continue;
                        if (d.Type == "8" && (!_showAvalonianDungeons || (d.Tier >= 0 && d.Tier <= 8 && !_showAvalonianTiers[d.Tier]))) continue;
                        if (d.Type == "Exit" && !_showExits) continue;

                        string typeName = d.Type switch
                        {
                            "1" => "Solo Dungeon",
                            "5" => "Solo Boss Lair",
                            "2" => "Group Dungeon",
                            "6" => "Group Boss Lair",
                            "3" => "Corrupted",
                            "4" => "Hellgate",
                            "7" => "Mist/Abbey",
                            "8" => d.Name ?? "Avalonian Dungeon",
                            "Exit" => "Exit",
                            _ => "Dungeon"
                        };

                        uint dCol = d.EnchantmentLevel switch
                        {
                            0 => 0xFF00FF00, // Green
                            1 => 0xFFFFD700, // Gold/Blue-ish depending on logic, let's use 0xFF00A5FF for Blue
                            2 => 0xFFFF00FF, // Purple
                            3 => 0xFF00BFFF, // Legendary/Gold
                            4 => 0xFFFFFFFF,
                            _ => 0xFFFFFFFF
                        };
                        
                        if (d.Type == "Exit") dCol = 0xFFFFFF00; // Cyan
                        else if (d.EnchantmentLevel == 1) dCol = 0xFFFF8C00; // Blue/Rare
                        else if (d.EnchantmentLevel == 2) dCol = 0xFFFF00FF; // Epic/Purple
                        else if (d.EnchantmentLevel == 3) dCol = 0xFF00D7FF; // Legendary/Gold
                        
                        string label = d.EnchantmentLevel > 0 ? $"{typeName} (.{d.EnchantmentLevel})" : typeName;
                        if (d.Type == "8") label = d.Tier > 0 ? $"T{d.Tier} {label}" : label;

                        bool drawnWithIcon = false;
                        if (_showDungeonIcons)
                        {
                            string imgPath = null;
                            string resDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources");
                            if (d.Type == "3") imgPath = Path.Combine(resDir, "corrupt.png");
                            else if (d.Type == "4") imgPath = Path.Combine(resDir, "hellgate.png");
                            else if (d.Type == "7") imgPath = Path.Combine(resDir, "mist.png");
                            else if (d.Type == "1" || d.Type == "5") imgPath = Path.Combine(resDir, $"dungeon_{Math.Min((int)d.EnchantmentLevel, 4)}.png");
                            else if (d.Type == "2" || d.Type == "6") imgPath = Path.Combine(resDir, $"group_{Math.Min((int)d.EnchantmentLevel, 4)}.png");

                            if (!string.IsNullOrEmpty(imgPath) && IsImageExistsCached(imgPath))
                            {
                                Vector2 targetFinal = WorldToScreen(center, new Vector2(d.PositionX, d.PositionY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                Vector2 dir = targetFinal - center;
                                float dist = dir.Length();
                                Vector2 drawPos = dist > radiusLimit ? center + (Vector2.Normalize(dir) * (radiusLimit - 2f)) : targetFinal;
                                
                                // Draw a solid circle background for the color first
                                drawList.AddCircleFilled(drawPos, _globalIconSize / 2f + 2f, dCol);
                                // Draw the icon over it
                                DrawImageOrDot(drawList, center, mainPlayer, d.PositionX, d.PositionY, imgPath, dCol, label, radiusLimit, _globalIconSize);
                                drawnWithIcon = true;
                            }
                        }

                        if (!drawnWithIcon)
                        {
                            DrawRadarDot(drawList, center, mainPlayer, d.PositionX, d.PositionY, dCol, label, radiusLimit, isSquare: true, size: 5.0f);
                        }
                    }
                }
            }



            // --- MESAFE HALKALARI (50m / 100m / 150m) ---
            {
                float[] ringDistances = { 50f, 100f, 150f };
                uint ringCol = 0x22FFFFFF; // %13 opak beyaz çizgi
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
                            // Konumlar XOR ile şifrelendiği için haritada çizim yapılmayacak (Erken uyarı sisteminde listelenmeye devam ediyor)
                            continue;
                        }
                    }
                }

                // --- MOBLAR ---
                lock (_dataLock)
                {
                    foreach (var m in _mobViewModels)
                    {
                        if (m.Id == _devHighlightEntityId)
                        {
                            Vector2 tPos = WorldToScreen(center, new Vector2(m.CurrentLerpedX, m.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                            drawList.AddCircleFilled(tPos, 20f, 0x4400FFFF); // Yarı saydam sarı zemin
                            drawList.AddCircle(tPos, 20f, 0xFF00FFFF, 32, 3f); // SarÄ± kalın kenarlık
                        }

                        // TAKİP LİSTESİ VEYA ÖZEL TRACKER LİSTESİ
                        if ((m.IsPriority || m.IsTrackerCustom) && _showEnemyMobs && !m.IsLivingResource && !m.IsHarvestableTypeId)
                        {
                            string iconToUse = !string.IsNullOrEmpty(m.SpecificIconPath) ? m.SpecificIconPath : _crownImagePath;
                            bool doEdgeClamp = _trackerEnableVipMobs && (m.IsTrackerCustom || m.IsPriority);
                            uint mobLaserCol = m.IsTrackerCustom && m.IsPriority ? 0xE6FFFF00 : 0xE6FF8C00;

                            if (m.IsTrackerCustom && m.IsPriority)
                                mobLaserCol = 0xE6FFFF00; // Parlak sarı
                            else if (m.IsTrackerCustom)
                                mobLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorMobs);
                            else
                                mobLaserCol = 0xE6FF8C00;

                            DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, iconToUse, COL_SPECIAL, m.DisplayName, radiusLimit, _globalIconSize + 10, doEdgeClamp, mobLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                            continue;
                        }

                        if (_debugMobs) { DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, 0xFFFFFFFF, $"[{m.TypeId}] {m.DisplayName}", radiusLimit); continue; }
                        if (m.IsMist)
                        {
                            if (_showMists)
                            {
                                int rarity = m.Enchant;
                                if (m.TypeId == 51800) rarity = m.RawMob?.Rarity ?? 0;
                                if (rarity > 4) rarity = 4;
                                if (rarity < 0) rarity = 0;

                                bool isDuo = m.DisplayName != null && m.DisplayName.ToUpperInvariant().Contains("DUO");

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
                                    0 => 0xFFAAAAAA,
                                    1 => 0xFF00FF00,
                                    2 => 0xFF00BFFF,
                                    3 => 0xFFFF00FF,
                                    4 => 0xFF00D7FF,
                                    _ => COL_MIST
                                };

                                float mistIconSize = 22f;
                                string mistLabel = isDuo ? $"Duo[{rarityLabel}]" : rarityLabel;

                                bool isBetaPortal = m.TypeId == 51800;
                                if (!isBetaPortal || _showBetaChests)
                                {
                                    DrawMistDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, _mistImagePaths[rarity], rarityColor, mistLabel, radiusLimit, mistIconSize);

                                    if (isBetaPortal)
                                    {
                                        long unlockTicks = m.RawMob?.UnlockTicks ?? 0;
                                        if (unlockTicks > 0)
                                        {
                                            long currentTicks = DateTime.UtcNow.Ticks;
                                            double remainingSeconds = (double)(unlockTicks - currentTicks) / 10000000.0;
                                            if (remainingSeconds > 0)
                                            {
                                                Vector2 final = WorldToScreen(center, new Vector2(m.CurrentLerpedX, m.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                                Vector2 dir = final - center;
                                                float dist = dir.Length();
                                                Vector2 drawPos = dist > radiusLimit ? center + (Vector2.Normalize(dir) * (radiusLimit - 2f)) : final;

                                                string timerStr = $"{remainingSeconds:F0}s";
                                                Vector2 timerTextSize = ImGui.CalcTextSize(timerStr);
                                                Vector2 timerTextPos = new Vector2(drawPos.X - (timerTextSize.X * 0.5f), drawPos.Y + 12f);
                                                drawList.AddText(timerTextPos, 0xFFFFFFFF, timerStr);
                                            }
                                        }
                                    }
                                }
                            }
                            continue;
                        }

                        // LIVING RESOURCES
                        if (m.IsLivingResource)
                        {
                            bool renderedAsResource = false;
                            if (_showResources && _resourceMasterToggles[m.Category])
                            {
                                if (_resourceShowOnlyEnchanted && m.Enchant <= 0) continue;

                                int tierIndex = Math.Max(0, Math.Min(m.Tier - 1, 7)); int enchantIndex = Math.Min(m.Enchant, 3);
                                if (_resourceFilters[m.Category][tierIndex, enchantIndex])
                                {
                                    string translatedName = Lang.Get(m.Category.ToString());
                                    string resName = translatedName != m.Category.ToString() ? translatedName : (_resourceMobNames.TryGetValue(m.Category, out var n) ? n : m.Category.ToString());

                                    string label = (m.Enchant > 0) ? $"T{m.Tier}.{m.Enchant} {resName}" : $"T{m.Tier} {resName}";
                                    uint tCol = GetTierEnchantColor(m.Tier, m.Enchant);
                                    string imgPath = GetResourceImagePath(m.Category, m.Tier, m.Enchant);
                                    bool iconExists = !string.IsNullOrEmpty(imgPath) && IsImageExistsCached(imgPath);
                                    uint resLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorResources);

                                    if (!_resourceTrackerOnlyMode)
                                    {
                                        if (_showResourceIcons && iconExists)
                                            DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, showTrackerIcon: _trackerShowResourceIcons);
                                        else
                                        {
                                            string tIcon = (_trackerEnableResources && _trackerShowResourceIcons && iconExists) ? imgPath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, tCol, label, radiusLimit, false, 4.0f, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, hideMarker: false, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }
                                    else if (_trackerEnableResources)
                                    {
                                        if (_showResourceIcons && iconExists)
                                            DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, imgPath, tCol, label, radiusLimit, _globalIconSize, true, resLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowResourceIcons);
                                        else
                                        {
                                            string tIcon = (_trackerShowResourceIcons && iconExists) ? imgPath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, 0x00000000, label, radiusLimit, false, 0.1f, true, resLaserCol, showOffScreenArrow: true, hideMarker: true, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }
                                    renderedAsResource = true;
                                }
                            }
                            if (renderedAsResource) continue;
                        }

                        // DÜŞMAN MOBLAR
                        if (_showEnemyMobs)
                        {
                            string upperName = m.DisplayName != null ? m.DisplayName.ToUpperInvariant() : "";
                            string rawUpperName = m.RawMob?.Name != null ? m.RawMob.Name.ToUpperInvariant() : "";



                            // --- Cages & Smugglers ---
                            bool isSmuggler = upperName.Contains("SMUGGLER") || rawUpperName.Contains("SMUGGLER")
                                || upperName.Contains("TRADING OUTPOST") || rawUpperName.Contains("TRADING OUTPOST")
                                || upperName.Contains("TRADING POST") || rawUpperName.Contains("TRADING POST")
                                || upperName.Contains("KAÇAKÇI") || rawUpperName.Contains("KAÇAKÇI")
                                || upperName.Contains("KACAKCI") || rawUpperName.Contains("KACAKCI")
                                || upperName.Contains("КОНТРАБАНДИСТ") || rawUpperName.Contains("КОНТРАБАНДИСТ")
                                || upperName.Contains("走私") || rawUpperName.Contains("走私");

                            bool isWispCage = !isSmuggler && (m.TypeId == 53000 
                                || (upperName.Contains("CAGE") && upperName.Contains("WISP")) 
                                || (rawUpperName.Contains("CAGE") && rawUpperName.Contains("WISP"))
                                || upperName.Contains("KAFES")
                                || rawUpperName.Contains("KAFES"));

                            if (isWispCage || isSmuggler)
                            {
                                bool shouldDraw = isWispCage ? _showWispCages : _showSmugglers;
                                if (shouldDraw)
                                {
                                    string cageLabel = _showMobNames ? (isWispCage ? "Wisp Cage" : "Smuggler") : null;
                                    if (isWispCage)
                                    {
                                        string wispIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "cage.png");
                                        DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, wispIconPath, 0xFF00A5FF, cageLabel, radiusLimit, _globalIconSize + 6, _trackerEnableVipMobs, laserCol: 0, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                                    }
                                    else
                                    {
                                        string smugglerIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", "smuggler.png");
                                        DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, smugglerIconPath, 0xFF00A5FF, cageLabel, radiusLimit, _globalIconSize + 6, _trackerEnableVipMobs, laserCol: 0, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                                    }
                                }
                                continue;
                            }

                            // --- Trackers ---
                            bool isTrack = m.TypeId == 55600;
                            if (isTrack)
                            {
                                if (_showTrackers)
                                {
                                    string trackType = (m.RawMob?.Name ?? "").ToUpperInvariant();
                                    bool isBear = trackType.Contains("BEAR");
                                    bool isWerewolf = trackType.Contains("WEREWOLF") || trackType.Contains("LYCAN");
                                    bool isWolf = (trackType.Contains("WOLF") || trackType.Contains("DIRE") || trackType.Contains("LUPINE")) && !isWerewolf;
                                    bool isPanther = trackType.Contains("PANTHER") || trackType.Contains("COUGAR");
                                    bool isHumanoid = trackType.Contains("HUMANOID");
                                    bool isElemental = trackType.Contains("ELEMENTAL") || trackType.Contains("ORED");
                                    bool isEnt = trackType.Contains("ENT") || trackType.Contains("WOOD");
                                    bool isImp = trackType.Contains("IMP") || trackType.Contains("DEMON");
                                    bool isGolem = trackType.Contains("GOLEM") || trackType.Contains("STONE");

                                    bool shouldTrack = (isBear && _trackBear) ||
                                                       (isWolf && _trackWolf) ||
                                                       (isPanther && _trackPanther) ||
                                                       (isHumanoid && _trackHumanoid) ||
                                                       (isElemental && _trackElemental) ||
                                                       (isEnt && _trackEnt) ||
                                                       (isImp && _trackImp) ||
                                                       (isGolem && _trackGolem) ||
                                                       (isWerewolf && _trackWerewolf);

                                    if (shouldTrack)
                                    {
                                        string iconName = "track_bear.png";
                                        if (isWolf) iconName = "track_wolf.png";
                                        else if (isPanther) iconName = "track_panther.png";
                                        else if (isHumanoid) iconName = "track_humanoid.png";
                                        else if (isElemental) iconName = "track_elemental.png";
                                        else if (isEnt) iconName = "track_ent.png";
                                        else if (isImp) iconName = "track_imp.png";
                                        else if (isGolem) iconName = "track_golem.png";
                                        else if (isWerewolf) iconName = "track_werewolf.png";

                                        string trackerIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", iconName);
                                        string trackLabel = $"Track: {CleanTrackName(m.RawMob?.Name)} T{m.RawMob?.NetworkTier ?? 0}";
                                        DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, trackerIconPath, 0xFFE0B0FF, _showMobNames ? trackLabel : null, radiusLimit, _globalIconSize + 6, edgeClamp: false, laserCol: 0x88E0B0FF, showOffScreenArrow: false, showTrackerIcon: _trackerShowMobIcons);
                                    }
                                }
                                continue;
                            }

                            bool isAspectOrWorldBoss = upperName.Contains("ASPECT") || upperName.Contains("WORLD_BOSS") || upperName.Contains("WORLD BOSS") || (upperName.Contains("TITAN") && !upperName.Contains("TITANIUM") && !upperName.Contains("TITANYUM") && !upperName.Contains("TİTANYUM")) || upperName.Contains("GUARDIAN")
                                || rawUpperName.Contains("ASPECT") || rawUpperName.Contains("WORLD_BOSS");
                            bool isChestOrTreasure = upperName.Contains("CHEST") || upperName.Contains("TREASURE") || upperName.Contains("CACHE") || upperName.Contains("KASA")
                                || rawUpperName.Contains("CHEST") || rawUpperName.Contains("TREASURE") || rawUpperName.Contains("CACHE");
                            bool isCrystalBoss = upperName.Contains("CRYSTAL") || rawUpperName.Contains("CRYSTAL") || upperName.Contains("KRİSTAL") || upperName.Contains("KRISTAL");
                            bool isBigBoss = upperName.Contains("BOSS") || rawUpperName.Contains("BOSS") || isAspectOrWorldBoss || upperName.Contains("OLD_WHITE") || rawUpperName.Contains("OLD_WHITE") || upperName.Contains("SPIDER") || rawUpperName.Contains("SPIDER") || upperName.Contains("VORTEX") || isCrystalBoss || isChestOrTreasure || !string.IsNullOrEmpty(m.SpecificIconPath);

                            if (m.IsLivingResource && !isAspectOrWorldBoss)
                                isBigBoss = false;

                            if (isBigBoss && _crownBlacklist.Contains(m.TypeId) && !isAspectOrWorldBoss && !isChestOrTreasure)
                                isBigBoss = false;

                            if (m.IsLivingResource && !isBigBoss)
                            {
                                string resLabel = _showMobNames ? (string.IsNullOrEmpty(m.DisplayName) || m.DisplayName == "Unknown" ? m.Category.ToString() : m.DisplayName) : null;
                                if (_showNormalMobs) DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, COL_RED, resLabel, radiusLimit, false, 3.0f, _trackerEnableNormalMobs, 0xCC4466FF, showOffScreenArrow: _trackerEnableNormalMobs, hideMarker: false);
                                continue;
                            }

                            if (string.IsNullOrEmpty(m.DisplayName) || m.DisplayName == "Unknown" || m.DisplayName.StartsWith("ID:")) continue;



                            string label = null;
                            if (_showMobNames || !string.IsNullOrEmpty(m.SpecificIconPath) || isChestOrTreasure) label = m.DisplayName;

                            if (isBigBoss)
                            {
                                bool shouldDraw = isChestOrTreasure ? _showHiddenChests : _showBosses;

                                if (shouldDraw)
                                {
                                    string bossIcon = _crownImagePath;
                                    if (isAspectOrWorldBoss && IsImageExistsCached(_aspectBossIconPath))
                                        bossIcon = _aspectBossIconPath;
                                    else if (!string.IsNullOrEmpty(m.SpecificIconPath) && IsImageExistsCached(m.SpecificIconPath))
                                        bossIcon = m.SpecificIconPath;
                                    
                                    // Chest veya Treasure ise özel bir renk verelim
                                    uint bossLaser = 0xE6FFD700;
                                    if (isAspectOrWorldBoss) bossLaser = 0xE600FFFF;
                                    else if (isChestOrTreasure) bossLaser = 0xE600FF00; // Yeşil

                                    float bossSize = isAspectOrWorldBoss ? _bossIconSize + 12f : _bossIconSize;
                                    if (isChestOrTreasure) bossSize = Math.Min(24f, _bossIconSize); // Daha küçük icon

                                    if (isChestOrTreasure && string.IsNullOrEmpty(m.SpecificIconPath))
                                    {
                                        DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, 0xFFFFFFFF, label, radiusLimit, isSquare: true, size: 6.0f, edgeClamp: _trackerEnableVipMobs, laserCol: bossLaser, showOffScreenArrow: true, hideMarker: false);
                                    }
                                    else
                                    {
                                        DrawImageOrDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, bossIcon, COL_GOLD, label, radiusLimit, bossSize, _trackerEnableVipMobs, bossLaser, showOffScreenArrow: true, showTrackerIcon: _trackerShowMobIcons);
                                    }
                                }
                            }
                            else
                            {
                                if (_showNormalMobs)
                                    DrawRadarDot(drawList, center, mainPlayer, m.CurrentLerpedX, m.CurrentLerpedY, COL_RED, label, radiusLimit, false, 3.0f, _trackerEnableNormalMobs, 0xCC4466FF, showOffScreenArrow: _trackerEnableNormalMobs, hideMarker: false);
                            }
                        }
                    }
                }

                // --- KAYNAKLAR (HARVESTABLES) ---
                if (_showResources)
                {
                    lock (_dataLock)
                    {
                        float renderDistanceSq = _renderDistance * _renderDistance;

                        foreach (var h in _harvestViewModels)
                        {
                            float rdx = h.CurrentLerpedX - mainPlayer.CurrentLerpedX;
                            float rdy = h.CurrentLerpedY - mainPlayer.CurrentLerpedY;
                            float rdistSq = rdx * rdx + rdy * rdy;
                            if (rdistSq > renderDistanceSq) continue;

                            if (_resourceShowOnlyEnchanted && h.Enchant <= 0) continue;

                            uint resLaserCol = ImGui.ColorConvertFloat4ToU32(_trackerLaserColorResources);

                            if (_debugStaticResources) { DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, 0xFFFFFFFF, $"[{h.Type}] {h.Category} T{h.Tier}.{h.Enchant}", radiusLimit); continue; }

                            // YENİ: GİZLİ SANDIK KONTROLÜ
                            // GİZLİ SANDIKLAR ARTIK BURADA DEĞİL MOB DÖNGÜSÜNDE KONTROL EDİLİYOR.
                            // ÇÜNKÜ OYUN SUNUCUSU GİZLİ SANDIKLARI HARVESTABLE DEĞİL MOB OLARAK YOLLAR.

                            if (h.Category != HarvestableCategory.None && _resourceMasterToggles[h.Category])
                            {
                                int tierIndex = Math.Max(0, Math.Min(h.Tier - 1, 7));
                                int enchantIndex = Math.Min(h.Enchant, 3);
                                if (_resourceFilters[h.Category][tierIndex, enchantIndex])
                                {
                                    if (_enableBetaHeatmap)
                                    {
                                        Vector2 hScreen = WorldToScreen(center, new Vector2(h.CurrentLerpedX, h.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                        if ((hScreen - center).Length() <= radiusLimit)
                                        {
                                            uint heatColor = 0x1500FF00; // T4/lower: Green (0x15 alpha)
                                            if (h.Tier >= 7) heatColor = 0x250000FF; // T7/8: Red (0x25 alpha)
                                            else if (h.Tier == 6) heatColor = 0x1A008CFF; // T6: Orange
                                            else if (h.Tier == 5) heatColor = 0x1500FFFF; // T5: Yellow
                                            
                                            drawList.AddCircleFilled(hScreen, 45f, heatColor, 32);
                                        }
                                        continue;
                                    }

                                    string label = _showResourceLabels ? h.ResourceLabel : "";
                                    bool iconDrawn = false;
                                    bool iconExists = !string.IsNullOrEmpty(h.ResourceImagePath) && IsImageExistsCached(h.ResourceImagePath);

                                    if (!_resourceTrackerOnlyMode)
                                    {
                                        if (_showResourceIcons && iconExists)
                                        {
                                            DrawImageOrDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, h.ResourceImagePath, h.TierColor, label, radiusLimit, _globalIconSize, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, showTrackerIcon: _trackerShowResourceIcons);
                                            iconDrawn = true;
                                        }
                                        else
                                        {
                                            string tIcon = (_trackerEnableResources && _trackerShowResourceIcons && iconExists) ? h.ResourceImagePath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, h.TierColor, label, radiusLimit, false, 4.0f, _trackerEnableResources, resLaserCol, showOffScreenArrow: false, hideMarker: false, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }
                                    else if (_trackerEnableResources)
                                    {
                                        if (_showResourceIcons && iconExists)
                                        {
                                            DrawImageOrDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, h.ResourceImagePath, h.TierColor, label, radiusLimit, _globalIconSize, true, resLaserCol, showOffScreenArrow: true, showTrackerIcon: _trackerShowResourceIcons);
                                            iconDrawn = true;
                                        }
                                        else
                                        {
                                            string tIcon = (_trackerShowResourceIcons && iconExists) ? h.ResourceImagePath : null;
                                            DrawRadarDot(drawList, center, mainPlayer, h.CurrentLerpedX, h.CurrentLerpedY, 0x00000000, label, radiusLimit, false, 0.1f, true, resLaserCol, showOffScreenArrow: true, hideMarker: true, trackerIcon: tIcon, trackerIconSize: _globalIconSize);
                                        }
                                    }

                                    // --- KAYNAK DOLULUK BARI ---
                                    if (!_resourceTrackerOnlyMode && h.RawHarvestable.Capacity > 0)
                                    {
                                        Vector2 hScreen = WorldToScreen(center, new Vector2(h.CurrentLerpedX, h.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                                        if ((hScreen - center).Length() <= radiusLimit)
                                        {
                                            float ratio = Math.Max(0f, Math.Min(1f, (float)h.Size / h.RawHarvestable.Capacity));
                                            float bW = _showResourceIcons ? MathF.Max(16f, _globalIconSize) : 16f;
                                            float bH = 5f;
                                            float yOff = _showResourceIcons && iconDrawn ? (_globalIconSize / 2f + 3f) : 8f;
                                            Vector2 bMin = hScreen + new Vector2(-bW / 2f, yOff);
                                            Vector2 bMax = bMin + new Vector2(bW, bH);
                                            drawList.AddRectFilled(bMin, bMax, 0xDD000000);
                                            drawList.AddRectFilled(bMin, bMin + new Vector2(bW * ratio, bH), h.TierColor);
                                            drawList.AddRect(bMin, bMax, 0xAAFFFFFF, 0, ImDrawFlags.None, 1.0f);
                                        }
                                    }

                                    // ======================================================================
                                    // --- SESSÃâ€žÂ°Z LOGLAMA: EKRANDA GÃÆ’Ã¢â‚¬â€œSTERÃâ€žÂ°LEN (FÃâ€žÂ°LTREDEN GEÃÆ’Ã¢â‚¬Â¡EN) KAYNAKLAR ---
                                    // ======================================================================
                                    if (_enableLogging)
                                    {
                                        string curMap = _gameStateManager.CurrentMapId ?? "0000";
                                        RadarLogger.LogResource(curMap, h.Category.ToString(), $"T{h.Tier}.{h.Enchant}", h.Size.ToString(), h.CurrentLerpedX, h.CurrentLerpedY);
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

            // --- SAÃ„Âž TIKLA WAYPOINT EKLE ---
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
                    _waypoint = null; // Radar dışına sağ tıklama Ã¢â‚¬Âº waypoint kaldır
                }
            }
        }
        #endregion

        #region Draw Logic Helpers

        private bool IsWhitelisted(Player p, Player mainPlayer)
        {
            if (_whitelist.Contains(p.Name)) return true;
            if (mainPlayer == null) return false;

            if (_whitelistImportSameGuild && !string.IsNullOrWhiteSpace(mainPlayer.Guild) && 
                string.Equals(mainPlayer.Guild, p.Guild, StringComparison.OrdinalIgnoreCase))
                return true;

            if (_whitelistImportSameAlliance && !string.IsNullOrWhiteSpace(mainPlayer.Alliance) && 
                string.Equals(mainPlayer.Alliance, p.Alliance, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private int CalculateEnemyCount(Player mainPlayer)
        {
            if (mainPlayer == null) return 0;
            if (_gameStateManager != null && _gameStateManager.IsSafeZone) return 0;
            int rawCount = 0;
            lock (_dataLock)
            {
                foreach (var p in _playersBuffer)
                {
                    if (IsWhitelisted(p, mainPlayer)) continue;
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

            // --- ANA EKRAN ESP LAZERÃâ€žÂ° ---
            if (edgeClamp)
            {
                if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                Vector2 screenCenter = new Vector2(_cachedPrimaryScreenW / 2f, _cachedPrimaryScreenH / 2f)
                                     + new Vector2(_trackerScreenOffsetX, _trackerScreenOffsetY);

                // Smooth pozisyon kullan ÃÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â  exp-decay lerp sayesinde hem akÃâ€žÂ±cÃâ€žÂ± hem anlÃâ€žÂ±k
                float ldx = tx - p.CurrentLerpedX;
                float ldy = ty - p.CurrentLerpedY;
                if (_swapXY) { float lt = ldx; ldx = ldy; ldy = lt; }
                if (_invertX) ldx = -ldx;
                if (_invertY) ldy = -ldy;
                // Gerçek İzometrik 45 derece Kamera Açısı (Minimap'ten bağımsız, oyuna tam kilitli)
                float laserAngle = (-45.0f + _trackerAngleOffset) * (float)(Math.PI / 180.0);
                float las = (float)Math.Sin(laserAngle);
                float lac = (float)Math.Cos(laserAngle);
                // Döndür
                float rdx = ldx * lac - ldy * las;
                float rdy = ldx * las + ldy * lac;
                // AyrÃâ€žÂ± X/Y ölçek: izometrik projeksiyon skew düzeltmesi
                Vector2 laserVec = new Vector2(rdx * _trackerScaleX, rdy * _trackerScaleY);

                if (laserVec.LengthSquared() > 0.0001f)
                {
                    Vector2 laserNorm = Vector2.Normalize(laserVec);
                    Vector2 targetOnScreen = screenCenter + laserVec + new Vector2(_trackerLaserEndOffsetX, _trackerLaserEndOffsetY);
                    var fgDrawList = ImGui.GetForegroundDrawList();
                    uint finalLaserCol = laserCol == 0 ? 0xAA0000FF : laserCol;

                    // YENİ: EÃ„ÂžER AYAR KAPALIYSA İKONU LAZERE GÖNDERME
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
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    if (_debugConsoleLog) Log(string.Format(Lang.Get("Error_IconDraw") ?? "[HATA] İkon Çizilemedi: {0}", ex.Message), LogLevel.Warning);
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
            
            if (ImGui.IsKeyDown(ImGuiKey.Tab) && !string.IsNullOrEmpty(lbl))
            {
                if (Vector2.Distance(ImGui.GetMousePos(), drawPos) < Math.Max(15f, currentSize))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(lbl);
                    ImGui.EndTooltip();
                }
            }
        }

        // --- MIST Ãâ€Â°ÃÆ’Ã¢â‚¬Â¡Ãâ€Â°N ÃÆ’Ã¢â‚¬â€œZEL ÃÆ’Ã¢â‚¬Â¡Ãâ€Â°ZÃâ€Â°CÃâ€Â°: Label ikona binmeden ALTINDA gösterilir ---
        private void DrawMistDot(ImDrawListPtr dl, Vector2 center, Player p, float tx, float ty, string imgPath, uint fallbackCol, string lbl, float lim, float size)
        {
            Vector2 final = WorldToScreen(center, new Vector2(tx, ty), new Vector2(p.CurrentLerpedX, p.CurrentLerpedY));
            Vector2 dir = final - center;
            float dist = dir.Length();

            if (dist > lim) return; // Mist her zaman ekran içinde gösterilsin, kenar clamp yok

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
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    if (_debugConsoleLog) Log(string.Format(Lang.Get("Error_MistIconDraw") ?? "[HATA] Mist ikonu Çizilemedi: {0}", ex.Message), LogLevel.Warning);
                    iconDrawn = false;
                }
            }

            if (!iconDrawn)
            {
                dl.AddCircleFilled(final, size / 2, fallbackCol);
                dl.AddCircle(final, size / 2 + 1, 0xFF000000);
            }

            // Label: ikonun ÃÆ’Ã…â€œSTÃÆ’Ã…â€œNDE gösterilir (negatif Y = yukarÃâ€Â±)
            if (!string.IsNullOrEmpty(lbl))
            {
                var ts = ImGui.CalcTextSize(lbl);
                Vector2 textStart = final + new Vector2(-ts.X / 2, -(size / 2 + ts.Y + 2));
                dl.AddText(textStart + new Vector2(1, 1), 0xFF000000, lbl); // gölge
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

            // --- ANA EKRAN ESP LAZERÃâ€Â° (MULTI-MONITOR FIX) ---
            if (edgeClamp)
            {
                if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                Vector2 screenCenter = new Vector2(_cachedPrimaryScreenW / 2f, _cachedPrimaryScreenH / 2f)
                                     + new Vector2(_trackerScreenOffsetX, _trackerScreenOffsetY);

                // Smooth pozisyon kullan ÃÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â exp-decay lerp sayesinde hem akÃâ€Â±cÃâ€Â± hem anlÃâ€Â±k
                float ldx = tx - p.CurrentLerpedX;
                float ldy = ty - p.CurrentLerpedY;
                if (_swapXY) { float lt = ldx; ldx = ldy; ldy = lt; }
                if (_invertX) ldx = -ldx;
                if (_invertY) ldy = -ldy;
                // Gerçek İzometrik 45 derece Kamera Açısı (Minimap'ten bağımsız, oyuna tam kilitli)
                float laserAngle = (-45.0f + _trackerAngleOffset) * (float)(Math.PI / 180.0);
                float las = (float)Math.Sin(laserAngle);
                float lac = (float)Math.Cos(laserAngle);
                // Döndür
                float rdx = ldx * lac - ldy * las;
                float rdy = ldx * las + ldy * lac;
                // AyrÃâ€Â± X/Y ölçek: izometrik projeksiyon skew düzeltmesi
                Vector2 laserVec = new Vector2(rdx * _trackerScaleX, rdy * _trackerScaleY);

                if (laserVec.LengthSquared() > 0.0001f)
                {
                    Vector2 laserNorm = Vector2.Normalize(laserVec);
                    Vector2 targetOnScreen = screenCenter + laserVec + new Vector2(_trackerLaserEndOffsetX, _trackerLaserEndOffsetY);
                    var fgDrawList = ImGui.GetForegroundDrawList();
                    uint finalLaserCol = laserCol == 0 ? 0xAA0000FF : laserCol;
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
        // lbl verilirse etiketi okun ARKASINDA (merkeze doğru) Ã¢â‚¬â€œ okun ucuyla çakışmaz
        private void DrawOffScreenArrow(ImDrawListPtr dl, Vector2 center, float radius, Vector2 normalizedDir, uint color, string lbl = "")
        {
            const float arrowSize = 6f;
            Vector2 tip = center + normalizedDir * (radius - 2f);
            Vector2 perp = new Vector2(-normalizedDir.Y, normalizedDir.X);
            Vector2 left = tip - normalizedDir * (arrowSize * 1.8f) + perp * arrowSize;
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

            // --- YENİ EKLENEN: LAZERİN UCUNA Ä°KON ÇİZİMİ ---
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
                catch (Exception ex) { System.Console.WriteLine($"Minimap image error: {ex.Message}"); }
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

        // Pill-box label çizici: yarÃâ€Â±-Ãâ€¦Ã…Â¸effaf koyu zemin + renkli çerçeve + beyaz yazÃâ€Â±
        private void DrawLaserLabel(ImDrawListPtr dl, Vector2 pos, string text, uint accentCol)
        {
            var ts = ImGui.CalcTextSize(text);
            float padX = 5f;
            float padY = 2f;
            Vector2 boxMin = pos - new Vector2(padX, padY);
            Vector2 boxMax = pos + new Vector2(ts.X + padX, ts.Y + padY);
            // Koyu yarÃâ€Â±-Ãâ€¦Ã…Â¸effaf arka plan
            dl.AddRectFilled(boxMin, boxMax, 0xCC0B0D10, 4f);
            // Aksan rengi çerçeve (lazer rengiyle eÃâ€¦Ã…Â¸leÃâ€¦Ã…Â¸ir)
            uint borderCol = (accentCol & 0x00FFFFFF) | 0xAA000000;
            dl.AddRect(boxMin, boxMax, borderCol, 4f, ImDrawFlags.None, 1.0f);
            // Beyaz metin
            dl.AddText(pos, 0xFFFFFFFF, text);
        }

        private string CleanTrackName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Track";
            return raw.Replace("SHARED_TRACK_", "")
                      .Replace("SOLO_", "")
                      .Replace("GROUP_", "")
                      .ToLowerInvariant();
        }

        #endregion

    }
}








