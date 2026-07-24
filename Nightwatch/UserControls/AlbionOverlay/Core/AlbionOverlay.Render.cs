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
        private static DateTime _lastResolutionCheckTime = DateTime.MinValue;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _uiThreadActions = new();

        private void EnqueueUi(Action action)
        {
            if (action != null)
                _uiThreadActions.Enqueue(action);
        }

        private void DrainUiActions()
        {
            while (_uiThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                }
            }
        }

        #region Arayüz Çizimi (UI Rendering)
        protected override void Render()
        {
            DrainUiActions();
            ApplyModernStyle();

            string currentLang = Lang.CurrentLanguage ?? "";
            if (_lastTabLanguage != currentLang)
            {
                _tabs[0] = Lang.Get("Tab_Resources") ?? "Resources";
                _tabs[1] = Lang.Get("Tab_Mobs") ?? "Mobs";
                _tabs[2] = Lang.Get("Tab_Players") ?? "Players";
                _tabs[3] = Lang.Get("Tab_Config") ?? "Config";
                _tabs[4] = Lang.Get("Tab_DevTools") ?? "Dev Tools";
                _tabs[5] = Lang.Get("Tab_Settings") ?? "Settings";
                _tabs[6] = Lang.Get("Tab_Device") ?? "Device";
                _lastTabLanguage = currentLang;

               /* ImGui.ClearIniSettings(); // UI'ın o anki bozuk konumunu/ayarlarını sıfırla
                return; // Dili değiştirdiğin an o karede çizimi bırak, bir sonraki karede temiz çizsin*/
            }

            if (!_isSizeFixed) FixLayoutWait();
            else if ((DateTime.UtcNow - _lastResolutionCheckTime).TotalMilliseconds >= 250)
            {
                _lastResolutionCheckTime = DateTime.UtcNow;
                if (_cachedPrimaryScreenW != GetSystemMetrics(0) || _cachedPrimaryScreenH != GetSystemMetrics(1)) // SM_CXSCREEN = 0, SM_CYSCREEN = 1
                {
                    FixLayoutWait();
                }
            }

            if (!_isIconSet) { _isIconSet = SetApplicationWindowIcon(); }

            // Oyun verilerini güncelle
            _gameStateManager.Update();
            string currentMapId = _gameStateManager.CurrentMapId ?? "";

            lock (_dataLock)
            {
                if (_lastMapId != currentMapId)
                {
                    _playersBuffer.Clear();
                    _harvestBuffer.Clear();
                    _mobBuffer.Clear();
                    _dungeonBuffer.Clear();
                    
                    _lastMapId = currentMapId;

                    _mapGlobalOffsetX = 0f;
                    _mapGlobalOffsetY = 0f;


                    // --- HARİTA TEMİZLEME (GHOST MOB FIX) ---
                    _prevPlayerPos.Clear();
                    ClearImageCache();
                }
                {
                    _playersBuffer.Clear();
                    _mobBuffer.Clear();
                    _harvestBuffer.Clear();
                    _dungeonBuffer.Clear();
                    
                    _gameStateManager.GetOtherPlayers(_playersBuffer);
                    _gameStateManager.GetMobs(_mobBuffer);
                    _gameStateManager.GetHarvestables(_harvestBuffer);
                    _gameStateManager.GetDungeons(_dungeonBuffer);
                    
                }
            }

            var mainPlayer = _gameStateManager.GetPlayer();

            // --- SMOOTH PLAYER POSITION (lazer çizgisini akıcı yapar) ---
            if (mainPlayer != null)
            {
                if (!_smoothPlayerInitialized)
                {
                    _smoothPlayerX = mainPlayer.PositionX;
                    _smoothPlayerY = mainPlayer.PositionY;
                    _smoothPlayerInitialized = true;
                }
                float dt = Math.Min(ImGui.GetIO().DeltaTime, 0.1f);
                float lerpT = 1f - (float)Math.Exp(-20f * dt);
                _smoothPlayerX += (mainPlayer.PositionX - _smoothPlayerX) * lerpT;
                _smoothPlayerY += (mainPlayer.PositionY - _smoothPlayerY) * lerpT;
                mainPlayer.CurrentLerpedX = _smoothPlayerX;
                mainPlayer.CurrentLerpedY = _smoothPlayerY;
            }

            // --- SMOOTH ENTITY POSITIONS (mob/kaynak lazer çizgilerini akÃâ€Â±cÃâ€Â± yapar) ---
            {
                float dtEnt = Math.Min(ImGui.GetIO().DeltaTime, 0.1f);
                float lerpTEnt = 1f - (float)Math.Exp(-20f * dtEnt);
                foreach (var m in _mobBuffer)
                {
                    m.CurrentLerpedX += (m.PositionX - m.CurrentLerpedX) * lerpTEnt;
                    m.CurrentLerpedY += (m.PositionY - m.CurrentLerpedY) * lerpTEnt;
                }
                foreach (var h in _harvestBuffer)
                {
                    h.CurrentLerpedX += (h.PositionX - h.CurrentLerpedX) * lerpTEnt;
                    h.CurrentLerpedY += (h.PositionY - h.CurrentLerpedY) * lerpTEnt;
                }
                foreach (var pl in _playersBuffer)
                {
                    pl.CurrentLerpedX += (pl.PositionX - pl.CurrentLerpedX) * lerpTEnt;
                    pl.CurrentLerpedY += (pl.PositionY - pl.CurrentLerpedY) * lerpTEnt;
                }
            }

            // Update ViewModels right after game logic and lerping
            UpdateViewModels();
            UpdateMobViewModels(mainPlayer);
            UpdateHarvestViewModels(mainPlayer);

            // Kısayol Dinleyicileri
            if (!_isChangingHotkey && !_isChangingMuteHotkey && !_isChangingHideAllHotkey)
            {
                bool currentKeyState = (GetAsyncKeyState(_toggleKey) & 0x8000) != 0;
                if (currentKeyState && !_lastKeyState) { _hideSettingsWindow = !_hideSettingsWindow; }
                _lastKeyState = currentKeyState;

                bool currentMuteKeyState = (GetAsyncKeyState(_muteToggleKey) & 0x8000) != 0;
                if (currentMuteKeyState && !_lastMuteKeyState) { _enableSoundAlerts = !_enableSoundAlerts; }
                _lastMuteKeyState = currentMuteKeyState;

                bool currentHideAllKeyState = (GetAsyncKeyState(_hideAllKey) & 0x8000) != 0;
                if (currentHideAllKeyState && !_lastHideAllKeyState) { _hideAllMenus = !_hideAllMenus; }
                _lastHideAllKeyState = currentHideAllKeyState;
            }
            else
            {
                _lastKeyState = (GetAsyncKeyState(_toggleKey) & 0x8000) != 0;
                _lastMuteKeyState = (GetAsyncKeyState(_muteToggleKey) & 0x8000) != 0;
                _lastHideAllKeyState = (GetAsyncKeyState(_hideAllKey) & 0x8000) != 0;
            }

            int previousEnemyCount = _lastEnemyCount;
            int enemyCount = mainPlayer != null ? CalculateEnemyCount(mainPlayer) : 0;
            int bossCount = 0;
            lock (_dataLock)
            {
                foreach (var m in _mobBuffer)
                {
                    if (_ignoredMobIds.Contains(m.TypeId)) continue;
                    _mobDatabase.TryGetValue(m.TypeId, out MobInfo info);
                    string dn = info?.Name ?? (string.IsNullOrEmpty(m.Name) ? "" : CleanName(m.Name));
                    if (string.IsNullOrEmpty(dn)) continue;

                    var typeInfo = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                    string un = (typeInfo?.UniqueName ?? m.Name ?? "").ToUpperInvariant();
                    bool isBoss = (m.TypeId != 51900 && m.TypeId != 51800 && m.TypeId != 53000) && (
                                      un.Contains("_BOSS") || un.EndsWith("BOSS") || un.StartsWith("BOSS_") || un.Contains("ASPECT") || un.Contains("TITAN") || un.Contains("GUARDIAN") || un.Contains("OLD_WHITE") 
                                      || un.Contains("DREAD LORD") || un.Contains("OVERLORD") || un.Contains("DEMON PRINCE") 
                                      || m.Rarity >= 3)
                                  && !_crownBlacklist.Contains(m.TypeId) && !un.Contains("TITANYUM");
                    if (isBoss) bossCount++;
                }
            }
            int resourceCount = CalculateResourceCount();

            // Ses Sistemi
            if (enemyCount > previousEnemyCount && _enableSoundAlerts)
            {
                string safeCheckMapId = _gameStateManager.CurrentMapId ?? "0000";
                string upperMapId = safeCheckMapId.ToUpperInvariant();
                bool isSafeZone = upperMapId.Contains("CITY") || upperMapId.Contains("PORTAL") || upperMapId.Contains("ISLAND") || upperMapId.Contains("HIDEOUT");

                if (!isSafeZone && (DateTime.Now - _lastBeepTime).TotalSeconds >= 2.0)
                {
                    try
                    {
                        string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "alert.wav");
                        if (System.IO.File.Exists(soundPath))
                        {
                            // Asenkron çalar ve bitene kadar dispose olmaz
                            var player = new System.Media.SoundPlayer(soundPath);
                            player.Load();
                            player.Play();
                        }
                        else
                        {
                            Console.Beep(800, 200);
                        }
                    }
                    catch (Exception ex)
                    {
                        Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                    }

                    _lastBeepTime = DateTime.Now;
                }
            }
            // Toast: Yeni düşman görüldüğünde bildirim ekle
            if (enemyCount > previousEnemyCount && _enableToastAlerts)
            {
                int newOnes = enemyCount - previousEnemyCount;
                string mapId = _gameStateManager.CurrentMapId ?? "0000";
                string upper = mapId.ToUpperInvariant();
                bool isSafe = upper.Contains("CITY") || upper.Contains("PORTAL") || upper.Contains("ISLAND") || upper.Contains("HIDEOUT");
                if (!isSafe)
                    _toasts.Add(($"+{newOnes} " + (Lang.Get("Toast_EnemyApproaching") ?? "dusman yaklasiyor!"), DateTime.Now, 0xFFDD4444));
            }

            _lastEnemyCount = enemyCount;

            // 1. WATERMARK
            if (_showWatermark && !_hideAllMenus)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

                ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

                if (!_watermarkMoveable)
                {
                    flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
                    ImGui.SetNextWindowPos(new Vector2(_watermarkX, _watermarkY), ImGuiCond.Always);
                }
                else
                {
                    ImGui.SetNextWindowPos(new Vector2(_watermarkX, _watermarkY), ImGuiCond.FirstUseEver);
                }

                if (bossCount > 0) ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.30f, 0.18f, 0.00f, 0.88f));
                else if (enemyCount > 0) ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.42f, 0.04f, 0.04f, 0.88f));
                else ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.05f, 0.06f, 0.09f, 0.82f));

                if (ImGui.Begin("EnemyCountWM", flags))
                {

                    ImGui.TextColored(new Vector4(0.60f, 0.60f, 0.60f, 1), Lang.Get("Watermark_Enemy") ?? "DUSMAN:"); ImGui.SameLine(0, 4);
                    ImGui.TextColored(enemyCount > 0 ? new Vector4(1f, 0.28f, 0.28f, 1) : new Vector4(0.65f, 0.65f, 0.65f, 1), $"{enemyCount}");
                    ImGui.SameLine(0, 6); ImGui.TextColored(new Vector4(0.25f, 0.25f, 0.30f, 1), "|"); ImGui.SameLine(0, 6);

                    ImGui.TextColored(new Vector4(0.60f, 0.60f, 0.60f, 1), Lang.Get("Watermark_Boss") ?? "BOSS:"); ImGui.SameLine(0, 4);
                    ImGui.TextColored(bossCount > 0 ? new Vector4(1f, 0.80f, 0.05f, 1) : new Vector4(0.65f, 0.65f, 0.65f, 1), $"{bossCount}");
                    ImGui.SameLine(0, 6); ImGui.TextColored(new Vector4(0.25f, 0.25f, 0.30f, 1), "|"); ImGui.SameLine(0, 6);

                    ImGui.TextColored(new Vector4(0.60f, 0.60f, 0.60f, 1), Lang.Get("Watermark_Resource") ?? "KAYNAK:"); ImGui.SameLine(0, 4);
                    ImGui.TextColored(resourceCount > 0 ? new Vector4(1f, 0.28f, 0.28f, 1) : new Vector4(0.65f, 0.65f, 0.65f, 1), $"{resourceCount}");

                    if (_watermarkMoveable)
                    {
                        var pos = ImGui.GetWindowPos();
                        _watermarkX = pos.X;
                        _watermarkY = pos.Y;
                    }
                }
                ImGui.End();

                ImGui.PopStyleColor();
                ImGui.PopStyleVar(2);
            }

            // --- TOAST BİLDİRİMLER (PREMIUM REDESIGN) ---
            {
                const float toastW = 320f, toastH = 45f, spacing = 10f, duration = 4.0f;
                _toasts.RemoveAll(t => (DateTime.Now - t.time).TotalSeconds > duration);
                if (_toasts.Count > 0 && !_hideAllMenus)
                {
                    var fgDl = ImGui.GetForegroundDrawList();
                    for (int ti = 0; ti < _toasts.Count; ti++)
                    {
                        var (msg, time, col) = _toasts[ti];
                        float elapsed = (float)(DateTime.Now - time).TotalSeconds;
                        
                        // Slide-in and Fade-out animations
                        float slideIn = Math.Min(1.0f, elapsed / 0.3f);
                        float fadeOut = elapsed > duration - 0.5f ? 1.0f - (elapsed - (duration - 0.5f)) / 0.5f : 1.0f;
                        float alpha = Math.Max(0f, Math.Min(1f, slideIn * fadeOut));
                        
                        // Smooth easing out for slide-in (Cubic ease out)
                        float easeSlide = 1.0f - (1.0f - slideIn) * (1.0f - slideIn) * (1.0f - slideIn);
                        float offsetX = -toastW * (1.0f - easeSlide);

                        Vector2 tp = new Vector2(20 + offsetX, 60 + ti * (toastH + spacing));
                        
                        // Premium Colors
                        uint bgDark = ImGui.ColorConvertFloat4ToU32(new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.Sidebar.X, Nightwatch.UserControls.MentalityTheme.Colors.Sidebar.Y, Nightwatch.UserControls.MentalityTheme.Colors.Sidebar.Z, alpha * 0.95f));
                        uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.Border.X, Nightwatch.UserControls.MentalityTheme.Colors.Border.Y, Nightwatch.UserControls.MentalityTheme.Colors.Border.Z, alpha * 0.5f));
                        uint accentCol = ((uint)(alpha * 0xFF) << 24) | (col & 0x00FFFFFF);
                        uint textCol = ImGui.ColorConvertFloat4ToU32(new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary.X, Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary.Y, Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary.Z, alpha));

                        // Background with rounded corners
                        fgDl.AddRectFilled(tp, tp + new Vector2(toastW, toastH), bgDark, 6f);
                        fgDl.AddRect(tp, tp + new Vector2(toastW, toastH), borderCol, 6f, ImDrawFlags.None, 1f);
                        
                        // Colored accent bar on the left (matches MentalityTheme aesthetics)
                        fgDl.AddRectFilled(tp, tp + new Vector2(4f, toastH), accentCol, 6f, ImDrawFlags.RoundCornersLeft);
                        
                        // Subtle inner shadow / glow effect
                        fgDl.AddRectFilledMultiColor(tp + new Vector2(4f, 0), tp + new Vector2(40f, toastH), accentCol & 0x44FFFFFF, 0x00000000, 0x00000000, accentCol & 0x44FFFFFF);

                        // Message Text
                        var ts = ImGui.CalcTextSize(msg);
                        fgDl.AddText(tp + new Vector2(18f, (toastH - ts.Y) / 2f), textCol, msg);
                    }
                }
            }

            // 2. PLAYER LIST
            if (_showPlayerList && !_hideAllMenus && _playersBuffer.Count > 0)
            {
                ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;
                if (_playerListMoveable) ImGui.SetNextWindowPos(new Vector2(_playerListX, _playerListY), ImGuiCond.FirstUseEver);
                else { flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoInputs; ImGui.SetNextWindowPos(new Vector2(_playerListX, _playerListY), ImGuiCond.Always); }

                ImGui.SetNextWindowBgAlpha(0.50f);
                string windowTitle = _playerListMoveable ? "TASI BENI (LISTE)" : "PlayerListPanel";

                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

                if (ImGui.Begin(windowTitle, flags))
                {
                    if (_playerListMoveable) { ImGui.TextColored(new Vector4(0.18f, 0.52f, 0.92f, 1f), Lang.Get("UI_MoveMode") ?? "Taşıma Modu Aktif"); var pos = ImGui.GetWindowPos(); _playerListX = pos.X; _playerListY = pos.Y; }

                    lock (_dataLock)
                    {
                        foreach (var p in _playerViewModels)
                        {
                            string finalWeapon = p.WeaponIP > 0 ? $"[{p.WeaponIP}] {p.WeaponName}" : p.WeaponName;
                            string finalHead = p.HeadIP > 0 ? $"[{p.HeadIP}] {p.HeadName}" : p.HeadName;
                            string finalArmor = p.ArmorIP > 0 ? $"[{p.ArmorIP}] {p.ArmorName}" : p.ArmorName;
                            string finalShoes = p.ShoesIP > 0 ? $"[{p.ShoesIP}] {p.ShoesName}" : p.ShoesName;
                            string finalCape = p.CapeIP > 0 ? $"[{p.CapeIP}] {p.CapeName}" : p.CapeName;

                            if (_enableLogging)
                            {
                                string curMap = _gameStateManager.CurrentMapId ?? "0000";
                                RadarLogger.LogPlayer(curMap, p.Name, 0, finalWeapon, finalHead, finalArmor, finalShoes, finalCape);
                            }

                            string tierStr = p.AverageIP > 0 ? $"IP:{p.AverageIP} (T{p.WeaponIP},{p.HeadIP},{p.ArmorIP},{p.ShoesIP},{p.CapeIP})" : $"(T{p.WeaponIP},{p.HeadIP},{p.ArmorIP},{p.ShoesIP},{p.CapeIP})";
                            ImGui.TextColored(p.ArrowColor, p.DirectionArrow); ImGui.SameLine();
                            ImGui.TextColored(p.NameColor, $"{p.Name}"); ImGui.SameLine();

                            if (p.MaxHealth > 0)
                            {
                                ImGui.TextColored(p.HealthColor, p.HealthText); ImGui.SameLine();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.PushStyleColor(ImGuiCol.Text, p.NameColor);
                                ImGui.Text($"{p.Name}");
                                ImGui.PopStyleColor();
                                if (!string.IsNullOrEmpty(p.Guild))
                                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 1f, 1), $"[{p.Guild}]" + (string.IsNullOrEmpty(p.Alliance) ? "" : $" <{p.Alliance}>"));
                                ImGui.Separator();

                                ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1), (Lang.Get("Player_AvgIP") ?? "Ortalama IP : ") + $"{p.AverageIP}");
                                ImGui.Spacing();
                                
                                int[] tooltipSlots = new[] { 0, 2, 3, 4, 6, 1 }; // Weapon, Head, Armor, Shoes, Cape, OffHand
                                float gridIconSz = 48f;
                                
                                if (ImGui.BeginTable("EqGrid", 3, ImGuiTableFlags.None))
                                {
                                    for(int i=0; i < 6; i++)
                                    {
                                        if (i == 0 || i == 3) ImGui.TableNextRow();
                                        ImGui.TableNextColumn();

                                        int sIdx = tooltipSlots[i];
                                        string internalName = GetEquipInternalName(p.RawPlayer, sIdx);
                                        string? diskPath = internalName != null ? GetItemRenderPath(internalName) : null;
                                        
                                        Vector2 cPos = ImGui.GetCursorScreenPos();
                                        var dl = ImGui.GetWindowDrawList();
                                        dl.AddRectFilled(cPos, cPos + new Vector2(gridIconSz, gridIconSz), 0xBB0B0D14, 6f);
                                        dl.AddRect(cPos, cPos + new Vector2(gridIconSz, gridIconSz), 0x44FFFFFF, 6f, ImDrawFlags.None, 1f);

                                        if (diskPath != null)
                                        {
                                            try
                                            {
                                                AddOrGetImagePointer(diskPath, true, out IntPtr tex, out uint iw, out uint ih);
                                                if (tex != IntPtr.Zero)
                                                    ImGui.Image(tex, new Vector2(gridIconSz, gridIconSz));
                                                else
                                                {
                                                    float t = (float)(ImGui.GetTime() * 3.0 + i) % 1.0f;
                                                    uint spinCol = ((uint)(t * 0xFF) << 24) | 0x00FFAA00;
                                                    dl.AddCircleFilled(cPos + new Vector2(gridIconSz / 2, gridIconSz / 2), 5f, spinCol);
                                                    ImGui.Dummy(new Vector2(gridIconSz, gridIconSz));
                                                }
                                            }
                                            catch { ImGui.Dummy(new Vector2(gridIconSz, gridIconSz)); }
                                        }
                                        else
                                        {
                                            int eqId = (p.EquipmentRaw != null && p.EquipmentRaw.Length > sIdx) ? p.EquipmentRaw[sIdx] : 0;
                                            if (eqId > 0)
                                            {
                                                float t = (float)(ImGui.GetTime() * 3.0 + i) % 1.0f;
                                                uint spinCol = ((uint)(t * 0xFF) << 24) | 0x00FFAA00;
                                                dl.AddCircleFilled(cPos + new Vector2(gridIconSz / 2, gridIconSz / 2), 5f, spinCol);
                                            }
                                            ImGui.Dummy(new Vector2(gridIconSz, gridIconSz));
                                        }
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.EndTooltip();
                            }
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), tierStr);
                            ImGui.TextColored(new Vector4(1, 0.6f, 0, 1), $"{p.WeaponName} | {p.CapeName}");
                            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1), $"{p.HeadName} | {p.ArmorName} | {p.ShoesName}");
                            ImGui.Separator();
                        }
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar(2);
            }

            // 2b. EKİPMAN KARTLARI
            if (_showEquipmentCards && !_hideAllMenus && mainPlayer != null)
            {
                int maxSlots = Math.Clamp(_equipmentCardsMaxSlots, 1, _equipCardSlots.Length);
                float memorySeconds = Math.Max(0f, _equipmentCardsMemorySeconds);
                DateTime now = DateTime.Now;
                const float IconSz = 48f;
                const float Pad = 6f;
                const float SlotGap = 3f;

                lock (_dataLock)
                {
                    foreach (var px in _playersBuffer)
                    {
                        if (IsWhitelisted(px, mainPlayer)) continue;
                        _enemyLastSeenAt[px.Id] = now;
                        _enemyCardCache[px.Id] = ClonePlayerForCard(px);
                    }

                    var expiredIds = _enemyLastSeenAt
                        .Where(kv => (now - kv.Value).TotalSeconds > memorySeconds)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var expiredId in expiredIds)
                    {
                        _enemyLastSeenAt.Remove(expiredId);
                        _enemyCardCache.Remove(expiredId);
                    }

                    var aliveIdsSet = new HashSet<int>();
                    foreach (var px in _playersBuffer)
                        if (!IsWhitelisted(px, mainPlayer)) aliveIdsSet.Add(px.Id);

                    for (int si = maxSlots; si < _equipCardSlots.Length; si++)
                        _equipCardSlots[si] = null;

                    for (int si = 0; si < maxSlots; si++)
                    {
                        if (!_equipCardSlots[si].HasValue) continue;
                        int cachedId = _equipCardSlots[si]!.Value;
                        if (aliveIdsSet.Contains(cachedId)) continue;

                        if (!_enemyLastSeenAt.TryGetValue(cachedId, out var lastSeen) || (now - lastSeen).TotalSeconds > memorySeconds)
                            _equipCardSlots[si] = null;
                    }

                    int writeIdx = 0;
                    for (int readIdx = 0; readIdx < maxSlots; readIdx++)
                    {
                        if (_equipCardSlots[readIdx].HasValue)
                        {
                            _equipCardSlots[writeIdx] = _equipCardSlots[readIdx];
                            if (readIdx != writeIdx) _equipCardSlots[readIdx] = null;
                            writeIdx++;
                        }
                    }

                    var newEnemies = _playersBuffer
                        .Where(px => !IsWhitelisted(px, mainPlayer) && !Array.Exists(_equipCardSlots, s => s == px.Id))
                        .OrderBy(px => Vector2.Distance(
                            new Vector2(px.CurrentLerpedX, px.CurrentLerpedY),
                            new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY)))
                        .ToList();

                    foreach (var px in newEnemies)
                    {
                        for (int si = 0; si < maxSlots; si++)
                        {
                            if (!_equipCardSlots[si].HasValue)
                            {
                                _equipCardSlots[si] = px.Id;
                                break;
                            }
                        }
                    }
                }

                for (int si = 0; si < maxSlots; si++)
                {
                    if (!_equipCardSlots[si].HasValue) continue;
                    int targetId = _equipCardSlots[si]!.Value;

                    Player? ep = null;
                    lock (_dataLock)
                    {
                        ep = _playersBuffer.FirstOrDefault(px => px.Id == targetId);
                        if (ep == null)
                        {
                            if (_enemyLastSeenAt.TryGetValue(targetId, out var lastSeen) && (now - lastSeen).TotalSeconds <= memorySeconds)
                                _enemyCardCache.TryGetValue(targetId, out ep);
                        }
                    }

                    if (ep == null) { _equipCardSlots[si] = null; continue; }

                    if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                    if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                    float cardW = Pad * 2 + IconSz * 5 + Pad * 4;
                    float cardH = Pad * 2 + IconSz + 36f;
                    if (_equipmentCardsX < 0f) _equipmentCardsX = _cachedPrimaryScreenW - cardW - 12f;
                    float maxCardX = Math.Max(0f, _cachedPrimaryScreenW - cardW - 8f);
                    float maxCardY = Math.Max(0f, _cachedPrimaryScreenH - cardH - 8f);
                    _equipmentCardsX = Math.Clamp(_equipmentCardsX, 0f, maxCardX);
                    _equipmentCardsY = Math.Clamp(_equipmentCardsY, 0f, maxCardY);
                    float baseCardX = _equipmentCardsX;
                    float baseCardY = _equipmentCardsY;
                    float cardX = baseCardX;
                    float cardY = baseCardY + si * (cardH + SlotGap);

                    bool canMoveCard = _equipmentCardsMoveable && si == 0;
                    ImGui.SetNextWindowPos(new Vector2(cardX, cardY), canMoveCard ? ImGuiCond.FirstUseEver : ImGuiCond.Always);
                    ImGui.SetNextWindowSize(new Vector2(cardW, cardH), ImGuiCond.Always);
                    ImGui.SetNextWindowBgAlpha(0.82f);

                    ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, Pad));

                    ImGuiWindowFlags ecFlags = ImGuiWindowFlags.NoScrollbar
                                             | ImGuiWindowFlags.NoSavedSettings
                                             | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav;

                    if (!canMoveCard)
                        ecFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove;

                    string cardWindowTitle = canMoveCard
                        ? $"{Lang.Get("Player_EquipCardsMoveWindow") ?? "Equipment Cards (Move)"}##EquipCard_{si}"
                        : $"EquipCard_{si}";

                    if (ImGui.Begin(cardWindowTitle, ecFlags))
                    {
                        if (canMoveCard)
                        {
                            var movePos = ImGui.GetWindowPos();
                            _equipmentCardsX = movePos.X;
                            _equipmentCardsY = movePos.Y;
                        }

                        var dl = ImGui.GetWindowDrawList();
                        Vector2 winPos = ImGui.GetWindowPos();

                        int[] slotIdx = { 0, 1, 2, 3, 4 };

                        int eqWeap = GetItemPower(ep.Equipment?.Length > 0 ? ep.Equipment[0] : 0);
                        int eqOff = GetItemPower(ep.Equipment?.Length > 1 ? ep.Equipment[1] : 0);
                        int eqCap = GetItemPower(ep.Equipment?.Length > 2 ? ep.Equipment[2] : 0);
                        int eqArm = GetItemPower(ep.Equipment?.Length > 3 ? ep.Equipment[3] : 0);
                        int eqShoe = GetItemPower(ep.Equipment?.Length > 4 ? ep.Equipment[4] : 0);
                        int eqCape = GetItemPower(ep.Equipment?.Length > 6 ? ep.Equipment[6] : 0);

                        if (eqWeap > 0 && eqOff == 0) eqOff = eqWeap;
                        int avgIP = (eqWeap + eqOff + eqCap + eqArm + eqShoe + eqCape) / 6;

                        Vector4 nameCol = avgIP >= 1300 ? new Vector4(1f, 0.2f, 0.2f, 1)
                                        : avgIP >= 1000 ? new Vector4(1f, 0.55f, 0f, 1)
                                        : avgIP >= 700 ? new Vector4(1f, 0.95f, 0.2f, 1)
                                        : avgIP > 0 ? new Vector4(0.3f, 1f, 0.3f, 1)
                                                         : new Vector4(0.7f, 0.7f, 0.7f, 1);

                        float dist2 = Vector2.Distance(
                            new Vector2(ep.CurrentLerpedX, ep.CurrentLerpedY),
                            new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));

                        for (int k = 0; k < slotIdx.Length; k++)
                        {
                            int eqId = (ep.Equipment != null && ep.Equipment.Length > slotIdx[k]) ? ep.Equipment[slotIdx[k]] : 0;
                            string internalName = GetEquipInternalName(ep, slotIdx[k]);
                            string? diskPath = internalName != null ? GetItemRenderPath(internalName) : null;

                            Vector2 iconMin = winPos + new Vector2(Pad + k * (IconSz + Pad), Pad);
                            Vector2 iconMax = iconMin + new Vector2(IconSz, IconSz);

                            dl.AddRectFilled(iconMin, iconMax, 0xBB0B0D14, 6f);
                            dl.AddRect(iconMin, iconMax, 0x44FFFFFF, 6f, ImDrawFlags.None, 1f);

                            if (diskPath != null)
                            {
                                try
                                {
                                    AddOrGetImagePointer(diskPath, true, out IntPtr tex, out uint iw, out uint ih);
                                    if (tex != IntPtr.Zero)
                                        dl.AddImage(tex, iconMin, iconMax);
                                    else if (eqId > 0)
                                    {
                                        float t = (float)(ImGui.GetTime() * 3.0 + k) % 1.0f;
                                        uint spinCol = ((uint)(t * 0xFF) << 24) | 0x00FFAA00;
                                        dl.AddCircleFilled(iconMin + new Vector2(IconSz / 2, IconSz / 2), 5f, spinCol);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                                    if (eqId > 0)
                                    {
                                        float t = (float)(ImGui.GetTime() * 3.0 + k) % 1.0f;
                                        uint spinCol = ((uint)(t * 0xFF) << 24) | 0x00FFAA00;
                                        dl.AddCircleFilled(iconMin + new Vector2(IconSz / 2, IconSz / 2), 5f, spinCol);
                                    }
                                }
                            }
                            else if (eqId > 0)
                            {
                                float t = (float)(ImGui.GetTime() * 3.0 + k) % 1.0f;
                                uint spinCol = ((uint)(t * 0xFF) << 24) | 0x00FFAA00;
                                dl.AddCircleFilled(iconMin + new Vector2(IconSz / 2, IconSz / 2), 5f, spinCol);
                            }
                        }

                        float textStartY = Pad + IconSz + 4f;
                        ImGui.SetCursorPos(new Vector2(Pad, textStartY));

                        bool hasHealthData = ep.MaxHealth > 0f;
                        float hpRatio = hasHealthData ? Math.Clamp(ep.CurrentHealth / ep.MaxHealth, 0f, 1f) : 0f;
                        var (hpCurrent, hpMax) = hasHealthData
                            ? GetDisplayHealthValues(ep.CurrentHealth, ep.MaxHealth)
                            : (0, 0);
                        Vector4 hpCol = new Vector4(1f - hpRatio, hpRatio, 0.15f, 1f);

                        ImGui.TextColored(nameCol, ep.Name);
                        /* if (hasHealthData)
                         {
                             ImGui.SameLine(0, 8);
                             ImGui.TextColored(hpCol, $"HP:{hpCurrent}/{hpMax}");
                         }*/
                        if (!string.IsNullOrEmpty(ep.Guild))
                            ImGui.SameLine(0, 8);
                        ImGui.TextColored(new Vector4(0.6f, 0.6f, 1f, 1), $"[{ep.Guild}]");
                        if (avgIP > 0)
                        {
                            ImGui.SameLine(0, 8);
                            ImGui.TextColored(nameCol, $"IP:{avgIP}");
                        }

                        float hpBarWidth = Math.Max(60f, cardW - (Pad * 2f));
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, hpCol);
                        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.10f, 0.10f, 0.12f, 1f));
                        ImGui.ProgressBar(hpRatio, new Vector2(hpBarWidth, 5f), hasHealthData ? string.Empty : "N/A");
                        ImGui.PopStyleColor(2);

                    }

                    ImGui.End();
                    ImGui.PopStyleVar(3);
                }
            }

            // 3. RADAR WIDGET
            if (_detachRadar && !_hideAllMenus)
            {
                ImGuiWindowFlags radarFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
                if (!_radarMoveable) radarFlags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;

                ImGui.SetNextWindowBgAlpha(0.0f);
                ImGui.SetNextWindowSize(new Vector2(_radarSize, _radarSize));

                if (_shouldUpdateRadarPos)
                {
                    ImGui.SetNextWindowPos(new Vector2(_radarWinX, _radarWinY), ImGuiCond.Always);
                    _shouldUpdateRadarPos = false;
                }
                else
                {
                    ImGui.SetNextWindowPos(new Vector2(_radarWinX, _radarWinY), ImGuiCond.FirstUseEver);
                }

                if (ImGui.Begin("MiniRadarWidget", radarFlags))
                {
                    var winPos = ImGui.GetWindowPos();

                    // Radar taşındığında yeni pozisyonu kaydet
                    if (_radarMoveable)
                    {
                        _radarWinX = winPos.X;
                        _radarWinY = winPos.Y;
                    }

                    if (_radarMoveable)
                    {
                        // Temamıza uygun şık mavi renk çerçeve ve metin
                        uint accentBlue = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.52f, 0.92f, 1f));
                        ImGui.GetWindowDrawList().AddRect(winPos, winPos + ImGui.GetWindowSize(), accentBlue, 12f, ImDrawFlags.None, 2f); // 12f köşe yumuşatması eklendi
                        ImGui.TextColored(new Vector4(0.18f, 0.52f, 0.92f, 1f), Lang.Get("UI_MoveRadar") ?? "Radarı Taşı");
                    }
                    if (mainPlayer != null)
                        DrawRadar(ImGui.GetWindowDrawList(), winPos, ImGui.GetWindowSize(), mainPlayer);
                    if (_radarMoveable)
                    {
                        ImGui.GetWindowDrawList().AddRect(winPos, winPos + ImGui.GetWindowSize(), 0xFF00FF00);
                        ImGui.Text(Lang.Get("UI_MoveRadar") ?? "Move");
                    }
                }
                ImGui.End();
            }

            // 4. MODERN SETTINGS UI
            if (!_hideSettingsWindow && !_hideAllMenus)
            {
                ImGui.SetNextWindowSize(new Vector2(900, 600), ImGuiCond.FirstUseEver);

                // Add shadow behind window
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

                if (ImGui.Begin("Nightwatch Radar ##ModernUI", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar))
                {
                    float sidebarWidth = 220f;
                    Vector2 winPos = ImGui.GetWindowPos();
                    Vector2 winSize = ImGui.GetWindowSize();
                    var drawList = ImGui.GetWindowDrawList();

                    // Sidebar Background Draw
                    drawList.AddRectFilled(winPos, winPos + new Vector2(sidebarWidth, winSize.Y), Nightwatch.UserControls.MentalityTheme.Colors.SidebarU32, 14f, ImDrawFlags.RoundCornersLeft);

                    // Animated Logo Glow Background
                    float time = (float)ImGui.GetTime();
                    float glowIntensity = (float)(Math.Sin(time * 1.0f) * 0.5f + 0.5f); // 0 to 1
                    uint glowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(
                        Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.X, 
                        Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Y, 
                        Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Z, 
                        0.25f * glowIntensity));
                    
                    drawList.AddRectFilledMultiColor(winPos, winPos + new Vector2(sidebarWidth, winSize.Y / 2.5f), 
                        glowCol, glowCol, 0x00000000, 0x00000000);

                    RenderSidebar(sidebarWidth);

                    ImGui.SameLine();

                    // Main Content Area
                    ImGui.BeginGroup();
                    {
                        // Custom Header Area
                        ImGui.BeginChild("Header", new Vector2(0, 56), ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
                        {
                            ImGui.SetCursorPos(new Vector2(20, 20));
                            Nightwatch.UserControls.MentalityTheme.Breadcrumb("Nightwatch", _tabs[_activeTab].ToUpperInvariant());

                            float headerWidth = ImGui.GetWindowWidth();
                            
                            // Status indicators removed from header
                            
                            // Window controls
                            ImGui.SetCursorPos(new Vector2(headerWidth - 80f, 16f));
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                            ImGui.PushStyleColor(ImGuiCol.Text, Nightwatch.UserControls.MentalityTheme.Colors.TextSecondary);

                            if (ImGui.Button("-##MinBtn", new Vector2(30, 30)))
                            {
                                string balloonMsg = Lang.Get("App_System_Tray") ?? "Minimized to tray";
                                _hideSettingsWindow = true;
                                if (_trayIcon != null)
                                    _trayIcon.ShowBalloonTip(2000, "Nightwatch", balloonMsg, System.Windows.Forms.ToolTipIcon.Info);
                            }
                            ImGui.SameLine(0, 4);

                            ImGui.PushStyleColor(ImGuiCol.Text, Nightwatch.UserControls.MentalityTheme.Colors.AccentDanger);
                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.AccentDanger.X, Nightwatch.UserControls.MentalityTheme.Colors.AccentDanger.Y, Nightwatch.UserControls.MentalityTheme.Colors.AccentDanger.Z, 0.2f));

                            if (ImGui.Button("X##ClsBtn", new Vector2(30, 30)))
                            {
                                if (_trayIcon != null) _trayIcon.Dispose();
                                Environment.Exit(0);
                            }
                            ImGui.PopStyleColor(2);
                            ImGui.PopStyleColor(2);
                        }
                        ImGui.EndChild(); 
                        
                        Nightwatch.UserControls.MentalityTheme.GradientSeparator(Nightwatch.UserControls.MentalityTheme.Colors.Border, 1f);

                        // Content Tab
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(24, 20));
                        ImGui.BeginChild("TabContent", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None);
                        {
                            RenderActiveTab();
                        }
                        ImGui.EndChild();
                        ImGui.PopStyleVar();
                    }
                    ImGui.EndGroup();
                    
                    // Npcap Status Indicator at bottom right
                    bool npcapInstalled = System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll")) || 
                                          System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wpcap.dll")); // Fallback to WinPcap/standard location
                    string npcapText = "[Npcap]";
                    Vector2 textSize = ImGui.CalcTextSize(npcapText);
                    Vector2 textPos = new Vector2(winPos.X + winSize.X - textSize.X - 15, winPos.Y + winSize.Y - textSize.Y - 15);
                    uint npcapColor = npcapInstalled ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f)) : ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
                    drawList.AddText(textPos, npcapColor, npcapText);
                }
                
                ImGui.PopStyleVar(2);
                ImGui.End();
            }
        } // Render metodunun bitiş süslü parantezi

        private static Player ClonePlayerForCard(Player p)
        {
            return new Player
            {
                Id = p.Id,
                Name = p.Name,
                Guild = p.Guild,
                Alliance = p.Alliance,
                Faction = p.Faction,
                PositionX = p.PositionX,
                PositionY = p.PositionY,
                CurrentLerpedX = p.CurrentLerpedX,
                CurrentLerpedY = p.CurrentLerpedY,
                CurrentHealth = p.CurrentHealth,
                MaxHealth = p.MaxHealth,
                Equipment = p.Equipment?.ToArray() ?? Array.Empty<int>()
            };
        }

        private static (int current, int max) GetDisplayHealthValues(float currentHealth, float maxHealth)
        {
            if (maxHealth <= 0f)
                return (0, 0);

            static bool HasFraction(float v) => MathF.Abs(v - MathF.Round(v)) > 0.001f;

            bool needsX100Scale =
                currentHealth <= maxHealth &&
                maxHealth <= 200f &&
                (HasFraction(currentHealth) || HasFraction(maxHealth));

            float scale = needsX100Scale ? 100f : 1f;

            int current = (int)MathF.Round(MathF.Max(0f, currentHealth * scale));
            int max = (int)MathF.Round(MathF.Max(0f, maxHealth * scale));
            return (current, max);
        }

        #endregion
    }
}






