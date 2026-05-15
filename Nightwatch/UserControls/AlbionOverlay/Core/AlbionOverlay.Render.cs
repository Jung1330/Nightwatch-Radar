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
        public static string DungeonChestMessage = "";
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
                    System.Console.WriteLine($"Error Code : 69 | {ex.Message}");
                }
            }
        }

        #region UI Rendering
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
                _tabs[6] = Lang.Get("Tab_Device") ?? "Device"; // YENİ EKLENDİ
                _lastTabLanguage = currentLang;
            }

            if (!_isSizeFixed) FixLayoutWait();
            if (!_isIconSet) { SetApplicationWindowIcon(); _isIconSet = true; }

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
                    _lastMapId = currentMapId;

                    _mapGlobalOffsetX = 0f;
                    _mapGlobalOffsetY = 0f;

                    _announcedChests.Clear();

                    // --- HARİTA TEMİZLEME (GHOST MOB FIX) ---
                    _playerTrails.Clear();
                    _prevPlayerPos.Clear();
                }
                else
                {
                    _playersBuffer.Clear();
                    _mobBuffer.Clear();
                    _harvestBuffer.Clear();
                    _gameStateManager.GetOtherPlayers(_playersBuffer);
                    _gameStateManager.GetMobs(_mobBuffer);
                    _gameStateManager.GetHarvestables(_harvestBuffer);
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
                float lerpT = 1f - (float)Math.Exp(-40f * dt);
                _smoothPlayerX += (mainPlayer.PositionX - _smoothPlayerX) * lerpT;
                _smoothPlayerY += (mainPlayer.PositionY - _smoothPlayerY) * lerpT;
                mainPlayer.CurrentLerpedX = _smoothPlayerX;
                mainPlayer.CurrentLerpedY = _smoothPlayerY;
            }

            // --- SMOOTH ENTITY POSITIONS (mob/kaynak lazer Ã§izgilerini akÄ±cÄ± yapar) ---
            {
                float dtEnt = Math.Min(ImGui.GetIO().DeltaTime, 0.1f);
                float lerpTEnt = 1f - (float)Math.Exp(-18f * dtEnt);
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

            // KÄ±sayol Dinleyicileri
            if (!_isChangingHotkey && !_isChangingMuteHotkey)
            {
                bool currentKeyState = (GetAsyncKeyState(_toggleKey) & 0x8000) != 0;
                if (currentKeyState && !_lastKeyState) { _hideSettingsWindow = !_hideSettingsWindow; }
                _lastKeyState = currentKeyState;

                bool currentMuteKeyState = (GetAsyncKeyState(_muteToggleKey) & 0x8000) != 0;
                if (currentMuteKeyState && !_lastMuteKeyState) { _enableSoundAlerts = !_enableSoundAlerts; }
                _lastMuteKeyState = currentMuteKeyState;
            }
            else
            {
                _lastKeyState = (GetAsyncKeyState(_toggleKey) & 0x8000) != 0;
                _lastMuteKeyState = (GetAsyncKeyState(_muteToggleKey) & 0x8000) != 0;
            }

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

                    string un = dn.ToUpperInvariant();
                    bool isBoss = (un.Contains("BOSS") || un.Contains("ASPECT") || un.Contains("TITAN") || un.Contains("GUARDIAN") || un.Contains("OLD_WHITE"))
                                  && !_crownBlacklist.Contains(m.TypeId);
                    if (isBoss) bossCount++;
                }
            }
            int resourceCount = CalculateResourceCount();

            // Ses Sistemi
            if (enemyCount > _lastEnemyCount && _enableSoundAlerts)
            {
                string safeCheckMapId = _gameStateManager.CurrentMapId ?? "0000";
                string upperMapId = safeCheckMapId.ToUpperInvariant();
                bool isSafeZone = upperMapId.Contains("CITY") || upperMapId.Contains("PORTAL") || upperMapId.Contains("ISLAND") || upperMapId.Contains("HIDEOUT");

                if (!isSafeZone && (DateTime.Now - _lastBeepTime).TotalSeconds >= 2.0)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "alert.wav");
                            if (System.IO.File.Exists(soundPath)) { using (var player = new System.Media.SoundPlayer(soundPath)) { player.Play(); } }
                            else { Console.Beep(800, 200); }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Error Code : 54 | {ex.Message}");
                        }
                    });
                    _lastBeepTime = DateTime.Now;
                }
            }
            // Toast: Yeni düşman görüldüğünde bildirim ekle
            if (enemyCount > _lastEnemyCount)
            {
                int newOnes = enemyCount - _lastEnemyCount;
                string mapId = _gameStateManager.CurrentMapId ?? "0000";
                string upper = mapId.ToUpperInvariant();
                bool isSafe = upper.Contains("CITY") || upper.Contains("PORTAL") || upper.Contains("ISLAND") || upper.Contains("HIDEOUT");
                if (!isSafe)
                    _toasts.Add(($"+{newOnes} " + (Lang.Get("Toast_EnemyApproaching") ?? "dusman yaklasiyor!"), DateTime.Now, 0xFFDD4444));
            }

            _lastEnemyCount = enemyCount;

            // 1. WATERMARK
            if (_showWatermark)
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

            // --- TOAST BİLDİRİMLER ---
            {
                const float toastW = 260f, toastH = 30f, spacing = 5f, duration = 3.0f;
                _toasts.RemoveAll(t => (DateTime.Now - t.time).TotalSeconds > duration);
                if (_toasts.Count > 0)
                {
                    var fgDl = ImGui.GetForegroundDrawList();
                    for (int ti = 0; ti < _toasts.Count; ti++)
                    {
                        var (msg, time, col) = _toasts[ti];
                        float elapsed = (float)(DateTime.Now - time).TotalSeconds;
                        float alpha = elapsed < duration - 0.5f ? 1.0f : 1.0f - (elapsed - (duration - 0.5f)) / 0.5f;
                        alpha = Math.Max(0f, Math.Min(1f, alpha));
                        Vector2 tp = new Vector2(20, 55 + ti * (toastH + spacing));
                        uint bgDark = ((uint)(alpha * 0xCC) << 24) | 0x00080C10;
                        uint border = ((uint)(alpha * 0xBB) << 24) | (col & 0x00FFFFFF);
                        uint textCol = ((uint)(alpha * 0xFF) << 24) | 0x00FFFFFF;
                        fgDl.AddRectFilled(tp, tp + new Vector2(toastW, toastH), bgDark, 6f);
                        fgDl.AddRect(tp, tp + new Vector2(toastW, toastH), border, 6f, ImDrawFlags.None, 1.2f);
                        var ts = ImGui.CalcTextSize(msg);
                        fgDl.AddText(tp + new Vector2(10f, (toastH - ts.Y) / 2f), textCol, msg);
                    }
                }
            }

            // --- YAKLASAN DUSMAN YON GOSTERGESI (DANGER COMPASS) ---
            if (_showDangerCompass && mainPlayer != null)
            {
                if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                float scrW = _cachedPrimaryScreenW, scrH = _cachedPrimaryScreenH;
                float dangerDistanceSq = (_renderDistance * 1.5f) * (_renderDistance * 1.5f);
                float pulse = 0.70f + 0.30f * (float)Math.Sin(ImGui.GetTime() * 6.0);
                var fgDl = ImGui.GetForegroundDrawList();
                lock (_dataLock)
                {
                    foreach (var p in _playersBuffer)
                    {
                        if (_whitelist.Contains(p.Name)) continue;
                        if (!_prevPlayerPos.TryGetValue(p.Id, out var prev)) continue;

                        float cDistSq = Vector2.DistanceSquared(new Vector2(p.CurrentLerpedX, p.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY));
                        if (cDistSq > dangerDistanceSq) continue;

                        float cDist = MathF.Sqrt(cDistSq);
                        float dDist = cDist - prev.dist;
                        if (dDist >= -0.25f) continue;

                        float ldx = p.CurrentLerpedX - mainPlayer.CurrentLerpedX;
                        float ldy = p.CurrentLerpedY - mainPlayer.CurrentLerpedY;
                        if (_swapXY) { float tmp = ldx; ldx = ldy; ldy = tmp; }
                        if (_invertX) ldx = -ldx;
                        if (_invertY) ldy = -ldy;
                        float rad = _radarRotation * MathF.PI / 180f;
                        float angle = MathF.Atan2(ldy, ldx) + rad;
                        float rotX = MathF.Cos(angle);
                        float rotY = MathF.Sin(angle);

                        if (rotX * rotX + rotY * rotY < 0.001f) continue;
                        Vector2 dir = Vector2.Normalize(new Vector2(rotX, rotY));

                        float margin = 55f;
                        Vector2 sc = new Vector2(scrW / 2f, scrH / 2f);
                        float tVal = float.MaxValue;
                        if (dir.X > 0.001f) tVal = Math.Min(tVal, (scrW - margin - sc.X) / dir.X);
                        else if (dir.X < -0.001f) tVal = Math.Min(tVal, (margin - sc.X) / dir.X);
                        if (dir.Y > 0.001f) tVal = Math.Min(tVal, (scrH - margin - sc.Y) / dir.Y);
                        else if (dir.Y < -0.001f) tVal = Math.Min(tVal, (margin - sc.Y) / dir.Y);
                        if (tVal == float.MaxValue || tVal < 0) continue;
                        Vector2 edgePt = sc + dir * tVal;
                        Vector2 inward = -dir;
                        Vector2 perp = new Vector2(-inward.Y, inward.X);
                        float aLen = 22f, aWid = 11f;
                        uint fA = (uint)(pulse * 210) << 24;
                        uint bA = (uint)(pulse * 195) << 24;
                        Vector2 tip = edgePt;
                        Vector2 bL = edgePt - inward * aLen + perp * aWid;
                        Vector2 bR = edgePt - inward * aLen - perp * aWid;
                        fgDl.AddTriangleFilled(tip, bL, bR, fA | 0x002255FF);
                        fgDl.AddTriangle(tip, bL, bR, bA | 0x00FFAA00, 1.5f);
                        string lbl2 = $"{p.Name}";
                        var ts2 = ImGui.CalcTextSize(lbl2);
                        Vector2 la = edgePt + inward * (aLen + 4f) + new Vector2(-ts2.X / 2f, -ts2.Y / 2f);
                        fgDl.AddRectFilled(la - new Vector2(4, 2), la + new Vector2(ts2.X + 4, ts2.Y + 2), 0xCC0B0D10, 4f);
                        fgDl.AddRect(la - new Vector2(4, 2), la + new Vector2(ts2.X + 4, ts2.Y + 2), bA | 0x00FFAA00, 4f, ImDrawFlags.None, 1f);
                        fgDl.AddText(la, 0xFFFFFFFF, lbl2);
                    }
                }
            }

            // 2. PLAYER LIST
            if (_showPlayerList && _playersBuffer.Count > 0)
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
                        foreach (var p in _playersBuffer)
                        {
                            if (_whitelist.Contains(p.Name)) continue;
                            float dist = (mainPlayer != null) ? Vector2.Distance(new Vector2(p.CurrentLerpedX, p.CurrentLerpedY), new Vector2(mainPlayer.CurrentLerpedX, mainPlayer.CurrentLerpedY)) : 0;

                            int pWeap = 0, pOff = 0, pCap = 0, pArm = 0, pShoe = 0, pCape = 0;
                            string wName = "-", oName = "-", hName = "-", aName = "-", sName = "-", cName = "-";

                            if (p.Equipment != null)
                            {
                                if (p.Equipment.Length > 0) { pWeap = GetItemPower(p.Equipment[0]); wName = GetItemName(p.Equipment[0]); }
                                if (p.Equipment.Length > 1) { pOff = GetItemPower(p.Equipment[1]); oName = GetItemName(p.Equipment[1]); }
                                if (p.Equipment.Length > 2) { pCap = GetItemPower(p.Equipment[2]); hName = GetItemName(p.Equipment[2]); }
                                if (p.Equipment.Length > 3) { pArm = GetItemPower(p.Equipment[3]); aName = GetItemName(p.Equipment[3]); }
                                if (p.Equipment.Length > 4) { pShoe = GetItemPower(p.Equipment[4]); sName = GetItemName(p.Equipment[4]); }
                                if (p.Equipment.Length > 6) { pCape = GetItemPower(p.Equipment[6]); cName = GetItemName(p.Equipment[6]); }
                            }

                            if (pWeap > 0 && pOff == 0) pOff = pWeap;

                            string finalWeapon = pWeap > 0 ? $"[{pWeap}] {wName}" : wName;
                            string finalHead = pCap > 0 ? $"[{pCap}] {hName}" : hName;
                            string finalArmor = pArm > 0 ? $"[{pArm}] {aName}" : aName;
                            string finalShoes = pShoe > 0 ? $"[{pShoe}] {sName}" : sName;
                            string finalCape = pCape > 0 ? $"[{pCape}] {cName}" : cName;

                            if (_enableLogging)
                            {
                                string curMap = _gameStateManager.CurrentMapId ?? "0000";
                                RadarLogger.LogPlayer(curMap, p.Name, 0, finalWeapon, finalHead, finalArmor, finalShoes, finalCape);
                            }

                            int avgIP = (pWeap + pOff + pCap + pArm + pShoe + pCape) / 6;

                            Vector4 nameColor;
                            if (avgIP >= 1300) nameColor = new Vector4(1.0f, 0.15f, 0.15f, 1);
                            else if (avgIP >= 1000) nameColor = new Vector4(1.0f, 0.55f, 0.0f, 1);
                            else if (avgIP >= 700) nameColor = new Vector4(1.0f, 0.95f, 0.2f, 1);
                            else if (avgIP > 0) nameColor = new Vector4(0.3f, 1.0f, 0.3f, 1);
                            else nameColor = new Vector4(0.7f, 0.7f, 0.7f, 1);

                            string dirArrow = "  ";
                            Vector4 arrowColor = new Vector4(0.7f, 0.7f, 0.7f, 1);
                            if (_prevPlayerPos.TryGetValue(p.Id, out var prev))
                            {
                                float prevDist = prev.dist;
                                float deltaDist = dist - prevDist;
                                if (MathF.Abs(deltaDist) > 0.5f)
                                {
                                    if (deltaDist < 0) { dirArrow = ">>"; arrowColor = new Vector4(1f, 0.3f, 0.3f, 1); }
                                    else { dirArrow = "<<"; arrowColor = new Vector4(0.4f, 0.9f, 0.4f, 1); }
                                }
                            }
                            _prevPlayerPos[p.Id] = (p.CurrentLerpedX, p.CurrentLerpedY, dist);

                            string hpText = "";
                            float hpRatio = 1f;
                            if (p.MaxHealth > 0)
                            {
                                hpRatio = Math.Clamp(p.CurrentHealth / p.MaxHealth, 0f, 1f);
                                var (displayHp, displayMax) = GetDisplayHealthValues(p.CurrentHealth, p.MaxHealth);
                                hpText = $"[{displayHp}/{displayMax}]";
                            }

                            string tierStr = avgIP > 0 ? $"IP:{avgIP} (T{pWeap},{pCap},{pArm},{pShoe},{pCape})" : $"(T{pWeap},{pCap},{pArm},{pShoe},{pCape})";
                            ImGui.TextColored(arrowColor, dirArrow); ImGui.SameLine();
                            ImGui.TextColored(nameColor, $"{p.Name}"); ImGui.SameLine();

                            if (p.MaxHealth > 0)
                            {
                                Vector4 hpCol = hpRatio > 0.7f ? new Vector4(0.3f, 1f, 0.3f, 1f) :
                                                hpRatio > 0.4f ? new Vector4(1f, 0.8f, 0.2f, 1f) :
                                                                 new Vector4(1f, 0.2f, 0.2f, 1f);
                                ImGui.TextColored(hpCol, hpText); ImGui.SameLine();
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
                                ImGui.Text($"{p.Name}");
                                ImGui.PopStyleColor();
                                if (!string.IsNullOrEmpty(p.Guild))
                                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 1f, 1), $"[{p.Guild}]" + (string.IsNullOrEmpty(p.Alliance) ? "" : $" <{p.Alliance}>"));
                                ImGui.Separator();

                                ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1), (Lang.Get("Player_AvgIP") ?? "Ortalama IP : ") + $"{avgIP}");
                                ImGui.Spacing();
                                (string slot, string name, int ip)[] slots = {
                                ("\u2694 " + (Lang.Get("Equip_Weapon") ?? "Silah "), wName, pWeap),
                                ("\uD83D\uDC9C " + (Lang.Get("Equip_Head") ?? "Migfer "), hName, pCap),
                                ("\uD83D\uDEE1 " + (Lang.Get("Equip_Armor") ?? "Zirh  "), aName, pArm),
                                ("\uD83D\uDC62 " + (Lang.Get("Equip_Shoes") ?? "Ayakkabi "), sName, pShoe),
                                ("\uD83C\uDF10 " + (Lang.Get("Equip_Cape") ?? "Pelerin "), cName, pCape),
                            };

                                foreach (var (slot, name, ip) in slots)
                                {
                                    Vector4 slotCol = ip >= 800 ? new Vector4(1f, 0.3f, 0.3f, 1) :
                                                      ip >= 600 ? new Vector4(1f, 0.6f, 0f, 1) :
                                                      ip >= 400 ? new Vector4(1f, 0.95f, 0.2f, 1) :
                                                      ip > 0 ? new Vector4(0.4f, 1f, 0.4f, 1) :
                                                                   new Vector4(0.5f, 0.5f, 0.5f, 1);
                                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), slot); ImGui.SameLine();
                                    string ipStr = ip > 0 ? $"[{ip}]" : "  -  ";
                                    ImGui.TextColored(slotCol, ipStr); ImGui.SameLine();
                                    ImGui.TextColored(new Vector4(1f, 1f, 1f, 1), name);
                                }
                                ImGui.EndTooltip();
                            }
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), tierStr);
                            ImGui.TextColored(new Vector4(1, 0.6f, 0, 1), $"{wName} | {cName}");
                            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1), $"{hName} | {aName} | {sName}");
                            ImGui.Separator();
                        }
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar(2);
            }

            // 2b. EKİPMAN KARTLARI
            if (_showEquipmentCards && mainPlayer != null)
            {
                int maxSlots = Math.Clamp(_equipmentCardsMaxSlots, 1, _equipCardSlots.Length);
                float memorySeconds = Math.Max(0f, _equipmentCardsMemorySeconds);
                DateTime now = DateTime.Now;
                const float IconSz = 48f;
                const float Pad = 6f;
                const float SlotGap = 10f;

                lock (_dataLock)
                {
                    foreach (var px in _playersBuffer)
                    {
                        if (_whitelist.Contains(px.Name)) continue;
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
                        if (!_whitelist.Contains(px.Name)) aliveIdsSet.Add(px.Id);

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
                        .Where(px => !_whitelist.Contains(px.Name) && !Array.Exists(_equipCardSlots, s => s == px.Id))
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
                                    System.Console.WriteLine($"Error Code : 32 | {ex.Message}");
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
                        if (hasHealthData)
                        {
                            ImGui.SameLine(0, 8);
                            ImGui.TextColored(hpCol, $"HP:{hpCurrent}/{hpMax}");
                        }
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

                        if (!string.IsNullOrEmpty(ep.Guild))
                            ImGui.TextColored(new Vector4(0.6f, 0.6f, 1f, 1), $"[{ep.Guild}]");
                    }

                    ImGui.End();
                    ImGui.PopStyleVar(3);
                }
            }

            // 3. RADAR WIDGET
            if (_detachRadar)
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

            // ========================================================
            // --- ?? DUNGEON TOAST (GARANTİ ÇÖZÜM) ---
            // ========================================================
            if (!string.IsNullOrEmpty(DungeonChestMessage))
            {
                string msg = DungeonChestMessage;

                uint color = 0xFF00FF00;
                if (msg.Contains("MAVI")) color = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.2f, 0.6f, 1.0f, 1.0f));
                else if (msg.Contains("MOR")) color = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.8f, 0.3f, 1.0f, 1.0f));
                else if (msg.Contains("LEGENDARY")) color = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 0.8f, 0.1f, 1.0f));

                _toasts.Add(("\uD83C\uDF81 " + msg, DateTime.Now, color));

                DungeonChestMessage = "";
            }

            // 4. MODERN SETTINGS UI
            if (!_hideSettingsWindow)
            {
                ImGui.SetNextWindowSize(new Vector2(850, 550), ImGuiCond.FirstUseEver);

                if (ImGui.Begin("Nightwatch Radar ##ModernUI", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar))
                {
                    float sidebarWidth = 180f;

                    // ARKA PLAN RENGİNİ BOZAN SABİT KOD BURADAN KALDIRILDI!
                    // Artık kusursuz bir şekilde 23,26,33 temanla uyumlu olacak.
                    ImGui.BeginChild("Sidebar", new Vector2(sidebarWidth, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);
                    {
                        ImGui.Spacing();

                        string logoPath;
                        if (_resourceCache.TryGetValue("logo", out string? cachedLogo) && !string.IsNullOrEmpty(cachedLogo)) { logoPath = cachedLogo; }
                        else { using (var bmp = Nightwatch.Properties.Resources.Nightwatch) { logoPath = GetResourceToTemp(bmp, "logo"); } }

                        if (File.Exists(logoPath))
                        {
                            AddOrGetImagePointer(logoPath, true, out IntPtr logoTex, out uint lw, out uint lh);
                            if (logoTex != IntPtr.Zero)
                            {
                                float imgSize = 140f;
                                ImGui.SetCursorPosX((sidebarWidth - imgSize) / 2f);
                                ImGui.Image(logoTex, new Vector2(imgSize, imgSize));
                            }
                        }

                        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                      for (int i = 0; i < 7; i++)
                        {
                            if (i == 5) continue; // Settings'i aşağıda çizdirdiğin için atlıyoruz

                            bool isActive = (_activeTab == i);

                            float btnHeight = 42f;
                            float btnWidth = sidebarWidth - 30f;

                            ImGui.SetCursorPosX(15f);
                            Vector2 startPos = ImGui.GetCursorScreenPos();

                            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));
                            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0, 0, 0, 0));
                            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0, 0, 0, 0));

                            if (ImGui.Selectable($"##tab{i}", isActive, ImGuiSelectableFlags.None, new Vector2(btnWidth, btnHeight)))
                                _activeTab = i;

                            bool isHovered = ImGui.IsItemHovered();
                            ImGui.PopStyleColor(3);

                            var dl = ImGui.GetWindowDrawList();
                            if (isActive || isHovered)
                            {
                                Vector4 col = isActive
                                    ? ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]
                                    : ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];

                                dl.AddRectFilled(startPos, startPos + new Vector2(btnWidth, btnHeight), ImGui.ColorConvertFloat4ToU32(col), btnHeight / 2f);
                            }

                            string tabName = "tab_" + i;
                            string iconPath = "";

                            if (_resourceCache.TryGetValue(tabName, out string? cachedTab) && !string.IsNullOrEmpty(cachedTab)) { iconPath = cachedTab; }
                            else
                            {
                                using (System.Drawing.Bitmap? currentIcon = i switch
                                {
                                    0 => Nightwatch.Properties.Resources.ResourcesPNG,
                                    1 => Nightwatch.Properties.Resources.MobMistPNG,
                                    2 => Nightwatch.Properties.Resources.PlayersPNG,
                                    3 => Nightwatch.Properties.Resources.ConfigPNG,
                                    4 => Nightwatch.Properties.Resources.DevToolsPNG,
                                    6 => Nightwatch.Properties.Resources.SettingsPNG, // Device için Settings İkonu kullanıyoruz
                                    _ => null
                                })
                                {
                                    if (currentIcon != null) iconPath = GetResourceToTemp(currentIcon, tabName);
                                }
                            }

                            if (IsImageExistsCached(iconPath))
                            {
                                AddOrGetImagePointer(iconPath, true, out IntPtr tex, out uint iw, out uint ih);
                                if (tex != IntPtr.Zero)
                                {
                                    float iconSize = 40f;
                                    float offY = (btnHeight - iconSize) / 2f;
                                    uint tint = isActive ? 0xFFFFFFFF : (isHovered ? 0xEEFFFFFF : 0xAAFFFFFF);

                                    dl.AddImage(tex, startPos + new Vector2(15, offY), startPos + new Vector2(15 + iconSize, offY + iconSize), Vector2.Zero, Vector2.One, tint);

                                    ImGui.SetCursorScreenPos(startPos + new Vector2(60, (btnHeight - ImGui.GetTextLineHeight()) / 2f));
                                    ImGui.Text(_tabs[i]);
                                }

                            }
                            ImGui.Dummy(new Vector2(0, 10f));
                        }

                        // SETTINGS İKONU (Ayarlar)
                        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 55f);
                        ImGui.Separator();
                        ImGui.Spacing();

                        bool isSetActive = (_activeTab == 5);
                        float setIconSize = 35f;
                        float hitBoxSize = 25f;

                        string settingsLabel = Lang.Get("Sidebar_Settings");
                        float settingsTextW = ImGui.CalcTextSize(settingsLabel).X;
                        float totalHitW = hitBoxSize + 6f + settingsTextW + 4f;

                        ImGui.SetCursorPosX((sidebarWidth - hitBoxSize) / 9f);
                        Vector2 setPos = ImGui.GetCursorScreenPos();

                        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));
                        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0, 0, 0, 0));
                        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0, 0, 0, 0));

                        if (ImGui.Selectable("##SettingsIcon", isSetActive, ImGuiSelectableFlags.None, new Vector2(totalHitW, hitBoxSize)))
                            _activeTab = 5;

                        bool isSetHovered = ImGui.IsItemHovered();
                        ImGui.PopStyleColor(3);

                        var dl2 = ImGui.GetWindowDrawList();
                        if (isSetActive || isSetHovered)
                        {
                            // TEMA RENGİ
                            Vector4 setCol = isSetActive
                                ? ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]
                                : ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];

                            dl2.AddRectFilled(setPos, setPos + new Vector2(hitBoxSize, hitBoxSize), ImGui.ColorConvertFloat4ToU32(setCol), hitBoxSize / 2f);
                        }

                        string setPath;
                        if (_resourceCache.TryGetValue("settings", out string? cachedSet) && !string.IsNullOrEmpty(cachedSet)) { setPath = cachedSet; }
                        else { using (var bmp = Nightwatch.Properties.Resources.SettingsPNG) { setPath = GetResourceToTemp(bmp, "settings"); } }

                        if (IsImageExistsCached(setPath))
                        {
                            AddOrGetImagePointer(setPath, true, out IntPtr setTex, out uint sw, out uint sh);
                            if (setTex != IntPtr.Zero)
                            {
                                float offset = (hitBoxSize - setIconSize) / 2f;
                                uint tint = isSetActive ? 0xFFFFFFFF : (isSetHovered ? 0xEEFFFFFF : 0xAAFFFFFF);

                                dl2.AddImage(setTex, setPos + new Vector2(offset, offset), setPos + new Vector2(offset + setIconSize, offset + setIconSize), Vector2.Zero, Vector2.One, tint);

                                Vector2 settingsTextPos = setPos + new Vector2(hitBoxSize + 6f, (hitBoxSize - ImGui.GetTextLineHeight()) / 2f);
                                uint textCol = isSetActive ? 0xFFFFFFFF : (isSetHovered ? 0xDDFFFFFF : 0x88FFFFFF);
                                dl2.AddText(settingsTextPos + new Vector2(1, 1), 0xFF000000, settingsLabel);
                                dl2.AddText(settingsTextPos, textCol, settingsLabel);
                            }
                        }
                    }
                    ImGui.EndChild();

                    ImGui.SameLine();

                    ImGui.BeginGroup();
                    {
                        ImGui.BeginChild("Header", new Vector2(0, 50), ImGuiChildFlags.None, ImGuiWindowFlags.None);
                        {
                            ImGui.SetCursorPos(new Vector2(10, 18));
                            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "UI: "); ImGui.SameLine();
                            ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), _tabs[_activeTab].ToUpperInvariant());

                            float headerWidth = ImGui.GetWindowWidth();
                            ImGui.SetCursorPos(new Vector2(headerWidth - 75f, 10f));

                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));

                            if (ImGui.Button("-##MinBtn", new Vector2(30, 30)))
                            {
                                string balloonMsg = Lang.Get("App_System_Tray");
                                _hideSettingsWindow = true;
                                if (_trayIcon != null)
                                    _trayIcon.ShowBalloonTip(2000, "Nightwatch", balloonMsg, System.Windows.Forms.ToolTipIcon.Info);
                            }
                            ImGui.SameLine(0, 0);

                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 0.3f, 1f));
                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.2f, 0.2f, 0.3f));

                            if (ImGui.Button("X##ClsBtn", new Vector2(30, 30)))
                            {
                                if (_trayIcon != null) _trayIcon.Dispose();
                                Environment.Exit(0);
                            }
                            ImGui.PopStyleColor(2);
                            ImGui.PopStyleColor(2);
                        }
                        ImGui.EndChild(); ImGui.Separator();

                        // ALT KISMI GİTTİ HATASI BURADAN ÇÖZÜLDÜ!
                        // (0, -40) yazıyordu, 40 pixel kırpıp atıyordu. (0, 0) yaparak tam doldurduk.
                        ImGui.BeginChild("TabContent", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None);
                        {
                            RenderActiveTab();
                        }
                        ImGui.EndChild();
                    }
                    ImGui.EndGroup();
                }
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

