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
using AlbionDataHandlers.Utils;
using AlbionDataHandlers.Mappers;
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

        private static Dictionary<int, string> ParsePayloadToMap(string payload)
        {
            var map = new Dictionary<int, string>();
            if (string.IsNullOrWhiteSpace(payload)) return map;

            var matches = Regex.Matches(payload, @"\[(\d+)\]=");
            for (int i = 0; i < matches.Count; i++)
            {
                var current = matches[i];
                if (!int.TryParse(current.Groups[1].Value, out int key)) continue;

                int valueStart = current.Index + current.Length;
                int valueEnd = (i + 1 < matches.Count) ? matches[i + 1].Index : payload.Length;
                if (valueEnd < valueStart) continue;

                string value = payload.Substring(valueStart, valueEnd - valueStart);
                value = value.Trim().TrimEnd('|').Trim();
                map[key] = value;
            }

            return map;
        }

        private static byte[] ParseByteArrayFromValueString(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<byte>();
            var m = Regex.Match(value, @"byte\[\d+\]\((.*)\)");
            if (!m.Success) return Array.Empty<byte>();

            string inside = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(inside)) return Array.Empty<byte>();

            var tokens = inside.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(x => x.Trim())
                               .Where(x => x != "...")
                               .ToList();

            var list = new List<byte>(tokens.Count);
            foreach (var t in tokens)
            {
                if (byte.TryParse(t, out byte b)) list.Add(b);
            }
            return list.ToArray();
        }

        private static int ReadInt32LE(byte[] src, int offset)
        {
            if (offset < 0 || offset + 4 > src.Length) return 0;
            return src[offset]
                | (src[offset + 1] << 8)
                | (src[offset + 2] << 16)
                | (src[offset + 3] << 24);
        }

        private static bool TryParseFloatInvariant(string? s, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool IsValidDecodePos(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
            if (Math.Abs(x) >= 4000f || Math.Abs(y) >= 4000f) return false;
            return Math.Abs(x) > 0.1f || Math.Abs(y) > 0.1f;
        }

        private static float DistSq(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        private static void AddDecodeCandidate(List<(string mode, float x, float y)> list, string mode, float x, float y)
        {
            if (IsValidDecodePos(x, y)) list.Add((mode, x, y));
        }

        private static List<(string mode, float x, float y)> DecodeCandidatesFromPayload(
            string payload,
            bool d01, bool d02, bool d03, bool d04, bool d05,
            bool d06, bool d07, bool d08, bool d09, bool d10,
            bool d11, bool d12, bool d13, bool d14, bool d15)
        {
            var result = new List<(string mode, float x, float y)>();
            var map = ParsePayloadToMap(payload);

            // Param yolları
            if (d13 && map.TryGetValue(4, out var p4) && map.TryGetValue(5, out var p5)
                && TryParseFloatInvariant(p4, out var x45) && TryParseFloatInvariant(p5, out var y45))
            {
                AddDecodeCandidate(result, "13:[4,5]", x45, y45);
            }

            if (d14 && map.TryGetValue(19, out var p19) && map.TryGetValue(25, out var p25)
                && TryParseFloatInvariant(p19, out var x1925) && TryParseFloatInvariant(p25, out var y1925))
            {
                AddDecodeCandidate(result, "14:[19,25]", x1925, y1925);
            }

            // Byte/List payload yolu
            if (!map.TryGetValue(1, out var p1Raw)) return result;

            byte[] bytes = ParseByteArrayFromValueString(p1Raw);
            if (bytes.Length < 13) return result;

            if (d15)
            {
                // p1 list [0,1] genelde byte list olabilir; parse stringinden ilk iki byte'ı dene
                float lx = bytes[0];
                float ly = bytes[1];
                AddDecodeCandidate(result, "15:list[0,1]", lx, ly);
            }

            if (bytes.Length >= 13)
            {
                if (d01) AddDecodeCandidate(result, "01:i[1,9]/1e7", ReadInt32LE(bytes, 1) / 10_000_000f, ReadInt32LE(bytes, 9) / 10_000_000f);
                if (d02) AddDecodeCandidate(result, "02:i[1,9]/1e6", ReadInt32LE(bytes, 1) / 1_000_000f, ReadInt32LE(bytes, 9) / 1_000_000f);
                if (d03) AddDecodeCandidate(result, "03:i[1,9]/1e5", ReadInt32LE(bytes, 1) / 100_000f, ReadInt32LE(bytes, 9) / 100_000f);
                if (d04) AddDecodeCandidate(result, "04:i[1,9]/100", ReadInt32LE(bytes, 1) / 100f, ReadInt32LE(bytes, 9) / 100f);
                if (d05 && bytes.Length >= 13) AddDecodeCandidate(result, "05:f[1,9]", BitConverter.ToSingle(bytes, 1), BitConverter.ToSingle(bytes, 9));
            }

            if (bytes.Length >= 17)
            {
                if (d06) AddDecodeCandidate(result, "06:i[9,13]/1e7", ReadInt32LE(bytes, 9) / 10_000_000f, ReadInt32LE(bytes, 13) / 10_000_000f);
                if (d07) AddDecodeCandidate(result, "07:i[9,13]/1e6", ReadInt32LE(bytes, 9) / 1_000_000f, ReadInt32LE(bytes, 13) / 1_000_000f);
                if (d08) AddDecodeCandidate(result, "08:i[9,13]/1e5", ReadInt32LE(bytes, 9) / 100_000f, ReadInt32LE(bytes, 13) / 100_000f);
                if (d09) AddDecodeCandidate(result, "09:i[9,13]/100", ReadInt32LE(bytes, 9) / 100f, ReadInt32LE(bytes, 13) / 100f);
                if (d10) AddDecodeCandidate(result, "10:f[9,13]", BitConverter.ToSingle(bytes, 9), BitConverter.ToSingle(bytes, 13));
                if (d11) AddDecodeCandidate(result, "11:x=i/100 y=f", ReadInt32LE(bytes, 9) / 100f, BitConverter.ToSingle(bytes, 13));
                if (d12) AddDecodeCandidate(result, "12:x=f y=i/100", BitConverter.ToSingle(bytes, 9), ReadInt32LE(bytes, 13) / 100f);
            }

            return result;
        }

        private List<(string mode, float x, float y)> PointerScanCandidatesFromPayload(string payload, int maxOffset)
        {
            var result = new List<(string mode, float x, float y)>();
            var map = ParsePayloadToMap(payload);

            if (map.TryGetValue(4, out var p4) && map.TryGetValue(5, out var p5)
                && TryParseFloatInvariant(p4, out var x45) && TryParseFloatInvariant(p5, out var y45))
            {
                AddDecodeCandidate(result, "P:[4,5]", x45, y45);
            }

            if (map.TryGetValue(19, out var p19) && map.TryGetValue(25, out var p25)
                && TryParseFloatInvariant(p19, out var x1925) && TryParseFloatInvariant(p25, out var y1925))
            {
                AddDecodeCandidate(result, "P:[19,25]", x1925, y1925);
            }

            if (!map.TryGetValue(1, out var p1Raw)) return result;
            byte[] bytes = ParseByteArrayFromValueString(p1Raw);
            if (bytes.Length < 8) return result;

            int safeMax = Math.Max(4, Math.Min(maxOffset, bytes.Length - 4));

            for (int ox = 0; ox <= safeMax; ox++)
            {
                for (int oy = 0; oy <= safeMax; oy++)
                {
                    if (ox + 4 > bytes.Length || oy + 4 > bytes.Length) continue;

                    int ix = ReadInt32LE(bytes, ox);
                    int iy = ReadInt32LE(bytes, oy);

                    AddDecodeCandidate(result, $"I/1e7 [{ox},{oy}]", ix / 10_000_000f, iy / 10_000_000f);
                    AddDecodeCandidate(result, $"I/1e6 [{ox},{oy}]", ix / 1_000_000f, iy / 1_000_000f);
                    AddDecodeCandidate(result, $"I/1e5 [{ox},{oy}]", ix / 100_000f, iy / 100_000f);
                    AddDecodeCandidate(result, $"I/100 [{ox},{oy}]", ix / 100f, iy / 100f);

                    if (ox + 4 <= bytes.Length && oy + 4 <= bytes.Length)
                    {
                        try
                        {
                            AddDecodeCandidate(result, $"F [{ox},{oy}]", BitConverter.ToSingle(bytes, ox), BitConverter.ToSingle(bytes, oy));
                            AddDecodeCandidate(result, $"XF/YI [{ox},{oy}]", BitConverter.ToSingle(bytes, ox), iy / 100f);
                            AddDecodeCandidate(result, $"XI/YF [{ox},{oy}]", ix / 100f, BitConverter.ToSingle(bytes, oy));
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return result;
        }

        #region UI nin ÅŸekilleri vs.
        private void ApplyModernStyle()
        {
            var style = ImGui.GetStyle();

            // Köşe Yumuşatmaları
            style.WindowRounding = 12f;
            style.ChildRounding = 10f;
            style.FrameRounding = 8f;
            style.PopupRounding = 10f;
            style.ScrollbarRounding = 12f;
            style.GrabRounding = 8f;

            style.WindowBorderSize = 1f;
            style.ChildBorderSize = 1f;
            style.FrameBorderSize = 0f;

            // ==========================================
            // 2. ARKA PLAN VE BAŞLIK EŞİTLEMESİ (Kusursuz Görünüm)
            // ==========================================
            // Senin verdiğin RGB: 1, 2, 3 (İçi ve başlığı aynı renk yapıyoruz ki çizgi olmasın)
            Vector4 mainBgColor = new Vector4(1f / 255f, 2f / 255f, 3f / 255f, 0.98f); // 0.98f hafif saydamlık

            style.Colors[(int)ImGuiCol.WindowBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.ChildBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.PopupBg] = mainBgColor;

            // BAŞLIK ÇUBUĞUNU (TASI BENI YAZAN YERİ) GİZLEYEN SİHİRLİ KISIM:
            style.Colors[(int)ImGuiCol.TitleBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.TitleBgActive] = mainBgColor;
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = mainBgColor;

            // Çerçeve (Border) rengini ana renkten çok hafif daha açık yapıyoruz ki tatlı bir sınırı olsun
            style.Colors[(int)ImGuiCol.Border] = new Vector4(35f / 255f, 38f / 255f, 45f / 255f, 1.0f);

            // Frame'ler (Kutucuklar, ComboBox'lar, alt planlar)
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(30f / 255f, 33f / 255f, 40f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(40f / 255f, 44f / 255f, 52f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(45f / 255f, 49f / 255f, 58f / 255f, 1.0f);

            // ==========================================
            // 3. ANA VURGU RENGİ (Accent Color)
            // ==========================================
            // Midnight mor vurgu rengi
            Vector4 accentColor = new Vector4(92f / 255f, 40f / 255f, 120f / 255f, 1.0f);
            Vector4 accentHover = new Vector4(118f / 255f, 62f / 255f, 150f / 255f, 1.0f);
            Vector4 accentActive = new Vector4(72f / 255f, 30f / 255f, 98f / 255f, 1.0f);
            Vector4 accentMuted = new Vector4(accentColor.X, accentColor.Y, accentColor.Z, 0.35f);

            // Butonlar, Sekmeler ve Sliderlar
            style.Colors[(int)ImGuiCol.Button] = accentColor;
            style.Colors[(int)ImGuiCol.ButtonHovered] = accentHover;
            style.Colors[(int)ImGuiCol.ButtonActive] = accentActive;

            style.Colors[(int)ImGuiCol.Header] = accentMuted;
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(accentColor.X, accentColor.Y, accentColor.Z, 0.60f);
            style.Colors[(int)ImGuiCol.HeaderActive] = accentColor;

            // Sekme (Tab) renkleri - Midnight mor (seçili daha açık)
            Vector4 tabMidnight = new Vector4(140f / 255f, 85f / 255f, 175f / 255f, 1.0f);
            Vector4 tabMidnightHover = new Vector4(165f / 255f, 110f / 255f, 195f / 255f, 1.0f);
            Vector4 tabMidnightActive = new Vector4(185f / 255f, 130f / 255f, 210f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.Tab] = tabMidnight;
            style.Colors[(int)ImGuiCol.TabHovered] = tabMidnightHover;

            // ImGuiCol enumunda TabActive yoksa index ile güvenli şekilde ayarla
            int tabActiveIndex = (int)ImGuiCol.TabHovered + 1;
            if (style.Colors.Count > tabActiveIndex)
            {
                style.Colors[tabActiveIndex] = tabMidnightActive;

                int tabUnfocusedIndex = tabActiveIndex + 1;
                if (style.Colors.Count > tabUnfocusedIndex)
                {
                    style.Colors[tabUnfocusedIndex] = tabMidnight;
                }

                int tabUnfocusedActiveIndex = tabUnfocusedIndex + 1;
                if (style.Colors.Count > tabUnfocusedActiveIndex)
                {
                    style.Colors[tabUnfocusedActiveIndex] = tabMidnight;
                }
            }

            style.Colors[(int)ImGuiCol.CheckMark] = accentColor;
            style.Colors[(int)ImGuiCol.SliderGrab] = accentColor;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = accentActive;

            style.Colors[(int)ImGuiCol.Separator] = style.Colors[(int)ImGuiCol.Border];
            style.Colors[(int)ImGuiCol.SeparatorHovered] = accentHover;
            style.Colors[(int)ImGuiCol.SeparatorActive] = accentActive;

            // ==========================================
            // 4. METİN RENKLERİ
            // ==========================================
            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.92f, 0.92f, 0.95f, 1.00f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.55f, 1.00f);
        }
        #endregion

        #region Tabs
        private void RenderActiveTab()
        {
            switch (_activeTab)
            {

                #region Resources
                case 0: // Kaynaklar
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Res_Title") ?? "Resources");
                    ImGui.Checkbox(Lang.Get("Res_ShowIcons") ?? "Show Icons", ref _showResourceIcons);
                    ImGui.Checkbox(Lang.Get("Res_ShowOnMap") ?? "Show on Map", ref _showResources);
                    ImGui.Checkbox(Lang.Get("Res_TrackerOnly") ?? "Tracker only (hide radar dots)", ref _resourceTrackerOnlyMode);
                    ImGui.Checkbox(Lang.Get("Res_Label") ?? "Show Resource Labels", ref _showResourceLabels);
                    ImGui.SliderFloat(Lang.Get("Res_IconSize") ?? "Icon Size", ref _globalIconSize, 10, 80);

                    ImGui.Separator();
                    var cats = Enum.GetValues(typeof(HarvestableCategory)).Cast<HarvestableCategory>();
                    foreach (var cat in cats)
                    {
                        if (cat == HarvestableCategory.None) continue;
                        if (!_resourceMasterToggles.ContainsKey(cat)) _resourceMasterToggles[cat] = true;
                        if (!_resourceFilters.ContainsKey(cat)) { var m = new bool[8, 4]; for (int i = 0; i < 8; i++) for (int j = 0; j < 4; j++) m[i, j] = true; _resourceFilters[cat] = m; }
                        bool on = _resourceMasterToggles[cat];
                        string displayCatName = Lang.Get(cat.ToString()) != cat.ToString() ? Lang.Get(cat.ToString()) : cat.ToString();
                        if (ImGui.Checkbox(displayCatName, ref on)) _resourceMasterToggles[cat] = on;
                        if (on)
                        {
                            ImGui.Indent();
                            if (ImGui.TreeNode(string.Format(Lang.Get("Res_Filter") ?? "{0} Filter", cat)))
                            {
                                if (ImGui.BeginTable($"TÄ°ERR_{cat}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                                {
                                    ImGui.TableSetupColumn(Lang.Get("Res_Enchantment") ?? "Enchant"); ImGui.TableSetupColumn("0"); ImGui.TableSetupColumn("1"); ImGui.TableSetupColumn("2"); ImGui.TableSetupColumn("3"); ImGui.TableHeadersRow();
                                    for (int t = 0; t < 8; t++)
                                    {
                                        ImGui.TableNextRow(); ImGui.TableSetColumnIndex(0); ImGui.TextColored(new Vector4(1, 0.9f, 0, 1), $"T{t + 1}");
                                        for (int e = 0; e < 4; e++)
                                        {
                                            ImGui.TableSetColumnIndex(e + 1); if (t < 3 && e > 0) continue;
                                            ImGui.PushID($"{cat}{t}{e}"); ImGui.Checkbox("", ref _resourceFilters[cat][t, e]); ImGui.PopID();
                                        }
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.TreePop();
                            }
                            ImGui.Unindent();
                        }
                    }
                    break;
                #endregion

                #region Mobs and Mists
                case 1: // Mob/Mist
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Mob_Title") ?? "Mobs");
                    ImGui.Checkbox(Lang.Get("Mob_ShowNormal") ?? "Show Normal", ref _showNormalMobs);
                    ImGui.Checkbox(Lang.Get("Mob_ShowBoss") ?? "Show Bosses", ref _showBosses);
                    ImGui.Checkbox(Lang.Get("Mob_ShowMist") ?? "Show Mists", ref _showMists);
                    ImGui.Checkbox(Lang.Get("Mob_ShowNames") ?? "Show Names", ref _showMobNames);
                    string[] truthModes = { "Name First", "Network First", "Metadata First" };
                    ImGui.SetNextItemWidth(220);
                    ImGui.Combo("Resource Truth Mode", ref _resourceTruthMode, truthModes, truthModes.Length);

                    ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), Lang.Get("Mob_BlacklistTitle") ?? "Blacklist");
                    ImGui.Text(Lang.Get("Mob_BlacklistSearch") ?? "Search:");
                    ImGui.InputText(Lang.Get("Mob_BlacklistInput") ?? "##Search", ref _blacklistSearchQuery, 64);

                    ImGui.BeginChild("BlResults", new Vector2(0, 100), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
                    {
                        if (!string.IsNullOrEmpty(_blacklistSearchQuery) && _mobDatabase.Count > 0)
                        {
                            string rawQuery = _blacklistSearchQuery.Trim();
                            string normalizedQuery = NormalizeSearchText(rawQuery);
                            var blMatches = _mobDatabase.Where(x =>
                                NameMatchesSearch(x.Value.Name, normalizedQuery) ||
                                x.Key.ToString().Contains(rawQuery)
                            )
                            .OrderByDescending(x => NormalizeSearchText(x.Value.Name) == normalizedQuery || x.Key.ToString() == rawQuery)
                            .ThenBy(x => x.Value.Name)
                            .Take(50);

                            foreach (var m in blMatches)
                            {
                                if (ImGui.Selectable($"[{m.Key}] {m.Value.Name}", _selectedMobIdForBlacklist == m.Key))
                                    _selectedMobIdForBlacklist = m.Key;
                            }
                        }
                    }
                    ImGui.EndChild();
                    if (ImGui.Button(Lang.Get("Mob_BlacklistAdd") ?? "Add") && _selectedMobIdForBlacklist != -1)
                    {
                        _ignoredMobIds.Add(_selectedMobIdForBlacklist);
                        _selectedMobIdForBlacklist = -1;
                    }

                    ImGui.Separator();
                    ImGui.Text(Lang.Get("Mob_BlacklistHeader") ?? "Ignored Mobs");
                    if (ImGui.BeginChild("HiddenList", new Vector2(0, 100), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
                    {
                        int idToRemove = -1;
                        foreach (var id in _ignoredMobIds)
                        {
                            string mName = _mobDatabase.ContainsKey(id) ? _mobDatabase[id].Name : "ID:" + id;
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), mName); ImGui.SameLine();
                            if (ImGui.SmallButton($"{Lang.Get("Mob_BlacklistRemove") ?? "Remove"}##{id}")) idToRemove = id;
                        }
                        if (idToRemove != -1) _ignoredMobIds.Remove(idToRemove);
                    }
                    ImGui.EndChild();
                    break;
                #endregion

                #region Players
                case 2: // Oyuncular
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Player_Title") ?? "Players");
                    ImGui.Checkbox(Lang.Get("Player_ShowOthers") ?? "Show Players", ref _showPlayers);
                    ImGui.Checkbox(Lang.Get("Player_ShowNames") ?? "Show Names", ref _showPlayerName);
                    ImGui.Checkbox(Lang.Get("Player_ShowGuild") ?? "Show Guild", ref _showGuild);
                    ImGui.Checkbox(Lang.Get("Player_ShowCount") ?? "Show Count", ref _showPlayerCount);
                    ImGui.SliderFloat("Enemy Count Hold (s)", ref _enemyCountHoldSeconds, 0.1f, 3.0f, "%.1f");
                    ImGui.Checkbox(Lang.Get("Player_ShowList") ?? "Show List", ref _showPlayerList);
                    if (_showPlayerList) ImGui.Checkbox(Lang.Get("Player_MoveList") ?? "Move List", ref _playerListMoveable);
                    ImGui.Checkbox(Lang.Get("Settings_EquipCards") ?? "Ekipman Kartlari", ref _showEquipmentCards);
                    if (_showEquipmentCards)
                    {
                        ImGui.Indent();
                        ImGui.Checkbox(Lang.Get("Player_EquipCardsMove") ?? "Kartlari Tasiyabil", ref _equipmentCardsMoveable);
                        ImGui.SliderInt(Lang.Get("Player_EquipCardsLimit") ?? "Kart Limiti", ref _equipmentCardsMaxSlots, 1, _equipCardSlots.Length);
                        ImGui.SliderFloat(Lang.Get("Player_EquipCardsMemory") ?? "Kart Hafizasi (sn)", ref _equipmentCardsMemorySeconds, 0f, 30f, "%.0f");
                        ImGui.Unindent();
                    }

                    ImGui.Checkbox(Lang.Get("Player_ImportSameGuild") ?? "Ayni Guild'i Whitelist'e Ekle", ref _whitelistImportSameGuild);
                    ImGui.Checkbox(Lang.Get("Player_ImportSameAlliance") ?? "Ayni Alliance'i Whitelist'e Ekle", ref _whitelistImportSameAlliance);

                    ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                    ImGui.TextColored(new Vector4(0, 1, 0, 1), Lang.Get("Player_Whitelist") ?? "Whitelist");
                    ImGui.InputText(Lang.Get("Player_WhitelistAdd") ?? "##Add", ref _whitelistInput, 32);
                    ImGui.SameLine();
                    if (ImGui.Button(Lang.Get("Player_WhitelistBtn") ?? "Add"))
                    {
                        if (!string.IsNullOrEmpty(_whitelistInput))
                        {
                            _whitelist.Add(_whitelistInput);
                            ImportWhitelistByGuildAlliance(_whitelistInput);
                            SaveWhitelist();
                            _whitelistInput = "";
                        }
                    }

                    if (ImGui.BeginChild("WlScroll", new Vector2(0, 150), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
                    {
                        string nameToRemove = null;
                        foreach (var name in _whitelist)
                        {
                            ImGui.BulletText(name); ImGui.SameLine(ImGui.GetWindowWidth() - 50);
                            if (ImGui.SmallButton($"{Lang.Get("Player_WhitelistRemove") ?? "Remove"}##{name}")) nameToRemove = name;
                        }
                        if (nameToRemove != null) { _whitelist.Remove(nameToRemove); SaveWhitelist(); }
                    }
                    ImGui.EndChild();
                    break;
                #endregion

                #region Config
                case 3: // Config
                    ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Config_Title") ?? "Config");
                    ImGui.InputText(Lang.Get("Config_NameInput") ?? "##Name", ref _configFileNameInput, 32);
                    if (ImGui.Button(Lang.Get("Config_SaveBtn") ?? "Save"))
                    {
                        // Kutu boÅŸ deÄŸilse veya sadece boÅŸluklardan oluÅŸmuyorsa kaydet
                        if (!string.IsNullOrWhiteSpace(_configFileNameInput))
                        {
                            SaveConfig(_configFileNameInput);
                        }
                    }
                    ImGui.Separator();
                    if (ImGui.BeginChild("CfgList", new Vector2(0, 150), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
                    {
                        for (int i = 0; i < _availableConfigs.Length; i++)
                        {
                            if (ImGui.Selectable(_availableConfigs[i], _selectedConfigIndex == i)) { _selectedConfigIndex = i; _configFileNameInput = _availableConfigs[i]; }
                        }
                    }
                    ImGui.EndChild();
                    if (ImGui.Button(Lang.Get("Config_LoadBtn") ?? "Load") && _selectedConfigIndex >= 0) LoadConfig(_availableConfigs[_selectedConfigIndex]);
                    ImGui.SameLine();
                    if (ImGui.Button(Lang.Get("Config_RefreshBtn") ?? "Refresh")) RefreshConfigList();
                    break;
                #endregion

                #region Settings
                case 5: // Ayarlar
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Settings_CalibTitle") ?? "Radar Calibration");

                    ImGui.SliderFloat(Lang.Get("Settings_Zoom") ?? "Zoom", ref _zoom, 0.5f, 10.0f);
                    ImGui.SliderFloat(Lang.Get("Settings_RadarSize") ?? "Size", ref _radarSize, 200, 2500);
                    ImGui.SliderFloat(Lang.Get("Settings_Render_Distance"), ref _renderDistance, 10.0f, 2500.0f, "%.0f");

                    /*  ImGui.Separator();
                      ImGui.Text(Lang.Get("Settings_ManageTitle") ?? "Manage Radar");
                      ImGui.Checkbox(Lang.Get("Settings_InvertX") ?? "Invert X", ref _invertX);
                      ImGui.Checkbox(Lang.Get("Settings_InvertY") ?? "Invert Y", ref _invertY);
                      ImGui.Checkbox(Lang.Get("Settings_SwapXY") ?? "Swap X/Y", ref _swapXY);*/

                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Settings_MapBgTitle") ?? "Map Background");
                    ImGui.Checkbox(Lang.Get("Settings_ShowMapBg") ?? "Show Map Background", ref _showMapBackground);
                    if (_showMapBackground)
                    {
                        ImGui.Indent();
                        ImGui.SliderFloat(Lang.Get("Settings_MapOpacity") ?? "Map Opacity", ref _mapOpacity, 0.1f, 1.0f, "%.2f");
                        ImGui.Unindent();
                    }

                    ImGui.Separator();
                    ImGui.Text(Lang.Get("Settings_WindowTitle") ?? "Window Settings");
                    ImGui.Checkbox(Lang.Get("Settings_DetachRadar") ?? "Detach", ref _detachRadar);
                    if (_detachRadar) ImGui.Checkbox(Lang.Get("Settings_MoveRadar") ?? "Move", ref _radarMoveable);
                    ImGui.Checkbox(Lang.Get("Settings_ShowWatermark") ?? "Watermark", ref _showWatermark);

                    if (_showWatermark)
                    {
                        ImGui.Checkbox(Lang.Get("Settings_MoveWatermark") ?? "Move WM", ref _watermarkMoveable);
                       /* ImGui.Text(Lang.Get("Settings_Position") ?? "Konum:"); ImGui.SameLine();
                        if (_cachedPrimaryScreenW == 0) _cachedPrimaryScreenW = GetSystemMetrics(SM_CXSCREEN);
                        if (_cachedPrimaryScreenH == 0) _cachedPrimaryScreenH = GetSystemMetrics(SM_CYSCREEN);
                        if (ImGui.SmallButton((Lang.Get("Settings_TopLeft") ?? "Sol Ust") + "##wm")) { _watermarkX = 10; _watermarkY = 10; }
                        ImGui.SameLine();
                        if (ImGui.SmallButton((Lang.Get("Settings_TopRight") ?? "Sag Ust") + "##wm")) { _watermarkX = _cachedPrimaryScreenW - 290; _watermarkY = 10; }
                        ImGui.SameLine();
                        if (ImGui.SmallButton((Lang.Get("Settings_BottomLeft") ?? "Sol Alt") + "##wm")) { _watermarkX = 10; _watermarkY = _cachedPrimaryScreenH - 45; }
                        ImGui.SameLine();
                        if (ImGui.SmallButton((Lang.Get("Settings_BottomRight") ?? "Sag Alt") + "##wm")) { _watermarkX = _cachedPrimaryScreenW - 290; _watermarkY = _cachedPrimaryScreenH - 45; }*/
                    }
                    ImGui.Checkbox(Lang.Get("Settings_DangerAlarm") ?? "Yaklasma Alarmi", ref _showDangerCompass);

                    /*  ImGui.Separator();
                      ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Settings_LogTitle") ?? "Logs");
                      ImGui.Checkbox(Lang.Get("Settings_EnableLog") ?? "Enable Logging", ref _enableLogging);*/



                    /*  ImGui.Separator();
                      ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), Lang.Get("Settings_TrackerLaserTitle") ?? "Tracker Lazeri");
                      ImGui.Checkbox(Lang.Get("Settings_TrackerResource") ?? "Resource Tracker", ref _trackerEnableResources);

                      ImGui.SameLine();
                      ImGui.Checkbox(Lang.Get("Settings_TrackerVip") ?? "VIP/Tac Mob Tracker", ref _trackerEnableVipMobs);
                      ImGui.SameLine();
                      ImGui.Checkbox(Lang.Get("Settings_TrackerNormal") ?? "Normal Mob Tracker", ref _trackerEnableNormalMobs);
                      if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_TrackerTooltip") ?? "Tum dusman moblar icin lazer\nUyari: Cok fazla mob varsa ekran dolabilir!");
                    */


                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Settings_SoundTitle") ?? "Sounds");
                    ImGui.Checkbox(Lang.Get("Settings_EnableSound") ?? "Enable Sounds", ref _enableSoundAlerts);

                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.00f, 1.00f, 1.00f, 1f), "UI Tema");
                    ImGui.Spacing();

                    // SADECE 2 TEMA (Original ve Obsidian)
                    string[] themeNames = { "Original", "Obsidian" };
                    ImGui.SetNextItemWidth(200);
                    ImGui.Combo("##ThemeSelect", ref _selectedTheme, themeNames, themeNames.Length);

                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.00f, 1.00f, 1.00f, 1f), Lang.Get("Settings_Stream_Module") ?? "Stream-Bypass");
                    ImGui.Spacing();
                    bool prevStream = _streamModuleEnabled;
                    ImGui.Checkbox((Lang.Get("Settings_OBS") ?? "OBS Bypass") + "##StreamMod", ref _streamModuleEnabled);
                    if (_streamModuleEnabled != prevStream) ApplyStreamModule();

                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.00f, 1.00f, 1.00f, 1f), Lang.Get("Settings_Language") ?? "Dil / Language");
                    ImGui.Spacing();
                    int prevLangIdx = _selectedLangIndex;
                    ImGui.SetNextItemWidth(200);


                    if (ImGui.Combo("##LangSettings", ref _selectedLangIndex, _languages, _languages.Length))
                    {
                        string newLang = _selectedLangIndex switch { 0 => "TR", 1 => "EN", 2 => "RU", 3 => "ZH", _ => "TR" };

                        Lang.LoadLanguage(newLang);
                        ApplyLanguageFont(newLang);


                        MobMapper.Instance.Reload($"Assets/Helper/mobs_{newLang}.min.json");
                        CheckAndLoadDatabase();  // Artık bu tek başına yeterli

                        _lastTabLanguage = null;
                    }


                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Settings_HotkeyTitle") ?? "Hotkeys");

                    string btnText = _isChangingHotkey ? Lang.Get("Settings_HotkeyWait") ?? "Wait..." : string.Format(Lang.Get("Settings_HotkeyMenu") ?? "Toggle: {0}", GetKeyName(_toggleKey));
                    if (ImGui.Button(btnText, new Vector2(250, 30)))
                    {
                        _isChangingHotkey = true;
                        _isChangingMuteHotkey = false;
                    }

                    string muteBtnText = _isChangingMuteHotkey ? Lang.Get("Settings_HotkeyWait") ?? "Wait..." : string.Format(Lang.Get("Settings_HotkeyMute") ?? "Mute: {0}", GetKeyName(_muteToggleKey));
                    if (ImGui.Button(muteBtnText, new Vector2(250, 30)))
                    {
                        _isChangingMuteHotkey = true;
                        _isChangingHotkey = false;
                    }

                    ImGui.SameLine();

                    if (!_enableSoundAlerts)
                        ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), Lang.Get("Settings_SoundOff") ?? "OFF");
                    else
                        ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), Lang.Get("Settings_SoundOn") ?? "ON");

                    if (_isChangingHotkey || _isChangingMuteHotkey)
                    {
                        int pressed = GetPressedKey();
                        if (pressed != -1 && pressed != 0x01 && pressed != 0x02)
                        {
                            if (pressed == 0x1B || pressed == 0x21 || pressed == 0x22 || pressed == 0x23 || pressed == 0x24)
                            {
                                _isChangingHotkey = false;
                                _isChangingMuteHotkey = false;
                            }
                            else
                            {
                                if (_isChangingHotkey) _toggleKey = pressed;
                                if (_isChangingMuteHotkey) _muteToggleKey = pressed;

                                _isChangingHotkey = false;
                                _isChangingMuteHotkey = false;
                            }
                        }
                    }
                    ImGui.Separator();

                    break;
                #endregion
                #region Device (Adapter & VPN)
                case 6:
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "Ağ Bağdaştırıcısı Ayarları (VPN & Booster Support)");
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.TextWrapped(Lang.Get("Device_VPN") ?? "Hiçbir ağ kartı bulunamadı!");
                    ImGui.Spacing();

                    if (!_adaptersLoaded)
                    {
                        _availableAdapters = PacketEngine.GetAvailableAdapters();
                        _adaptersLoaded = true;

                        string saved = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "last_adapter.txt"))
                            ? File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "last_adapter.txt")).Trim() : "";

                        int idx = _availableAdapters.IndexOf(saved);
                        if (idx != -1) _selectedAdapterIndex = idx;
                    }

                    if (_availableAdapters.Count > 0)
                    {
                        ImGui.SetNextItemWidth(400);
                        if (ImGui.Combo("##NetworkAdapter", ref _selectedAdapterIndex, _availableAdapters.ToArray(), _availableAdapters.Count))
                        {
                            PacketEngine.SaveSelectedAdapter(_availableAdapters[_selectedAdapterIndex]);
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Hiçbir ağ kartı bulunamadı! Npcap kurduğunuzdan emin olun.");
                    }

                    ImGui.Spacing();

                    if (ImGui.Button(Lang.Get("Device_Button1") ?? "Uygulamayı Yeniden Başlat (Restart)", new Vector2(300, 35)))
                    {
                        System.Windows.Forms.Application.Restart();
                        Environment.Exit(0);
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // ==========================================
                    // YENİ: AĞ TANILAMA (DIAGNOSTIC) BÖLÜMÜ
                    // ==========================================
                    ImGui.TextColored(new Vector4(0f, 1f, 1f, 1f), Lang.Get("Device_Discovery"));
                    ImGui.TextWrapped(Lang.Get("Device_DiscoveryText") ?? "Hangi adaptörünüzün Albion Online verisi (5055/5056/5057 UDP) aldığını tespit etmek için oyuna girip hareket ederken bu testi başlatın.");

                    if (_isTestingAdapters)
                    {
                        // Test sırasında ekran donmasın diye adama "Bekle" yazısı gösteriyoruz
                        ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), Lang.Get("Device_Search") ?? "3 Saniye Bekleyiniz.");
                    }
                    else
                    {
                        if (ImGui.Button(Lang.Get("Device_Button2") ?? "Ağları Test Et (Albion Trafiği Ara)", new Vector2(300, 35)))
                        {
                            _isTestingAdapters = true;
                            _adapterTestResults.Clear();

                            // Arayüz donmasın diye testi arka planda (Task) çalıştırıyoruz
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                _adapterTestResults = PacketEngine.TestAllAdaptersForAlbion();
                                _isTestingAdapters = false;
                            });
                        }
                    }

                    // Test sonuçları geldiyse ekrana bas
                    if (_adapterTestResults.Count > 0 && !_isTestingAdapters)
                    {
                        ImGui.Spacing();
                        if (ImGui.BeginChild("AdapterTestResults", new Vector2(0, 200), ImGuiChildFlags.Borders))
                        {
                            foreach (var kvp in _adapterTestResults)
                            {
                                if (kvp.Value)
                                {
                                    // Trafik olan kartı YEŞİL ile kocaman [ YES ] yazarak gösteriyoruz
                                    ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), $"[ YES ] 5055/5056/5057 -> {kvp.Key}");
                                }
                                else
                                {
                                    // Boş kartları GRİ/SÖNÜK şekilde gösteriyoruz
                                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"[ NO  ] 5055/5056/5057 -> {kvp.Key}");
                                }
                            }
                        }
                        ImGui.EndChild();
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), Lang.Get("Device_Info") ?? "* Lütfen [ YES ] yazan adaptörü yukarıdaki menüden seçip 'Restart' atın.");
                    }
                    break;
                #endregion
                #region Geliştirme Araçları
                case 4: // Dev Tools
                    if (ImGui.BeginTabBar("DevToolsTabs"))
                    {

                        #region Sekme 1 [Debug & Simulator]
                        // --- 1. SEKME: DEBUG & SIMULATOR ---
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabDebug") ?? "Debug"))
                        {
                            ImGui.Spacing();
                            ImGui.Checkbox(Lang.Get("Dev_ConsoleLog") ?? "Log", ref _debugConsoleLog);
                            ImGui.Checkbox(Lang.Get("Dev_MobID") ?? "Mob ID", ref _debugMobs);
                            ImGui.Checkbox(Lang.Get("Dev_ResID") ?? "Res ID", ref _debugStaticResources);
                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Dev_SimTitle") ?? "Sim");

                            if (ImGui.CollapsingHeader(Lang.Get("Dev_MobHeader") ?? "Mob Sim"))
                            {
                                ImGui.Text(Lang.Get("Dev_MobSearch") ?? "Search");
                                if (ImGui.InputText("##MobSearchSim", ref _simMobSearch, 64) || _searchRefreshNeeded)
                                {
                                    _searchRefreshNeeded = false;
                                    string rawQuery = _simMobSearch.Trim();
                                    string normalizedQuery = NormalizeSearchText(rawQuery);
                                    if (string.IsNullOrEmpty(_simMobSearch)) _cachedDatabaseResults = _mobDatabase.ToList();
                                    else _cachedDatabaseResults = _mobDatabase.Where(x => NameMatchesSearch(x.Value.Name, normalizedQuery) || x.Key.ToString().Contains(rawQuery)).ToList();
                                }

                                if (ImGui.BeginChild("SimMobList", new Vector2(0, 200), ImGuiChildFlags.Borders))
                                {
                                    string[] categories = { "Mob", "Miniboss", "Boss", "Sniffer", "Crystals" };
                                    foreach (var cat in categories)
                                    {
                                        var catMatches = _cachedDatabaseResults.Where(x => GetMobCategory(x.Value.Name, x.Value.Tier) == cat).ToList();
                                        if (ImGui.TreeNodeEx($"{cat} ({catMatches.Count})##Sim{cat}", ImGuiTreeNodeFlags.DefaultOpen))
                                        {
                                            if (catMatches.Count == 0) ImGui.TextDisabled(Lang.Get("Dev_NoResult") ?? "No result");
                                            else
                                            {
                                                foreach (var m in catMatches)
                                                {
                                                    bool isSelected = (_simMobId == m.Key);
                                                    if (ImGui.Selectable($"[{m.Key}] {m.Value.Name} (T{m.Value.Tier})##SimSel{m.Key}", isSelected))
                                                        _simMobId = m.Key;
                                                    if (isSelected) ImGui.SetItemDefaultFocus();
                                                }
                                            }
                                            ImGui.TreePop();
                                        }
                                    }
                                }
                                ImGui.EndChild();

                                ImGui.Spacing();
                                string selectedMobName = _mobDatabase.ContainsKey(_simMobId) ? _mobDatabase[_simMobId].Name : "Unknown";
                                ImGui.TextColored(new Vector4(0, 1, 0, 1), string.Format(Lang.Get("Dev_Selected") ?? "Sel: {0}", _simMobId, selectedMobName));

                                if (ImGui.Button(Lang.Get("Dev_SpawnMob") ?? "Spawn", new Vector2(-1, 30)))
                                {
                                    var p = _gameStateManager.GetPlayer();
                                    float bx = (p != null) ? p.PositionX : 0; float by = (p != null) ? p.PositionY : 0;
                                    Random rnd = new Random();
                                    float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                                    float dist = 10.0f + (float)(rnd.NextDouble() * 10.0f);
                                    _gameStateManager.AddDebugMob(_simMobId, bx + (float)Math.Cos(angle) * dist, by + (float)Math.Sin(angle) * dist, selectedMobName);
                                }
                            }

                            if (ImGui.CollapsingHeader(Lang.Get("Dev_ResHeader") ?? "Res Sim"))
                            {
                                ImGui.Text(Lang.Get("Dev_ResSearch") ?? "Search");
                                ImGui.InputText("##ResSearch", ref _simResSearch, 64);

                                if (ImGui.BeginChild("SimResList", new Vector2(0, 150), ImGuiChildFlags.Borders))
                                {
                                    for (int i = 0; i <= 30; i++)
                                    {
                                        var cat = GetCategoryFromTypeId(i);
                                        string catName = cat.ToString();
                                        string displayText = $"ID: {i} - {catName}";

                                        if (string.IsNullOrEmpty(_simResSearch) || catName.Contains(_simResSearch, StringComparison.OrdinalIgnoreCase) || i.ToString().Contains(_simResSearch))
                                        {
                                            bool isSelected = (_simResType == i);
                                            if (ImGui.Selectable(displayText, isSelected)) { _simResType = i; }
                                        }
                                    }
                                }
                                ImGui.EndChild();

                                ImGui.Spacing();
                                ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), string.Format(Lang.Get("Dev_SelectedType") ?? "Sel: {0}", _simResType, GetCategoryFromTypeId(_simResType)));

                                ImGui.Columns(2, "ResSettings", false);
                                ImGui.InputInt(Lang.Get("Dev_Tier") ?? "Tier", ref _simResTier);
                                ImGui.InputInt(Lang.Get("Dev_Enchant") ?? "Enchant", ref _simResEnchant);
                                ImGui.NextColumn();
                                ImGui.InputInt(Lang.Get("Dev_Count") ?? "Count", ref _simResCount);
                                ImGui.InputInt(Lang.Get("Dev_Capacity") ?? "Cap", ref _simResCap);
                                ImGui.Columns(1);

                                if (ImGui.Button(Lang.Get("Dev_SpawnRes") ?? "Spawn", new Vector2(-1, 30)))
                                {
                                    var p = _gameStateManager.GetPlayer();
                                    float bx = (p != null) ? p.PositionX : 0; float by = (p != null) ? p.PositionY : 0;
                                    Random rnd = new Random();
                                    float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                                    float dist = 10.0f + (float)(rnd.NextDouble() * 10.0f);
                                    _gameStateManager.AddDebugHarvestable(_simResType, _simResTier, _simResCount, _simResCap, _simResEnchant, bx + (float)Math.Cos(angle) * dist, by + (float)Math.Sin(angle) * dist);
                                }
                            }
#endregion
                        #region Sekme 2 [Simulated Entities]
                            ImGui.Separator();
                            ImGui.Text(Lang.Get("Dev_ActiveSims") ?? "Active Sims");

                            if (ImGui.BeginTable("SimTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                            {
                                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 50);
                                ImGui.TableSetupColumn(Lang.Get("Dev_ColType") ?? "Type");
                                ImGui.TableSetupColumn(Lang.Get("Dev_ColDetail") ?? "Detail");
                                ImGui.TableSetupColumn(Lang.Get("Dev_ColAction") ?? "Action", ImGuiTableColumnFlags.WidthFixed, 60);
                                ImGui.TableHeadersRow();

                                List<Mob> fakeMobs = new List<Mob>();
                                lock (_dataLock) { _mobBuffer.Clear(); _gameStateManager.GetMobs(_mobBuffer); fakeMobs = _mobBuffer.Where(x => x.Id < 0).ToList(); }
                                foreach (var m in fakeMobs)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(0); ImGui.Text(m.Id.ToString());
                                    ImGui.TableSetColumnIndex(1); ImGui.Text(Lang.Get("Dev_TypeMob") ?? "Mob");
                                    ImGui.TableSetColumnIndex(2); ImGui.Text($"{m.Name}");
                                    ImGui.TableSetColumnIndex(3); if (ImGui.SmallButton($"{Lang.Get("Dev_DeleteBtn") ?? "Del"}##M{m.Id}")) { _gameStateManager.RemoveDebugEntity(m.Id); }
                                }

                                List<Harvestable> fakeRes = new List<Harvestable>();
                                lock (_dataLock) { _harvestBuffer.Clear(); _gameStateManager.GetHarvestables(_harvestBuffer); fakeRes = _harvestBuffer.Where(x => x.Id < 0).ToList(); }
                                foreach (var r in fakeRes)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(0); ImGui.Text(r.Id.ToString());
                                    ImGui.TableSetColumnIndex(1); ImGui.Text(Lang.Get("Dev_TypeResource") ?? "Res");
                                    ImGui.TableSetColumnIndex(2); ImGui.Text($"T{r.Tier}.{r.EnchantmentLevel} {GetCategoryFromTypeId(r.Type)} ({r.Count}/{r.Capacity})");
                                    ImGui.TableSetColumnIndex(3); if (ImGui.SmallButton($"SIL##R{r.Id}")) { _gameStateManager.RemoveDebugEntity(r.Id); }
                                }
                                ImGui.EndTable();
                            }

                            if (ImGui.Button(Lang.Get("Dev_ClearAll") ?? "Clear", new Vector2(-1, 30))) { _gameStateManager.ClearAllData(); }
                            ImGui.EndTabItem();
                        }
                            #endregion
                        #region Sekme 3 [Mobs DB & Tracking]
                        // --- 2. SEKME: MOBS (DB VE TAKÄ°P) ---
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabMobs") ?? "Mobs DB"))
                        {
                            if (ImGui.BeginTabBar("MobSubTabs"))
                            {
                                string[] categories = { "Mob", "Miniboss", "Boss", "Sniffer", "Crystals" };

                                if (ImGui.BeginTabItem(Lang.Get("Dev_TabTracked") ?? "Tracked"))
                                {
                                    ImGui.TextColored(new Vector4(0, 1, 0, 1), Lang.Get("Dev_TrackedTitle") ?? "Tracked Mobs");
                                    bool filterChanged = ImGui.InputText(Lang.Get("Dev_TrackedFilter") ?? "Filter", ref _trackedListFilter, 32);

                                    if (filterChanged || _cachedTrackedResults.Count != _customPriorityMobs.Count)
                                    {
                                        _cachedTrackedResults = _customPriorityMobs.ToList();
                                        if (!string.IsNullOrEmpty(_trackedListFilter))
                                        {
                                            _cachedTrackedResults = _cachedTrackedResults.Where(id =>
                                                id.ToString().Contains(_trackedListFilter) ||
                                                (_mobDatabase.ContainsKey(id) && _mobDatabase[id].Name.Contains(_trackedListFilter, StringComparison.OrdinalIgnoreCase))
                                            ).ToList();
                                        }
                                    }

                                    ImGui.BeginChild("ConfigMobsList", new Vector2(0, 0), ImGuiChildFlags.Borders);
                                    foreach (var cat in categories)
                                    {
                                        var mobsInCat = _cachedTrackedResults.Where(id =>
                                        {
                                            if (!_mobDatabase.ContainsKey(id)) return cat == "Mob";
                                            return GetMobCategory(_mobDatabase[id].Name, _mobDatabase[id].Tier) == cat;
                                        }).ToList();

                                        if (ImGui.TreeNodeEx($"{cat} ({mobsInCat.Count})", ImGuiTreeNodeFlags.DefaultOpen))
                                        {
                                            foreach (var id in mobsInCat)
                                            {
                                                string name = _mobDatabase.ContainsKey(id) ? _mobDatabase[id].Name : "Unknown";
                                                ImGui.Text($"ID: {id} - {name}");
                                                ImGui.SameLine(ImGui.GetWindowWidth() - 70);
                                                if (ImGui.SmallButton($"SIL##{id}")) { _customPriorityMobs.Remove(id); _cachedTrackedResults.Remove(id); }
                                            }
                                            ImGui.TreePop();
                                        }
                                    }
                                    ImGui.EndChild();
                                    ImGui.EndTabItem();
                                }

                                if (ImGui.BeginTabItem(Lang.Get("Dev_TabAllDb") ?? "Database"))
                                {
                                    ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Dev_DbTitle") ?? "Database");
                                   /* ImGui.Text(Lang.Get("Dev_DbSearch") ?? "Search");*/

                                    if (ImGui.InputText(Lang.Get("Dev_DbSearchInput") ?? "##DB", ref _mobSearchQuery, 64) || _searchRefreshNeeded)
                                    {
                                        _searchRefreshNeeded = false;
                                        string rawQuery = _mobSearchQuery.Trim();
                                        string normalizedQuery = NormalizeSearchText(rawQuery);
                                        if (string.IsNullOrEmpty(_mobSearchQuery)) _cachedDatabaseResults = _mobDatabase.ToList();
                                        else _cachedDatabaseResults = _mobDatabase.Where(x => NameMatchesSearch(x.Value.Name, normalizedQuery) || x.Key.ToString().Contains(rawQuery)).ToList();
                                    }

                                    ImGui.BeginChild("DbList", new Vector2(0, 0), ImGuiChildFlags.Borders);
                                    foreach (var cat in categories)
                                    {
                                        var catMatches = _cachedDatabaseResults.Where(x => GetMobCategory(x.Value.Name, x.Value.Tier) == cat).ToList();
                                        if (ImGui.TreeNodeEx($"{cat} ({catMatches.Count})", ImGuiTreeNodeFlags.DefaultOpen))
                                        {
                                            if (catMatches.Count == 0) ImGui.TextDisabled(Lang.Get("Dev_NoResult") ?? "No Result");
                                            else
                                            {
                                                foreach (var m in catMatches)
                                                {
                                                    ImGui.Text($"[{m.Key}] {m.Value.Name}");
                                                    float avail = ImGui.GetContentRegionAvail().X;
                                                    ImGui.SameLine(avail - 110);

                                                    if (ImGui.SmallButton($"{Lang.Get("Dev_DbSpawnBtn") ?? "Spawn"}##DB{m.Key}"))
                                                    {
                                                        var p = _gameStateManager.GetPlayer();
                                                        float bx = (p != null) ? p.PositionX : 0; float by = (p != null) ? p.PositionY : 0;
                                                        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                                                        float dist = 10.0f + (float)(_rng.NextDouble() * 10.0f);
                                                        _gameStateManager.AddDebugMob(m.Key, bx + (float)Math.Cos(angle) * dist, by + (float)Math.Sin(angle) * dist, m.Value.Name);
                                                    }

                                                    ImGui.SameLine();
                                                    if (_customPriorityMobs.Contains(m.Key)) { ImGui.TextColored(new Vector4(0, 1, 0, 1), "EKLI"); }
                                                    else { if (ImGui.SmallButton($"{Lang.Get("Dev_DbAddBtn") ?? "Add"}##DB{m.Key}")) _customPriorityMobs.Add(m.Key); }
                                                }
                                            }
                                            ImGui.TreePop();
                                        }
                                    }
                                    ImGui.EndChild();
                                    ImGui.EndTabItem();
                                }
                                ImGui.EndTabBar();
                            }
                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 4 [PNG]
                        // --- 3. SEKME: PNG (Özel İkon Yönetimi) ---
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabPng") ?? "Icons"))
                        {
                            if (ImGui.BeginTabBar("PngSubTabs"))
                            {
                                if (ImGui.BeginTabItem("Crown"))
                                {
                                    ImGui.InputText(Lang.Get("Dev_CrownSearch") ?? "Search", ref _crownSearchQuery, 64);
                                    ImGui.Spacing();

                                    // EKRANI İKİYE BÖLÜYORUZ (SÜTUN SİSTEMİ)
                                    ImGui.Columns(2, "CrownSplitUI", true);

                                    // ==========================================
                                    // SOL SÜTUN: TAÇLI MOBLAR (Crowned)
                                    // ==========================================
                                    ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f), "Taçlı Moblar (Crowned)");
                                    ImGui.Separator();
                                    if (ImGui.BeginChild("CrownedListChild", new Vector2(0, 0), ImGuiChildFlags.None))
                                    {
                                        var crownedMobs = _mobDatabase.Where(x =>
                                        {
                                            string upName = x.Value.Name.ToUpperInvariant();
                                            bool isBoss = upName.Contains("BOSS") || upName.Contains("ASPECT") || upName.Contains("TITAN") || upName.Contains("GUARDIAN") || upName.Contains("OLD_WHITE");

                                            // Boss ise veya Whitelist'teyse VE Blacklist'te DEĞİLSE taçlıdır
                                            bool hasCrown = (isBoss || _crownWhitelist.Contains(x.Key)) && !_crownBlacklist.Contains(x.Key);

                                            if (!string.IsNullOrEmpty(_crownSearchQuery))
                                            {
                                                return hasCrown && (x.Value.Name.Contains(_crownSearchQuery, StringComparison.OrdinalIgnoreCase) || x.Key.ToString().Contains(_crownSearchQuery));
                                            }
                                            return hasCrown;
                                        }).ToList();

                                        foreach (var m in crownedMobs)
                                        {
                                            ImGui.Text($"[{m.Key}] {m.Value.Name}");
                                            ImGui.SameLine(ImGui.GetWindowWidth() - 75);

                                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                                            if (ImGui.SmallButton($"{Lang.Get("Dev_CrownRemove") ?? "Tacı Sil"}##rem{m.Key}"))
                                            {
                                                _crownBlacklist.Add(m.Key);
                                                _crownWhitelist.Remove(m.Key);
                                            }
                                            ImGui.PopStyleColor();
                                        }
                                    }
                                    ImGui.EndChild();

                                    ImGui.NextColumn();

                                    // ==========================================
                                    // SAĞ SÜTUN: NORMAL MOBLAR (No Crown)
                                    // ==========================================
                                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Normal Moblar");
                                    ImGui.Separator();
                                    if (ImGui.BeginChild("NormalListChild", new Vector2(0, 0), ImGuiChildFlags.None))
                                    {
                                        var normalMobs = _mobDatabase.Where(x =>
                                        {
                                            string upName = x.Value.Name.ToUpperInvariant();
                                            bool isBoss = upName.Contains("BOSS") || upName.Contains("ASPECT") || upName.Contains("TITAN") || upName.Contains("GUARDIAN") || upName.Contains("OLD_WHITE");

                                            bool hasCrown = (isBoss || _crownWhitelist.Contains(x.Key)) && !_crownBlacklist.Contains(x.Key);

                                            if (!string.IsNullOrEmpty(_crownSearchQuery))
                                            {
                                                return !hasCrown && (x.Value.Name.Contains(_crownSearchQuery, StringComparison.OrdinalIgnoreCase) || x.Key.ToString().Contains(_crownSearchQuery));
                                            }
                                            return !hasCrown;
                                        }).ToList();

                                        // Kasmaması için 150 limit koydum, arama yapınca hepsi gelir
                                        foreach (var m in normalMobs.Take(150))
                                        {
                                            ImGui.Text($"[{m.Key}] {m.Value.Name}");
                                            ImGui.SameLine(ImGui.GetWindowWidth() - 75);

                                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.2f, 1f));
                                            if (ImGui.SmallButton($"{Lang.Get("Dev_CrownGive") ?? "Taç Ekle"}##add{m.Key}"))
                                            {
                                                _crownWhitelist.Add(m.Key);
                                                _crownBlacklist.Remove(m.Key);
                                            }
                                            ImGui.PopStyleColor();
                                        }
                                    }
                                    ImGui.EndChild();

                                    // Sütunları kapat
                                    ImGui.Columns(1);

                                    ImGui.EndTabItem();
                                }
                                ImGui.EndTabBar();
                            }
                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 5 [Trackers]
                        // --- 4. SEKME: TRACKERS ---
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabTrackers") ?? "Trackers"))
                        {
                            ImGui.Spacing();
                            ImGui.Checkbox(Lang.Get("Dev_TrackerResources") ?? "Res", ref _trackerEnableResources);
                            ImGui.SameLine();
                            ImGui.Checkbox(Lang.Get("Dev_TrackerShowResIcon") ?? "Kaynak İkonunu Göster", ref _trackerShowResourceIcons);

                            ImGui.Checkbox(Lang.Get("Dev_TrackerVip") ?? "Vip", ref _trackerEnableVipMobs);
                            ImGui.SameLine();
                            ImGui.Checkbox(Lang.Get("Dev_TrackerShowMobIcon") ?? "Mob İkonunu Göster", ref _trackerShowMobIcons);

                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Dev_TrackerListTitle") ?? "Tracker List");
                            ImGui.InputText(Lang.Get("Dev_TrackerSearch") ?? "Search", ref _trackerSearchQuery, 64);

                            if (ImGui.BeginChild("TrackerSearchRes", new Vector2(0, 120), ImGuiChildFlags.Borders))
                            {
                                if (!string.IsNullOrEmpty(_trackerSearchQuery))
                                {
                                    string rawQuery = _trackerSearchQuery.Trim();
                                    string normalizedQuery = NormalizeSearchText(rawQuery);
                                    var matches = _mobDatabase.Where(x => NameMatchesSearch(x.Value.Name, normalizedQuery) || x.Key.ToString().Contains(rawQuery))
                                                            .OrderByDescending(x => NormalizeSearchText(x.Value.Name) == normalizedQuery || x.Key.ToString() == rawQuery)
                                                            .ThenBy(x => x.Value.Name)
                                                            .Take(50);
                                    foreach (var m in matches)
                                    {
                                        if (ImGui.Selectable($"[{m.Key}] {m.Value.Name}##Add{m.Key}", _selectedMobIdForTracker == m.Key))
                                            _selectedMobIdForTracker = m.Key;
                                    }
                                }
                            }
                            ImGui.EndChild();

                            if (ImGui.Button(Lang.Get("Dev_TrackerAddBtn") ?? "Add") && _selectedMobIdForTracker != -1)
                            {
                                _trackerCustomMobs.Add(_selectedMobIdForTracker);
                                _selectedMobIdForTracker = -1;
                            }

                            ImGui.Separator();
                            ImGui.Text(Lang.Get("Dev_TrackerListHeader") ?? "List");
                            if (ImGui.BeginChild("TrackerAddedList", new Vector2(0, 150), ImGuiChildFlags.Borders))
                            {
                                string[] categories = { "Mob", "Miniboss", "Boss", "Sniffer", "Crystals" };
                                int idToRemoveTrk = -1;

                                var filteredList = _trackerCustomMobs.ToList();
                                if (!string.IsNullOrEmpty(_trackerSearchQuery))
                                {
                                    filteredList = filteredList.Where(id =>
                                        id.ToString().Contains(_trackerSearchQuery) ||
                                        (_mobDatabase.ContainsKey(id) && _mobDatabase[id].Name.Contains(_trackerSearchQuery, StringComparison.OrdinalIgnoreCase))
                                    ).ToList();
                                }

                                foreach (var cat in categories)
                                {
                                    var mobsInCat = filteredList.Where(id => {
                                        if (!_mobDatabase.ContainsKey(id)) return cat == "Mob";
                                        return GetMobCategory(_mobDatabase[id].Name, _mobDatabase[id].Tier) == cat;
                                    }).ToList();

                                    if (mobsInCat.Count > 0)
                                    {
                                        if (ImGui.TreeNodeEx($"{cat} ({mobsInCat.Count})##TrkCat{cat}", ImGuiTreeNodeFlags.DefaultOpen))
                                        {
                                            foreach (var id in mobsInCat)
                                            {
                                                string name = _mobDatabase.ContainsKey(id) ? _mobDatabase[id].Name : "???";
                                                ImGui.Text($"[{id}] {name}");
                                                ImGui.SameLine(ImGui.GetWindowWidth() - 60);
                                                if (ImGui.SmallButton($"{Lang.Get("Dev_TrackerRemove") ?? "Rem"}##Trk{id}")) idToRemoveTrk = id;
                                            }
                                            ImGui.TreePop();
                                        }
                                    }
                                }
                                if (idToRemoveTrk != -1) _trackerCustomMobs.Remove(idToRemoveTrk);
                            }
                            ImGui.EndChild();

                            /*    ImGui.Separator();
                                ImGui.SliderFloat(Lang.Get("Dev_LaserX") ?? "Laser X", ref _trackerScreenOffsetX, -300f, 300f);
                                ImGui.SliderFloat(Lang.Get("Dev_LaserY") ?? "Laser Y", ref _trackerScreenOffsetY, -300f, 300f);
                                ImGui.SliderFloat(Lang.Get("Dev_LaserGap") ?? "Laser Gap", ref _trackerStartGap, 0f, 200f);*/
/*
                            ImGui.Separator();
                            ImGui.Spacing();
                            ImGui.TextColored(new Vector4(1, 0.5f, 1, 1), Lang.Get("Dev_TrackerVisual") ?? "Visuals");
                            ImGui.ColorEdit4(Lang.Get("Dev_LaserColorMob") ?? "Mob Col", ref _trackerLaserColorMobs);
                            ImGui.ColorEdit4(Lang.Get("Dev_LaserColorRes") ?? "Res Col", ref _trackerLaserColorResources);

                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), Lang.Get("Settings_LaserCalibrationTitle") ?? "Laser Kalibrasyon");
                            ImGui.TextDisabled(Lang.Get("Settings_LaserCalibStep1") ?? "Adim 1: Tam sag/solunuzdaki kaynakla Scale X, tam onunuzdakiyle Scale Y ayarlayin.");
                            ImGui.SetNextItemWidth(210f);
                            ImGui.SliderFloat(Lang.Get("Settings_LaserScaleX") ?? "Scale X (sag/sol)", ref _trackerScaleX, 0.5f, 50.0f);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserScaleXTip") ?? "Dunyada tam saginizda/solunuzda bir kaynak alin.\nLazer ucu tam ustune gelene kadar ayarlayin.");
                            ImGui.SetNextItemWidth(210f);
                            ImGui.SliderFloat(Lang.Get("Settings_LaserScaleY") ?? "Scale Y (ileri/geri)", ref _trackerScaleY, 0.5f, 50.0f);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserScaleYTip") ?? "Dunyada tam onunuzde/arkanizda bir kaynak alin.\nLazer ucu tam ustune gelene kadar ayarlayin.");
                            ImGui.SetNextItemWidth(210f);
                            ImGui.SliderFloat(Lang.Get("Settings_LaserAngle") ?? "Aci Ofseti (derece)", ref _trackerAngleOffset, -45f, 45f);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserAngleTip") ?? "Tum lazerler ayni yonde kayiyorsa buradan duzelt.\nDefault: 0");
                            ImGui.SetNextItemWidth(210f);
                            ImGui.SliderFloat(Lang.Get("Settings_LaserOffsetX") ?? "Uc Ofset X", ref _trackerLaserEndOffsetX, -200f, 200f);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserOffsetXTip") ?? "Lazer ucunu saga/sola kaydirma (ince ayar)");
                            ImGui.SetNextItemWidth(210f);
                            ImGui.SliderFloat(Lang.Get("Settings_LaserOffsetY") ?? "Uc Ofset Y", ref _trackerLaserEndOffsetY, -200f, 200f);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserOffsetYTip") ?? "Lazer ucunu yukari/asagi kaydirma (ince ayar)");
                            if (ImGui.Button(Lang.Get("Settings_LaserResetBtn") ?? "Kalibrasyonu Sifirla")) { _trackerScaleX = 7f; _trackerScaleY = 7f; _trackerAngleOffset = 0f; _trackerLaserEndOffsetX = 0f; _trackerLaserEndOffsetY = 0f; }
                            ImGui.SameLine();
                            if (ImGui.Button(Lang.Get("Settings_LaserSaveBtn") ?? "Kalibrasyonu Kaydet"))
                            {
                                string saveName = !string.IsNullOrWhiteSpace(_configFileNameInput) ? _configFileNameInput : "default";
                                SaveConfig(saveName);
                            }
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("Settings_LaserSaveTip") ?? "Ayarlari Config sekmesindeki aktif profil adiyla kaydeder.\nConfig sekmesinden profil adi girmeyi unutmayin!");
*/
                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 8 [Player Decode]
                        if (ImGui.BeginTabItem("Player Decode"))
                        {
                            ImGui.TextColored(new Vector4(0.45f, 1f, 0.45f, 1f), "Player XY Decode Paths (Test)");
                            ImGui.TextDisabled("Ayni anda birden fazla path acarak hizli A/B test yapabilirsin.");
                            ImGui.Separator();

                            bool d01 = PlayersHandler.DecodePath01_Int1e7_1_9;
                            if (ImGui.Checkbox("01 Int [1,9] / 1e7", ref d01)) PlayersHandler.DecodePath01_Int1e7_1_9 = d01;
                            bool d02 = PlayersHandler.DecodePath02_Int1e6_1_9;
                            if (ImGui.Checkbox("02 Int [1,9] / 1e6", ref d02)) PlayersHandler.DecodePath02_Int1e6_1_9 = d02;
                            bool d03 = PlayersHandler.DecodePath03_Int1e5_1_9;
                            if (ImGui.Checkbox("03 Int [1,9] / 1e5", ref d03)) PlayersHandler.DecodePath03_Int1e5_1_9 = d03;
                            bool d04 = PlayersHandler.DecodePath04_Int100_1_9;
                            if (ImGui.Checkbox("04 Int [1,9] / 100", ref d04)) PlayersHandler.DecodePath04_Int100_1_9 = d04;
                            bool d05 = PlayersHandler.DecodePath05_Float_1_9;
                            if (ImGui.Checkbox("05 Float [1,9]", ref d05)) PlayersHandler.DecodePath05_Float_1_9 = d05;

                            ImGui.Separator();

                            bool d06 = PlayersHandler.DecodePath06_Int1e7_9_13;
                            if (ImGui.Checkbox("06 Int [9,13] / 1e7", ref d06)) PlayersHandler.DecodePath06_Int1e7_9_13 = d06;
                            bool d07 = PlayersHandler.DecodePath07_Int1e6_9_13;
                            if (ImGui.Checkbox("07 Int [9,13] / 1e6", ref d07)) PlayersHandler.DecodePath07_Int1e6_9_13 = d07;
                            bool d08 = PlayersHandler.DecodePath08_Int1e5_9_13;
                            if (ImGui.Checkbox("08 Int [9,13] / 1e5", ref d08)) PlayersHandler.DecodePath08_Int1e5_9_13 = d08;
                            bool d09 = PlayersHandler.DecodePath09_Int100_9_13;
                            if (ImGui.Checkbox("09 Int [9,13] / 100", ref d09)) PlayersHandler.DecodePath09_Int100_9_13 = d09;
                            bool d10 = PlayersHandler.DecodePath10_Float_9_13;
                            if (ImGui.Checkbox("10 Float [9,13]", ref d10)) PlayersHandler.DecodePath10_Float_9_13 = d10;
                            bool d11 = PlayersHandler.DecodePath11_XInt100YFloat_9_13;
                            if (ImGui.Checkbox("11 X=int/100@9, Y=float@13", ref d11)) PlayersHandler.DecodePath11_XInt100YFloat_9_13 = d11;
                            bool d12 = PlayersHandler.DecodePath12_XFloatYInt100_9_13;
                            if (ImGui.Checkbox("12 X=float@9, Y=int/100@13", ref d12)) PlayersHandler.DecodePath12_XFloatYInt100_9_13 = d12;

                            ImGui.Separator();

                            bool d13 = PlayersHandler.DecodePath13_Param4_5;
                            if (ImGui.Checkbox("13 Params [4,5]", ref d13)) PlayersHandler.DecodePath13_Param4_5 = d13;
                            bool d14 = PlayersHandler.DecodePath14_Param19_25;
                            if (ImGui.Checkbox("14 Params [19,25]", ref d14)) PlayersHandler.DecodePath14_Param19_25 = d14;
                            bool d15 = PlayersHandler.DecodePath15_List0_1;
                            if (ImGui.Checkbox("15 p1 List [0,1]", ref d15)) PlayersHandler.DecodePath15_List0_1 = d15;

                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0.75f, 0.95f, 1f, 1f), "Auto XY Finder (near-self best mode)");

                            var self = _gameStateManager?.GetPlayer();
                            float selfX = self?.CurrentLerpedX ?? 0f;
                            float selfY = self?.CurrentLerpedY ?? 0f;
                            float selfParserX = self?.PositionX ?? 0f;
                            float selfParserY = self?.PositionY ?? 0f;

                            ImGui.TextColored(new Vector4(0.65f, 1f, 0.65f, 1f), $"Suanki Konum (Parser)  X:{selfParserX:F3} / Y:{selfParserY:F3}");
                            ImGui.TextDisabled($"Lerped (Render)        X:{selfX:F3} / Y:{selfY:F3}");

                            var nearby = new List<(int id, string name)>();
                            lock (_dataLock)
                            {
                                foreach (var p in _playersBuffer)
                                {
                                    if (p.Id <= 0) continue;
                                    nearby.Add((p.Id, p.Name ?? string.Empty));
                                }
                            }

                            var known = PlayerParserTraceStore.GetKnownPlayersSnapshot(nearby);
                            if (_parserOnlyNearby)
                            {
                                var set = nearby.Select(x => x.id).ToHashSet();
                                known = known.Where(x => set.Contains(x.id)).ToList();
                            }

                            if (ImGui.BeginChild("PlayerDecodeAutoList", new Vector2(0, 260), ImGuiChildFlags.Borders))
                            {
                                if (known.Count == 0)
                                {
                                    ImGui.TextDisabled("Yakin oyuncu yok.");
                                }
                                else
                                {
                                    foreach (var k in known)
                                    {
                                        var entries = PlayerParserTraceStore.GetPlayerEntries(k.id);
                                        var last = entries.LastOrDefault(e =>
                                            e.eventName.Contains("Move", StringComparison.OrdinalIgnoreCase)
                                            && e.payload.Contains("[1]=", StringComparison.Ordinal));

                                        if (last == default)
                                        {
                                            ImGui.TextDisabled($"[{k.id}] {k.name} -> Move payload yok");
                                            continue;
                                        }

                                        var cands = DecodeCandidatesFromPayload(
                                            last.payload,
                                            PlayersHandler.DecodePath01_Int1e7_1_9,
                                            PlayersHandler.DecodePath02_Int1e6_1_9,
                                            PlayersHandler.DecodePath03_Int1e5_1_9,
                                            PlayersHandler.DecodePath04_Int100_1_9,
                                            PlayersHandler.DecodePath05_Float_1_9,
                                            PlayersHandler.DecodePath06_Int1e7_9_13,
                                            PlayersHandler.DecodePath07_Int1e6_9_13,
                                            PlayersHandler.DecodePath08_Int1e5_9_13,
                                            PlayersHandler.DecodePath09_Int100_9_13,
                                            PlayersHandler.DecodePath10_Float_9_13,
                                            PlayersHandler.DecodePath11_XInt100YFloat_9_13,
                                            PlayersHandler.DecodePath12_XFloatYInt100_9_13,
                                            PlayersHandler.DecodePath13_Param4_5,
                                            PlayersHandler.DecodePath14_Param19_25,
                                            PlayersHandler.DecodePath15_List0_1);

                                        if (cands.Count == 0)
                                        {
                                            ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), $"[{k.id}] {k.name} -> decode yok");
                                            continue;
                                        }

                                        var best = cands
                                            .OrderBy(c => DistSq(c.x, c.y, selfX, selfY))
                                            .First();

                                        float d = MathF.Sqrt(DistSq(best.x, best.y, selfX, selfY));
                                        ImGui.Text($"[{k.id}] {k.name} | {best.mode} | X:{best.x:F1} Y:{best.y:F1} | d:{d:F1}");
                                    }
                                }
                            }
                            ImGui.EndChild();

                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Pointer Scanner (continuous)");
                            ImGui.Checkbox("Enable Pointer Scanner", ref _pointerScannerEnabled);
                            ImGui.Checkbox("Use Manual Target XY (2nd PC)", ref _pointerScannerUseManualTarget);
                            if (_pointerScannerUseManualTarget)
                            {
                                ImGui.SetNextItemWidth(180f);
                                ImGui.InputFloat("Target X", ref _pointerScannerManualTargetX, 1f, 10f, "%.3f");
                                ImGui.SetNextItemWidth(180f);
                                ImGui.InputFloat("Target Y", ref _pointerScannerManualTargetY, 1f, 10f, "%.3f");
                            }
                            ImGui.SliderFloat("Max Distance", ref _pointerScannerMaxDistance, 2f, 150f, "%.1f");
                            ImGui.SliderInt("Max Offset", ref _pointerScannerMaxOffset, 8, 48);
                            ImGui.SliderFloat("Scan Interval ms", ref _pointerScannerIntervalMs, 50f, 1000f, "%.0f");
                            if (ImGui.Button("Clear Candidates")) _pointerScannerCandidates.Clear();

                            if (_pointerScannerEnabled)
                            {
                                double elapsedMs = (DateTime.Now - _pointerScannerLastRun).TotalMilliseconds;
                                if (elapsedMs >= _pointerScannerIntervalMs)
                                {
                                    _pointerScannerLastRun = DateTime.Now;

                                    var self2 = _gameStateManager?.GetPlayer();
                                    float selfX2 = _pointerScannerUseManualTarget ? _pointerScannerManualTargetX : (self2?.CurrentLerpedX ?? 0f);
                                    float selfY2 = _pointerScannerUseManualTarget ? _pointerScannerManualTargetY : (self2?.CurrentLerpedY ?? 0f);

                                    var nearby2 = new List<(int id, string name)>();
                                    lock (_dataLock)
                                    {
                                        foreach (var p in _playersBuffer)
                                        {
                                            if (p.Id <= 0) continue;
                                            nearby2.Add((p.Id, p.Name ?? string.Empty));
                                        }
                                    }

                                    var known2 = PlayerParserTraceStore.GetKnownPlayersSnapshot(nearby2);
                                    if (_parserOnlyNearby)
                                    {
                                        var set = nearby2.Select(x => x.id).ToHashSet();
                                        known2 = known2.Where(x => set.Contains(x.id)).ToList();
                                    }

                                    foreach (var k in known2)
                                    {
                                        var entries = PlayerParserTraceStore.GetPlayerEntries(k.id);
                                        var lastMove = entries.LastOrDefault(e =>
                                            e.eventName.Contains("Move", StringComparison.OrdinalIgnoreCase)
                                            && e.payload.Contains("[1]=", StringComparison.Ordinal));

                                        if (lastMove == default) continue;

                                        var scanCandidates = PointerScanCandidatesFromPayload(lastMove.payload, _pointerScannerMaxOffset);
                                        foreach (var c in scanCandidates)
                                        {
                                            float d = MathF.Sqrt(DistSq(c.x, c.y, selfX2, selfY2));
                                            if (d > _pointerScannerMaxDistance) continue;

                                            string key = c.mode;
                                            if (!_pointerScannerCandidates.TryGetValue(key, out var stat))
                                            {
                                                stat = new PointerCandidateStat();
                                                _pointerScannerCandidates[key] = stat;
                                            }

                                            stat.Hits++;
                                            stat.LastDistance = d;
                                            if (d < stat.BestDistance) stat.BestDistance = d;
                                            stat.LastX = c.x;
                                            stat.LastY = c.y;
                                            stat.LastSeen = DateTime.Now;
                                            stat.LastSource = $"{k.id}:{k.name}";
                                        }
                                    }
                                }
                            }

                            if (ImGui.BeginChild("PointerScannerResults", new Vector2(0, 220), ImGuiChildFlags.Borders))
                            {
                                if (_pointerScannerCandidates.Count == 0)
                                {
                                    ImGui.TextDisabled("Henüz aday yok.");
                                }
                                else
                                {
                                    var ordered = _pointerScannerCandidates
                                        .OrderByDescending(x => x.Value.Hits)
                                        .ThenBy(x => x.Value.BestDistance)
                                        .Take(80)
                                        .ToList();

                                    foreach (var kv in ordered)
                                    {
                                        var s = kv.Value;
                                        ImGui.Text($"{kv.Key} | hits:{s.Hits} | best:{s.BestDistance:F2} | last:{s.LastDistance:F2} | XY:{s.LastX:F1},{s.LastY:F1} | {s.LastSource}");
                                    }
                                }
                            }
                            ImGui.EndChild();

                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 9 [Ports]
                        if (ImGui.BeginTabItem("Ports"))
                        {
                            int targetPort = UdpPortInspector.GetTargetPort();
                            ImGui.TextColored(new Vector4(0.6f, 1f, 0.8f, 1f), $"Target UDP Port: {targetPort}");
                            ImGui.SetNextItemWidth(120f);
                            ImGui.InputInt("Manual Port", ref _manualTargetUdpPortInput);
                            ImGui.SameLine();
                            if (ImGui.Button("Use This Port") && _manualTargetUdpPortInput > 0)
                            {
                                UdpPortInspector.SetTargetPort(_manualTargetUdpPortInput);
                                UdpPortInspector.RequestManualOverride(_manualTargetUdpPortInput);
                            }
                            if (ImGui.Button("Clear Port Stats")) UdpPortInspector.Clear();

                            var ports = UdpPortInspector.Snapshot();
                            if (ImGui.BeginTable("PortStatsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                            {
                                ImGui.TableSetupColumn("Port", ImGuiTableColumnFlags.WidthFixed, 70);
                                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
                                ImGui.TableSetupColumn("Packets", ImGuiTableColumnFlags.WidthFixed, 90);
                                ImGui.TableSetupColumn("PhotonLike", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 95);
                                ImGui.TableSetupColumn("Adapter");
                                ImGui.TableHeadersRow();

                                DateTime nowUtc = DateTime.UtcNow;
                                foreach (var p in ports.Take(150))
                                {
                                    bool active = (nowUtc - p.LastSeen).TotalSeconds <= 3;
                                    string status = active ? "ACTIVE" : "IDLE";
                                    uint statusCol = active ? 0xFF5CFF5C : 0xFFAAAAAA;

                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(0); ImGui.Text(p.Port.ToString());
                                    ImGui.TableSetColumnIndex(1); ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(statusCol), status);
                                    ImGui.TableSetColumnIndex(2); ImGui.Text(p.PacketCount.ToString());
                                    ImGui.TableSetColumnIndex(3); ImGui.Text(p.PhotonLikeCount.ToString());
                                    ImGui.TableSetColumnIndex(4); ImGui.Text($"{(nowUtc - p.LastSeen).TotalSeconds:F1}s");
                                    ImGui.TableSetColumnIndex(5); ImGui.Text(p.LastAdapter ?? "");
                                }

                                ImGui.EndTable();
                            }

                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 6 [Console]
                        // ========================================================
                        // --- UI CONSOLE (LOGLAR VE RAW DUMP) ---
                        // ========================================================
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabConsole") ?? "Console"))
                        {
                            ImGui.Spacing();
                            // --- OTOMATİK TARAMA TİKİ ---
                            ImGui.Checkbox("RAW Search", ref AlbionOverlay._autoRawDump);




                            if (_autoRawDump)
                            {
                                ImGui.SameLine();

                                if ((DateTime.Now - _lastAutoRawDumpTime).TotalSeconds >= 0.5)
                                {
                                    _lastAutoRawDumpTime = DateTime.Now;
                                    lock (_dataLock)
                                    {
                                        /*
                                        // 1. Mobları Yazdır
                                        foreach (var m in _mobBuffer)
                                        {
                                            AddUIConsoleLog($"[Mob] ID: {m.TypeId} | Name: {m.Name} | X:{m.CurrentLerpedX:F1} Y:{m.CurrentLerpedY:F1}");
                                        }
                                        // 2. Oyuncuları Yazdır
                                        foreach (var p in _playersBuffer)
                                        {
                                            AddUIConsoleLog($"[Player] ID: {p.Id} | Name: {p.Name} | X:{p.CurrentLerpedX:F1} Y:{p.CurrentLerpedY:F1}");
                                        }
                                        */

                                        // 1. Kendini Yazdır (Self)
                                        var self = _gameStateManager?.GetPlayer();
                                        if (self != null)
                                        {
                                            AddUIConsoleLog(
                                                $"[Self] ID: {self.Id} | Name: {self.Name} | " +
                                                $"X:{self.CurrentLerpedX:F1} Y:{self.CurrentLerpedY:F1} | " +
                                                $"PX:{self.PositionX:F1} PY:{self.PositionY:F1}");
                                        }

                                        // 2. Diğer oyuncuları yazdır (ilk 20, mesafe ile)
                                        int playerDumpLimit = 20;
                                        int playerTotal = _playersBuffer.Count;
                                        AddUIConsoleLog($"[Players] Total: {playerTotal}");

                                        foreach (var p in _playersBuffer.Take(playerDumpLimit))
                                        {
                                            float dist = 0f;
                                            if (self != null)
                                            {
                                                float dx = p.CurrentLerpedX - self.CurrentLerpedX;
                                                float dy = p.CurrentLerpedY - self.CurrentLerpedY;
                                                dist = MathF.Sqrt(dx * dx + dy * dy);
                                            }

                                            AddUIConsoleLog(
                                                $"[Player] ID:{p.Id} | Name:{p.Name} | " +
                                                $"X:{p.CurrentLerpedX:F1} Y:{p.CurrentLerpedY:F1} | Dist:{dist:F1}m");
                                        }



                                    }
                                }
                            }




                            ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                            // 3. KONSOLU SADECE 1 KERE ÇİZ
                            UIConsole.DrawConsoleWindow();

                            ImGui.EndTabItem();
                        }
                        #endregion
                        #region Sekme 7 [Parser]
                        if (ImGui.BeginTabItem("Parser"))
                        {
                            ImGui.Spacing();
                            ImGui.TextColored(new Vector4(0.35f, 0.95f, 1f, 1f), "Kisi Bazli Ham Parser Verisi");
                            if (ImGui.Button("Tum parser verisini TXT'ye cikar", new Vector2(280, 28)))
                            {
                                _lastParserDumpPath = PlayerParserTraceStore.DumpAllToFile();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Maps/Jobs Test", new Vector2(160, 28)))
                            {
                                _lastParserDumpPath = PlayerParserTraceStore.DumpMapJobsTestToFile();
                            }

                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0.6f, 1f, 0.9f, 1f), "Parser Profiles");
                            if (ImGui.Button("Movement Debug", new Vector2(130, 24)))
                            {
                                _parserActiveProfile = "Movement Debug";
                                _parserEventFilter = "Move";
                                _parserPayloadFilter = "";
                                _parserOnlyNearby = false;
                                _parserDiffOnlyChanged = true;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Map/Jobs Debug", new Vector2(130, 24)))
                            {
                                _parserActiveProfile = "Map/Jobs Debug";
                                _parserEventFilter = "Join|Leave|Cluster|Map|REQ:2|RES:PlayerJoiningMap";
                                _parserPayloadFilter = "";
                                _parserOnlyNearby = false;
                                _parserDiffOnlyChanged = true;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Resource Debug", new Vector2(130, 24)))
                            {
                                _parserActiveProfile = "Resource Debug";
                                _parserEventFilter = "Mob|Harvest|NewMob|Harvestable";
                                _parserPayloadFilter = "";
                                _parserOnlyNearby = true;
                                _parserDiffOnlyChanged = true;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Reset Profile", new Vector2(110, 24)))
                            {
                                _parserActiveProfile = "Custom";
                                _parserEventFilter = "";
                                _parserPayloadFilter = "";
                            }
                            ImGui.TextDisabled($"Active: {_parserActiveProfile}");

                            if (!string.IsNullOrWhiteSpace(_lastParserDumpPath))
                            {
                                ImGui.SameLine();
                                ImGui.TextWrapped($"Kaydedildi: {_lastParserDumpPath}");
                            }

                            ImGui.Checkbox("Sadece cevremdekiler", ref _parserOnlyNearby);
                            ImGui.SameLine();
                            ImGui.Text("Filter:");
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(220);
                            ImGui.InputText("##ParserPlayerFilter", ref _parserPlayerFilter, 64);

                            ImGui.Text("Event Filter (isim veya code):");
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(220);
                            ImGui.InputText("##ParserEventFilter", ref _parserEventFilter, 64);

                            ImGui.Text("Payload Filter:");
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(220);
                            ImGui.InputText("##ParserPayloadFilter", ref _parserPayloadFilter, 64);

                            var nearbyEntityMap = new Dictionary<int, string>();

                            lock (_dataLock)
                            {
                                foreach (var p in _playersBuffer)
                                {
                                    if (p.Id <= 0) continue;
                                    nearbyEntityMap[p.Id] = p.Name ?? string.Empty;
                                }

                                foreach (var m in _mobBuffer)
                                {
                                    if (m.Id <= 0) continue;
                                    if (nearbyEntityMap.ContainsKey(m.Id)) continue;

                                    string mobName = (_mobDatabase.TryGetValue(m.TypeId, out var info) && !string.IsNullOrWhiteSpace(info.Name))
                                        ? info.Name
                                        : (!string.IsNullOrWhiteSpace(m.Name)
                                            ? CleanName(m.Name)
                                            : $"MobType:{m.TypeId}");

                                    if (string.IsNullOrWhiteSpace(mobName))
                                        mobName = $"MobType:{m.TypeId}";

                                    nearbyEntityMap[m.Id] = $"[M] {mobName}";
                                }
                            }

                            var nearbyPlayers = nearbyEntityMap
                                .Select(x => (id: x.Key, name: x.Value))
                                .ToList();

                            var allKnown = PlayerParserTraceStore.GetKnownPlayersSnapshot(nearbyPlayers);
                            IEnumerable<(int id, string name)> source = allKnown;

                            if (_parserOnlyNearby)
                            {
                                var nearbySet = new HashSet<int>(nearbyPlayers.Select(x => x.id));
                                source = source.Where(x => nearbySet.Contains(x.id));
                            }

                            if (!string.IsNullOrWhiteSpace(_parserPlayerFilter))
                            {
                                string q = _parserPlayerFilter.Trim();
                                source = source.Where(x =>
                                    x.name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                    x.id.ToString().Contains(q));
                            }

                            var playerList = source
                                .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            ImGui.Separator();
                            if (ImGui.BeginChild("ParserPlayers", new Vector2(320, 0), ImGuiChildFlags.Borders))
                            {
                                if (playerList.Count == 0)
                                {
                                    ImGui.TextDisabled("Oyuncu bulunamadi.");
                                }
                                else
                                {
                                    if (_parserMobRenameTargetId > 0)
                                    {
                                        ImGui.TextColored(new Vector4(0.75f, 0.95f, 1f, 1f), $"Mob Isim Testi | ID: {_parserMobRenameTargetId}");
                                        ImGui.SetNextItemWidth(210f);
                                        ImGui.InputText("##ParserMobRenameInput", ref _parserMobRenameInput, 96);
                                        ImGui.SameLine();
                                        if (ImGui.SmallButton("Kaydet##ParserMobRenameSave"))
                                        {
                                            if (string.IsNullOrWhiteSpace(_parserMobRenameInput))
                                                _parserMobNameOverrides.Remove(_parserMobRenameTargetId);
                                            else
                                                _parserMobNameOverrides[_parserMobRenameTargetId] = _parserMobRenameInput.Trim();
                                        }
                                        ImGui.SameLine();
                                        if (ImGui.SmallButton("Temizle##ParserMobRenameClear"))
                                        {
                                            _parserMobNameOverrides.Remove(_parserMobRenameTargetId);
                                            _parserMobRenameInput = "";
                                        }
                                        ImGui.Separator();
                                    }

                                    foreach (var item in playerList)
                                    {
                                        bool selected = _parserSelectedPlayerId == item.id;
                                        bool checkbox = selected;
                                        string displayName = item.name;
                                        if (_parserMobNameOverrides.TryGetValue(item.id, out var overrideName) && !string.IsNullOrWhiteSpace(overrideName))
                                            displayName = $"[M] {overrideName}";

                                        string label = $"[{item.id}] {displayName}";
                                        if (ImGui.Checkbox(label, ref checkbox))
                                        {
                                            if (checkbox) _parserSelectedPlayerId = item.id;
                                            else if (_parserSelectedPlayerId == item.id) _parserSelectedPlayerId = -1;
                                        }

                                        if (displayName.StartsWith("[M]", StringComparison.Ordinal))
                                        {
                                            ImGui.SameLine();
                                            if (ImGui.SmallButton($"Isim##ParserRename{item.id}"))
                                            {
                                                _parserMobRenameTargetId = item.id;
                                                _parserMobRenameInput = _parserMobNameOverrides.TryGetValue(item.id, out var existing)
                                                    ? existing
                                                    : displayName.Replace("[M]", "").Trim();
                                            }
                                        }
                                    }
                                }
                            }
                            ImGui.EndChild();

                            ImGui.SameLine();

                            if (ImGui.BeginChild("ParserDump", new Vector2(0, 0), ImGuiChildFlags.Borders))
                            {
                                if (_parserSelectedPlayerId <= 0)
                                {
                                    ImGui.TextDisabled("Soldan bir oyuncu sec.");
                                }
                                else
                                {
                                    var entries = PlayerParserTraceStore.GetPlayerEntries(_parserSelectedPlayerId);

                                    if (!string.IsNullOrWhiteSpace(_parserEventFilter))
                                    {
                                        string ef = _parserEventFilter.Trim();
                                        entries = entries.Where(e =>
                                            e.eventName.Contains(ef, StringComparison.OrdinalIgnoreCase) ||
                                            e.eventCode.ToString().Contains(ef))
                                            .ToList();
                                    }

                                    if (!string.IsNullOrWhiteSpace(_parserPayloadFilter))
                                    {
                                        string pf = _parserPayloadFilter.Trim();
                                        entries = entries.Where(e =>
                                            e.payload.Contains(pf, StringComparison.OrdinalIgnoreCase))
                                            .ToList();
                                    }

                                    string selectedPlayerName = playerList.FirstOrDefault(x => x.id == _parserSelectedPlayerId).name;
                                    if (_parserMobNameOverrides.TryGetValue(_parserSelectedPlayerId, out var parserOverrideName) && !string.IsNullOrWhiteSpace(parserOverrideName))
                                        selectedPlayerName = $"[M] {parserOverrideName}";
                                    if (string.IsNullOrWhiteSpace(selectedPlayerName))
                                        selectedPlayerName = $"ID:{_parserSelectedPlayerId}";

                                    if (ImGui.Button("Secili Oyuncuyu Export Et"))
                                    {
                                        try
                                        {
                                            string safeName = string.IsNullOrWhiteSpace(selectedPlayerName)
                                                ? $"ID_{_parserSelectedPlayerId}"
                                                : string.Concat(selectedPlayerName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

                                            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "Parser");
                                            Directory.CreateDirectory(exportDir);
                                            string filePath = Path.Combine(exportDir, $"{safeName}_{_parserSelectedPlayerId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                                            var lines = new List<string>
                                            {
                                                $"Player: {selectedPlayerName} (ID: {_parserSelectedPlayerId})",
                                                $"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                                                $"Total Entries: {entries.Count}",
                                                new string('-', 80)
                                            };

                                            foreach (var e in entries)
                                            {
                                                lines.Add($"[{e.time:HH:mm:ss}] Event: {e.eventName} | Code: {e.eventCode}");
                                                lines.Add(e.payload);
                                                lines.Add(new string('-', 80));
                                            }

                                            File.WriteAllLines(filePath, lines);
                                            _parserExportStatus = $"Export OK: {filePath}";
                                        }
                                        catch (Exception ex)
                                        {
                                            _parserExportStatus = $"Export HATA: {ex.Message}";
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(_parserExportStatus))
                                    {
                                        ImGui.TextWrapped(_parserExportStatus);
                                    }

                                    if (entries.Count == 0)
                                    {
                                        ImGui.TextDisabled("Secili oyuncu icin parser kaydi yok.");
                                    }
                                    else
                                    {
                                        if (ImGui.Button("Snapshot A = Son Kayit"))
                                        {
                                            var last = entries[^1];
                                            _parserSnapshotAPayload = last.payload;
                                            _parserSnapshotALabel = $"A: [{last.time:HH:mm:ss}] {last.eventName} ({last.eventCode})";
                                        }
                                        ImGui.SameLine();
                                        if (ImGui.Button("Snapshot B = Son Kayit"))
                                        {
                                            var last = entries[^1];
                                            _parserSnapshotBPayload = last.payload;
                                            _parserSnapshotBLabel = $"B: [{last.time:HH:mm:ss}] {last.eventName} ({last.eventCode})";
                                        }

                                        ImGui.SameLine();
                                        if (ImGui.Button("A/B Kaydet"))
                                        {
                                            try
                                            {
                                                if (string.IsNullOrWhiteSpace(_parserSnapshotAPayload) || string.IsNullOrWhiteSpace(_parserSnapshotBPayload))
                                                {
                                                    _parserExportStatus = "Kayit icin once Snapshot A ve B sec.";
                                                }
                                                else
                                                {
                                                    string safeName = string.IsNullOrWhiteSpace(selectedPlayerName)
                                                        ? $"ID_{_parserSelectedPlayerId}"
                                                        : string.Concat(selectedPlayerName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

                                                    string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "Parser", "Snapshots");
                                                    Directory.CreateDirectory(exportDir);
                                                    string filePath = Path.Combine(exportDir, $"{safeName}_{_parserSelectedPlayerId}_AB_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                                                    var lines = new List<string>
                                                    {
                                                        $"Player: {selectedPlayerName} (ID: {_parserSelectedPlayerId})",
                                                        $"Saved At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                                                        _parserSnapshotALabel,
                                                        _parserSnapshotBLabel,
                                                        new string('=', 90),
                                                        "SNAPSHOT A PAYLOAD:",
                                                        _parserSnapshotAPayload,
                                                        new string('-', 90),
                                                        "SNAPSHOT B PAYLOAD:",
                                                        _parserSnapshotBPayload
                                                    };

                                                    File.WriteAllLines(filePath, lines);
                                                    _parserExportStatus = $"A/B Kaydedildi: {filePath}";
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _parserExportStatus = $"A/B Kayit HATA: {ex.Message}";
                                            }
                                        }

                                        ImGui.SameLine();
                                        ImGui.Checkbox("Sadece degisenler", ref _parserDiffOnlyChanged);

                                        ImGui.TextColored(new Vector4(0.75f, 0.9f, 1f, 1f), _parserSnapshotALabel);
                                        ImGui.TextColored(new Vector4(1f, 0.8f, 0.65f, 1f), _parserSnapshotBLabel);

                                        ImGui.Text("Field Filter (key veya value):");
                                        ImGui.SameLine();
                                        ImGui.SetNextItemWidth(220);
                                        ImGui.InputText("##ParserNewCharFieldFilter", ref _parserNewCharFieldFilter, 64);

                                        if (!string.IsNullOrWhiteSpace(_parserSnapshotAPayload) && !string.IsNullOrWhiteSpace(_parserSnapshotBPayload))
                                        {
                                            var mapA = ParsePayloadToMap(_parserSnapshotAPayload);
                                            var mapB = ParsePayloadToMap(_parserSnapshotBPayload);

                                            var keys = new HashSet<int>(mapA.Keys);
                                            keys.UnionWith(mapB.Keys);

                                            var diffRows = new List<(int key, string a, string b, bool changed)>();
                                            foreach (var key in keys.OrderBy(k => k))
                                            {
                                                mapA.TryGetValue(key, out string? aVal);
                                                mapB.TryGetValue(key, out string? bVal);
                                                aVal ??= "(yok)";
                                                bVal ??= "(yok)";
                                                bool changed = !string.Equals(aVal, bVal, StringComparison.Ordinal);
                                                if (_parserDiffOnlyChanged && !changed) continue;

                                                if (!string.IsNullOrWhiteSpace(_parserNewCharFieldFilter))
                                                {
                                                    string fq = _parserNewCharFieldFilter.Trim();
                                                    bool match = key.ToString().Contains(fq, StringComparison.OrdinalIgnoreCase)
                                                                 || aVal.Contains(fq, StringComparison.OrdinalIgnoreCase)
                                                                 || bVal.Contains(fq, StringComparison.OrdinalIgnoreCase);
                                                    if (!match) continue;
                                                }

                                                diffRows.Add((key, aVal, bVal, changed));
                                            }

                                            ImGui.TextColored(new Vector4(0.95f, 0.95f, 0.6f, 1f), $"Field Diff Row: {diffRows.Count}");
                                            if (ImGui.BeginChild("ParserFieldDiff", new Vector2(0, 220), ImGuiChildFlags.Borders))
                                            {
                                                foreach (var row in diffRows)
                                                {
                                                    var col = row.changed
                                                        ? new Vector4(1f, 0.45f, 0.45f, 1f)
                                                        : new Vector4(0.65f, 0.9f, 0.65f, 1f);
                                                    ImGui.TextColored(col, $"[{row.key}] A={row.a} | B={row.b}");
                                                }

                                                var addedKeys = diffRows.Where(r => r.a == "(yok)" && r.b != "(yok)").Select(r => r.key).OrderBy(x => x).ToList();
                                                var removedKeys = diffRows.Where(r => r.a != "(yok)" && r.b == "(yok)").Select(r => r.key).OrderBy(x => x).ToList();
                                                var changedKeys = diffRows.Where(r => r.changed && r.a != "(yok)" && r.b != "(yok)").Select(r => r.key).OrderBy(x => x).ToList();

                                                ImGui.Separator();
                                                ImGui.TextColored(new Vector4(0.75f, 0.95f, 1f, 1f), "A/B Summary");
                                                ImGui.TextWrapped($"Added: {(addedKeys.Count == 0 ? "-" : string.Join(",", addedKeys))}");
                                                ImGui.TextWrapped($"Removed: {(removedKeys.Count == 0 ? "-" : string.Join(",", removedKeys))}");
                                                ImGui.TextWrapped($"Changed: {(changedKeys.Count == 0 ? "-" : string.Join(",", changedKeys))}");

                                                bool mapLike = diffRows.Any(r => (r.a + " " + r.b).Contains("@MISTS@", StringComparison.OrdinalIgnoreCase)
                                                                                  || (r.a + " " + r.b).Contains("@HIDEOUT@", StringComparison.OrdinalIgnoreCase)
                                                                                  || (r.a + " " + r.b).Contains("CLUSTER", StringComparison.OrdinalIgnoreCase)
                                                                                  || (r.a + " " + r.b).Contains("MAP", StringComparison.OrdinalIgnoreCase));
                                                bool movementLike = changedKeys.Any(k => k == 1 || k == 3 || k == 4 || k == 5 || k == 19 || k == 25);
                                                string impact = mapLike ? "Impact: Map/Jobs" : (movementLike ? "Impact: Movement" : "Impact: General");
                                                ImGui.TextColored(new Vector4(1f, 0.9f, 0.6f, 1f), impact);
                                            }
                                            ImGui.EndChild();

                                            if (mapA.TryGetValue(1, out var aByteVal) && mapB.TryGetValue(1, out var bByteVal)
                                                && aByteVal.StartsWith("byte[", StringComparison.OrdinalIgnoreCase)
                                                && bByteVal.StartsWith("byte[", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var ba = ParseByteArrayFromValueString(aByteVal);
                                                var bb = ParseByteArrayFromValueString(bByteVal);

                                                if (ba.Length >= 8 && bb.Length >= 8)
                                                {
                                                    int maxOffset = Math.Min(ba.Length, bb.Length) - 4;

                                                    ImGui.Separator();
                                                    ImGui.TextColored(new Vector4(0.55f, 1f, 0.85f, 1f), "byte[1] Int32 LE Decode (offset bazli)");

                                                    if (ImGui.Button("Decode byte[1] A/B"))
                                                    {
                                                        try
                                                        {
                                                            string selected = string.IsNullOrWhiteSpace(selectedPlayerName)
                                                                ? $"ID_{_parserSelectedPlayerId}"
                                                                : string.Concat(selectedPlayerName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                                                            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "Parser", "Decode");
                                                            Directory.CreateDirectory(exportDir);
                                                            string filePath = Path.Combine(exportDir, $"{selected}_{_parserSelectedPlayerId}_Byte1Decode_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                                                            var lines = new List<string>
                                                            {
                                                                $"Player: {selectedPlayerName} ({_parserSelectedPlayerId})",
                                                                _parserSnapshotALabel,
                                                                _parserSnapshotBLabel,
                                                                $"A byte count={ba.Length}, B byte count={bb.Length}",
                                                                new string('-', 90)
                                                            };

                                                            for (int off = 0; off <= maxOffset; off++)
                                                            {
                                                                int ia = ReadInt32LE(ba, off);
                                                                int ib = ReadInt32LE(bb, off);
                                                                long d = (long)ib - ia;
                                                                if (d == 0) continue;

                                                                lines.Add($"off={off:D2} | A={ia} | B={ib} | d={d} | /1e6 A={ia / 1_000_000.0:F6} B={ib / 1_000_000.0:F6}");
                                                            }

                                                            File.WriteAllLines(filePath, lines);
                                                            _parserByteDecodeStatus = $"Decode kaydedildi: {filePath}";
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            _parserByteDecodeStatus = $"Decode HATA: {ex.Message}";
                                                        }
                                                    }

                                                    if (!string.IsNullOrWhiteSpace(_parserByteDecodeStatus))
                                                        ImGui.TextWrapped(_parserByteDecodeStatus);

                                                    if (ImGui.BeginChild("ParserByteDecodePreview", new Vector2(0, 140), ImGuiChildFlags.Borders))
                                                    {
                                                        int shown = 0;
                                                        for (int off = 0; off <= maxOffset; off++)
                                                        {
                                                            int ia = ReadInt32LE(ba, off);
                                                            int ib = ReadInt32LE(bb, off);
                                                            long d = (long)ib - ia;
                                                            if (d == 0) continue;

                                                            ImGui.Text($"off={off:D2} | A={ia} | B={ib} | d={d}");
                                                            shown++;
                                                            if (shown >= 24) break;
                                                        }
                                                        if (shown == 0) ImGui.TextDisabled("Degisen Int32 offset bulunamadi.");
                                                    }
                                                    ImGui.EndChild();
                                                }
                                            }
                                        }

                                        ImGui.TextColored(new Vector4(1f, 0.9f, 0.3f, 1f), $"Toplam Kayit: {entries.Count}");
                                        ImGui.Separator();

                                        // En yeni en altta kalacak şekilde sırala
                                        foreach (var entry in entries)
                                        {
                                            ImGui.TextColored(new Vector4(0.55f, 0.95f, 0.55f, 1f), $"[{entry.time:HH:mm:ss}] {entry.eventName} (Code: {entry.eventCode})");
                                            ImGui.TextWrapped(entry.payload);
                                            ImGui.Separator();
                                        }
                                    }
                                }
                            }
                            ImGui.EndChild();

                            ImGui.EndTabItem();
                        }
                        #endregion

                        ImGui.EndTabBar();

                    }
                    break;
                #endregion

            }
        }
        #endregion

        #region Dev Toolsdaki Kategoriler
        private static string NormalizeSearchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Trim().Replace('_', ' ').Replace('-', ' ');
            normalized = Regex.Replace(normalized, @"\s+", " ");
            return normalized.ToUpperInvariant();
        }

        private static bool NameMatchesSearch(string name, string normalizedQuery)
        {
            if (string.IsNullOrEmpty(normalizedQuery)) return true;
            return NormalizeSearchText(name).Contains(normalizedQuery, StringComparison.Ordinal);
        }

        private string GetMobCategory(string mobName, int tier)
        {
            string upper = mobName.ToUpperInvariant();

            if (upper.Contains("CRYSTAL") || upper.Contains("SPIDER"))
                return "Crystals";
            if (upper.Contains("DRONE") || upper.Contains("SNIFFER") ||
                upper.Contains("GRIFFIN") || upper.Contains("FEY") || upper.Contains("FAIRY") ||
                upper.Contains("VEILWEAVER") || upper.Contains("WEAVER"))
                return "Sniffer";
            if (upper.Contains("BOSS") || upper.Contains("TITAN") || upper.Contains("ANCIENT") ||
                upper.Contains("OLD_WHITE") || upper.Contains("MAMMOTH"))
                return "Boss";
            if (upper.Contains("VETERAN") || upper.Contains("CHAMPION") || upper.Contains("ASPECT"))
                return "Miniboss";
            return "Mob";
        }
        #endregion

        #region Show Hide Button Settings
        private int GetPressedKey()
        {
            // TÃ¼m sanal tuÅŸ kodlarÄ±nÄ± tara (Mouse butonlarÄ± hariÃ§ genelde 0x08'den baÅŸlar)
            for (int i = 0x08; i <= 0xFF; i++)
            {
                // Mevcut toggle tuÅŸunu algÄ±lamamasÄ± iÃ§in kontrol (opsiyonel) veya direkt algÄ±la
                if ((GetAsyncKeyState(i) & 0x8000) != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private string GetKeyName(int key)
        {
            // Basit bir eÅŸleÅŸtirme (Daha fazlasÄ± eklenebilir)
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x6F); // F1-F24
            if (key == 0x1B) return "ESC";
            if (key == 0x2D) return "INSERT";
            if (key == 0x2E) return "DELETE";
            if (key == 0x24) return "HOME";
            if (key == 0x23) return "END";
            if (key == 0x21) return "PG UP";
            if (key == 0x22) return "PG DOWN";
            if (key >= 0x30 && key <= 0x39) return ((char)key).ToString(); // 0-9
            if (key >= 0x41 && key <= 0x5A) return ((char)key).ToString(); // A-Z
            return "KEY " + key;
        }

        private void ImportWhitelistByGuildAlliance(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            lock (_dataLock)
            {
                var seed = _playersBuffer.FirstOrDefault(p => string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));
                if (seed == null) return;

                if (_whitelistImportSameGuild && !string.IsNullOrWhiteSpace(seed.Guild))
                {
                    foreach (var p in _playersBuffer)
                    {
                        if (string.Equals(p.Guild, seed.Guild, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Name))
                            _whitelist.Add(p.Name);
                    }
                }

                if (_whitelistImportSameAlliance && !string.IsNullOrWhiteSpace(seed.Alliance))
                {
                    foreach (var p in _playersBuffer)
                    {
                        if (string.Equals(p.Alliance, seed.Alliance, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Name))
                            _whitelist.Add(p.Name);
                    }
                }
            }
        }
        #endregion

    }
}


