using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using ImGuiNET;
using AlbionDataHandlers;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Utils;
using AlbionDataHandlers.Mappers;
using ClickableTransparentOverlay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        private int _devHighlightEntityId = -1;

        private void RenderDevToolsTab()
        {
                    if (ImGui.BeginTabBar("DevToolsTabs"))
                    {

                        #region Sekme 1 [Debug & Simulator]
                        // --- 1. SEKME: DEBUG & SIMULATOR ---
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabDebug") ?? "Debug"))
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
                            ImGui.Checkbox(Lang.Get("Dev_ConsoleLog") ?? "Log", ref _debugConsoleLog);
                            ImGui.Checkbox(Lang.Get("Dev_MobID") ?? "Mob ID", ref _debugMobs);
                            ImGui.Checkbox(Lang.Get("Dev_ResID") ?? "Res ID", ref _debugStaticResources);
                            ImGui.PopStyleVar();
                            ImGui.Separator();
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), Lang.Get("Dev_SimTitle") ?? "Sim");

                            if (ImGui.CollapsingHeader(Lang.Get("Dev_MobHeader") ?? "Mob Sim"))
                            {
                                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 2));
                                ImGui.Text(Lang.Get("Dev_MobSearch") ?? "Search");
                                ImGui.SetNextItemWidth(-1);
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
                                        if (ImGui.TreeNodeEx($"{cat} ({catMatches.Count})##Sim{cat}", ImGuiTreeNodeFlags.None))
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
                                ImGui.PopStyleVar();

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
                                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 2));
                                ImGui.Text(Lang.Get("Dev_ResSearch") ?? "Search");
                                ImGui.SetNextItemWidth(-1);
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
                                ImGui.PopStyleVar();

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
                        // --- 2. SEKME: MOBS (DB VE TAKİP) ---
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

                                        if (ImGui.TreeNodeEx($"{cat} ({mobsInCat.Count})", ImGuiTreeNodeFlags.None))
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
                                        if (ImGui.TreeNodeEx($"{cat} ({catMatches.Count})", ImGuiTreeNodeFlags.None))
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

                        // --- DEVELOPER TABS (Hidden unless Developer == 1) ---
                        #region Developer Tabs
                        if (_developer == 1)
                        {
                            if (ImGui.BeginTabItem("Developer"))
                            {
                                ImGui.Spacing();
                                if (ImGui.TreeNodeEx("Development & Debugging", ImGuiTreeNodeFlags.None))
                                {
                                    ImGui.Spacing();
                                    if (ImGui.Button("Diagnostic: ALL (Stres Testi)", new Vector2(-1, 30)))
                                    {
                                        Nightwatch.UserControls.AlbionOverlay.Map.NetworkPacketDiagnostic.RunDiagnostic(_gameStateManager, _mobDatabase, "All");
                                    }
                                    if (ImGui.Button("Diagnostic: Sadece Bosslar", new Vector2(-1, 30)))
                                    {
                                        Nightwatch.UserControls.AlbionOverlay.Map.NetworkPacketDiagnostic.RunDiagnostic(_gameStateManager, _mobDatabase, "Bosses");
                                    }
                                    if (ImGui.Button("Diagnostic: Sadece Sandiklar", new Vector2(-1, 30)))
                                    {
                                        Nightwatch.UserControls.AlbionOverlay.Map.NetworkPacketDiagnostic.RunDiagnostic(_gameStateManager, _mobDatabase, "Chests");
                                    }
                                    if (ImGui.Button("Diagnostic: Sadece Mistler", new Vector2(-1, 30)))
                                    {
                                        Nightwatch.UserControls.AlbionOverlay.Map.NetworkPacketDiagnostic.RunDiagnostic(_gameStateManager, _mobDatabase, "Mists");
                                    }
                                    ImGui.Spacing();
                                    ImGui.TreePop();
                                }
                                ImGui.Separator();
                                ImGui.Spacing();

                                if (ImGui.TreeNodeEx("SimTest", ImGuiTreeNodeFlags.None))
                                {
                                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), "Simulated Player Position");
                                var simPlayer = _gameStateManager.GetPlayer();
                                if (simPlayer != null)
                                {
                                    float simX = simPlayer.PositionX;
                                    float simY = simPlayer.PositionY;
                                    ImGui.Text($"Current X: {simX:F1} | Y: {simY:F1}");
                                    
                                    if (ImGui.Button(" < X (-10)", new Vector2(80, 30))) _gameStateManager.MoveLocalPlayer(-10f, 0f);
                                    ImGui.SameLine();
                                    if (ImGui.Button(" X (+10) > ", new Vector2(80, 30))) _gameStateManager.MoveLocalPlayer(10f, 0f);
                                    
                                    ImGui.SameLine();
                                    ImGui.Text("   ||   ");
                                    ImGui.SameLine();
                                    
                                    if (ImGui.Button(" v Y (-10)", new Vector2(80, 30))) _gameStateManager.MoveLocalPlayer(0f, -10f);
                                    ImGui.SameLine();
                                    if (ImGui.Button(" Y (+10) ^ ", new Vector2(80, 30))) _gameStateManager.MoveLocalPlayer(0f, 10f);
                                    
                                    ImGui.Spacing();
                                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Oyunda hareket ettiginizde bu koordinatlar gercegiyle ezilir.");
                                }
                                else
                                {
                                    ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Player nesnesi yok. Oyuna girmeniz lazim.");
                                }
                                ImGui.TreePop();
                                }
                                ImGui.Spacing();
                                ImGui.Separator();

                                if (ImGui.TreeNode("BetaTest"))
                                {
                                    ImGui.Checkbox("Resource Density Heatmap", ref _enableBetaHeatmap);
                                    ImGui.Checkbox(Lang.Get("Beta_Tracks") ?? "Tracks (556)", ref _showBetaTracks);
                                    ImGui.Checkbox(Lang.Get("Beta_Wisps") ?? "Wisps/Caged (530)", ref _showBetaWisps);
                                    ImGui.Checkbox(Lang.Get("Beta_Indicators") ?? "Spell Indicators (542)", ref _showBetaIndicators);
                                    ImGui.Checkbox(Lang.Get("Beta_Structures") ?? "Fortifications (583/584)", ref _showBetaStructures);
                                    ImGui.Checkbox("Locked Mists Portals (518/529)", ref _showBetaChests);

                                    // Sıradaki kilitli portal bilgisi
                                    var nextChest = _mobViewModels
                                        .Where(x => x.TypeId == 51800 && (x.RawMob?.UnlockTicks ?? 0) > DateTime.UtcNow.Ticks)
                                        .OrderBy(x => x.RawMob?.UnlockTicks ?? 0)
                                        .FirstOrDefault();

                                    if (nextChest != null)
                                    {
                                        long currentTicks = DateTime.UtcNow.Ticks;
                                        double remainingSeconds = (double)((nextChest.RawMob?.UnlockTicks ?? 0) - currentTicks) / 10000000.0;
                                        int rarity = nextChest.RawMob?.Rarity ?? 0;
                                        string rarityStr = rarity switch
                                        {
                                            0 => "Common",
                                            1 => "Uncommon",
                                            2 => "Rare",
                                            3 => "Epic",
                                            4 => "Legendary",
                                            _ => "Common"
                                        };

                                        Vector4 textColor = rarity switch
                                        {
                                            0 => new Vector4(1f, 1f, 1f, 1f),
                                            1 => new Vector4(0f, 1f, 0f, 1f),
                                            2 => new Vector4(0f, 0.6f, 1f, 1f),
                                            3 => new Vector4(1f, 0f, 1f, 1f),
                                            4 => new Vector4(1f, 0.8f, 0f, 1f),
                                            _ => new Vector4(1f, 1f, 1f, 1f)
                                        };

                                        ImGui.Indent(20f);
                                        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "Sıradaki Portal Bilgisi:");
                                        ImGui.TextColored(textColor, $"  Nadirliği: {rarityStr}");
                                        ImGui.TextColored(new Vector4(1f, 0.9f, 0.3f, 1f), $"  Açılış Süresi: {remainingSeconds:F0}s kaldı");
                                        ImGui.Unindent(20f);
                                    }
                                    else
                                    {
                                        ImGui.Indent(20f);
                                        ImGui.TextDisabled("Aktif kilitli portal yok.");
                                        ImGui.Unindent(20f);
                                    }

                                    ImGui.TreePop();
                                }
                                ImGui.Separator();

                            // TreeNode: TestApp
                            if (ImGui.TreeNodeEx("TestApp", ImGuiTreeNodeFlags.None))
                            {
                                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Tüm entity tiplerini radara spawn et");
                                ImGui.TextDisabled("Her butona basınca ilgili tip oyuncunun etrafında spawn olur.");
                                ImGui.Separator();

                                var player = _gameStateManager.GetPlayer();
                                float px = player?.PositionX ?? 0f;
                                float py = player?.PositionY ?? 0f;
                                Random rng = new Random();

                                Func<float, (float x, float y)> rndPos = (float dist) =>
                                {
                                    float angle = (float)(rng.NextDouble() * Math.PI * 2);
                                    float d = dist + (float)(rng.NextDouble() * 5f);
                                    return (px + MathF.Cos(angle) * d, py + MathF.Sin(angle) * d);
                                };

                                Func<string, int> findTypeId = (string partialUniqueName) =>
                                {
                                    string upper = partialUniqueName.ToUpperInvariant();
                                    foreach (var kv in _mobDatabase)
                                    {
                                        var mapInfo = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(kv.Key);
                                        if (mapInfo?.UniqueName != null && mapInfo.UniqueName.ToUpperInvariant().Contains(upper))
                                            return kv.Key;
                                    }
                                    return -1;
                                };

                                ImGui.TextColored(new Vector4(0.4f, 1f, 0.6f, 1f), "— Moblar —");
                                float bw = 180f;

                                if (ImGui.Button("Crystal Spider##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("CRYSTALSPIDER_VETERAN_BOSS"); if (tid <= 0) tid = 9999; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Crystal Spider"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Crystal Cobra##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("CRYSTALCOBRA_VETERAN_BOSS"); if (tid <= 0) tid = 9998; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Crystal Cobra"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Fey Dragon##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("MISTS_FAIRYDRAGON"); if (tid <= 0) tid = 9997; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Fey Dragon"); }

                                if (ImGui.Button("Griffin##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("MISTS_GRIFFIN"); if (tid <= 0) tid = 9996; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Griffin"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Veil Weaver##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("VEILWEAVER"); if (tid <= 0) tid = 9995; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Veil Weaver"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Aspect Boss##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("ASPECT"); if (tid <= 0) tid = 9994; var p = rndPos(20f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Aspect of the Storm"); }

                                if (ImGui.Button("Normal Mob##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("HERETIC_MAGE_BOSS"); if (tid <= 0) tid = 9993; var p = rndPos(12f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Seeker of Flames"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Hidden Treasure##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("FOREST_CHEST"); if (tid <= 0) tid = 9992; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Hidden Treasure"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Treasure Drone##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("AVALON_TREASURE_MINION"); if (tid <= 0) tid = 9991; var p = rndPos(15f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Avalonian Treasure Drone"); }

                                if (ImGui.Button("Mist Wisp##TA", new Vector2(bw, 0)))
                                { int tid = findTypeId("MISTS_PORTAL_WISP"); if (tid <= 0) tid = 9990; var p = rndPos(18f); _gameStateManager.AddDebugMob(tid, p.x, p.y, "Will o' Wisp"); }
                                ImGui.SameLine();
                                if (ImGui.Button("Avalon Minion Chest##TA", new Vector2(bw, 0)))
                                { var p = rndPos(15f); _gameStateManager.AddDebugMob(910, p.x, p.y, "Avalon Minion Chest"); }

                                ImGui.Spacing();
                                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "— Kaynaklar (Harvestable) —");

                                if (ImGui.Button("Hidden Chest (800)##TA", new Vector2(bw, 0)))
                                { var p = rndPos(15f); _gameStateManager.AddDebugHarvestable(800, 6, 1, 1, 0, p.x, p.y); }
                                ImGui.SameLine();
                                if (ImGui.Button("Hidden Chest (2638)##TA", new Vector2(bw, 0)))
                                { var p = rndPos(15f); _gameStateManager.AddDebugHarvestable(2638, 6, 1, 1, 0, p.x, p.y); }
                                ImGui.SameLine();
                                if (ImGui.Button("Ore T6.1##TA", new Vector2(bw, 0)))
                                { var p = rndPos(12f); _gameStateManager.AddDebugHarvestable(1, 6, 5, 9, 1, p.x, p.y); }

                                if (ImGui.Button("Wood T7.2##TA", new Vector2(bw, 0)))
                                { var p = rndPos(12f); _gameStateManager.AddDebugHarvestable(6, 7, 3, 9, 2, p.x, p.y); }
                                ImGui.SameLine();
                                if (ImGui.Button("Hide T5.3##TA", new Vector2(bw, 0)))
                                { var p = rndPos(12f); _gameStateManager.AddDebugHarvestable(3, 5, 4, 9, 3, p.x, p.y); }
                                ImGui.SameLine();
                                if (ImGui.Button("Fiber T8.0##TA", new Vector2(bw, 0)))
                                { var p = rndPos(12f); _gameStateManager.AddDebugHarvestable(4, 8, 2, 9, 0, p.x, p.y); }

                                if (ImGui.Button("Rock T6.0##TA", new Vector2(bw, 0)))
                                { var p = rndPos(12f); _gameStateManager.AddDebugHarvestable(2, 6, 6, 9, 0, p.x, p.y); }

                                ImGui.Spacing();
                                ImGui.Separator();
                                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "— Toplu İşlemler —");

                                if (ImGui.Button("TÜMÜNÜ SPAWN ET##TA", new Vector2(-1, 35)))
                                {
                                    float baseAngle = 0f; float radius = 15f; int idx = 0;
                                    Action<int, string, float> spawnMobAt = (int typeId, string name, float r) =>
                                    { float a = baseAngle + idx * (MathF.PI * 2f / 14f); _gameStateManager.AddDebugMob(typeId, px + MathF.Cos(a) * r, py + MathF.Sin(a) * r, name); idx++; };
                                    Action<int, int, int, int, int, float> spawnResAt = (int typeId, int tier, int count, int cap, int ench, float r) =>
                                    { float a = baseAngle + idx * (MathF.PI * 2f / 14f); _gameStateManager.AddDebugHarvestable(typeId, tier, count, cap, ench, px + MathF.Cos(a) * r, py + MathF.Sin(a) * r); idx++; };

                                    int csId = findTypeId("CRYSTALSPIDER_VETERAN_BOSS"); if (csId <= 0) csId = 9999; spawnMobAt(csId, "Crystal Spider", radius);
                                    int ccId = findTypeId("CRYSTALCOBRA_VETERAN_BOSS"); if (ccId <= 0) ccId = 9998; spawnMobAt(ccId, "Crystal Cobra", radius);
                                    int fdId = findTypeId("MISTS_FAIRYDRAGON"); if (fdId <= 0) fdId = 9997; spawnMobAt(fdId, "Fey Dragon", radius);
                                    int grId = findTypeId("MISTS_GRIFFIN"); if (grId <= 0) grId = 9996; spawnMobAt(grId, "Griffin", radius);
                                    int vwId = findTypeId("VEILWEAVER"); if (vwId <= 0) vwId = 9995; spawnMobAt(vwId, "Veil Weaver", radius);
                                    int asId = findTypeId("ASPECT"); if (asId <= 0) asId = 9994; spawnMobAt(asId, "Aspect of the Storm", radius + 5f);
                                    int htId = findTypeId("FOREST_CHEST"); if (htId <= 0) htId = 9992; spawnMobAt(htId, "Hidden Treasure", radius);
                                    spawnMobAt(910, "Avalon Minion Chest", radius);
                                    int wpId = findTypeId("MISTS_PORTAL_WISP"); if (wpId <= 0) wpId = 9990; spawnMobAt(wpId, "Will o' Wisp", radius + 3f);
                                    spawnResAt(800, 6, 1, 1, 0, radius); spawnResAt(2638, 6, 1, 1, 0, radius);
                                    spawnResAt(1, 6, 5, 9, 1, radius); spawnResAt(6, 7, 3, 9, 2, radius); spawnResAt(3, 5, 4, 9, 3, radius);
                                }

                                if (ImGui.Button("TÜMÜNÜ TEMİZLE##TA", new Vector2(-1, 30)))
                                { _gameStateManager.ClearAllData(); }

                                ImGui.TreePop();
                            }
                            ImGui.Separator();
/*
                            // TreeNode 1: Player Decode
                            if (ImGui.TreeNodeEx("Player Decode", ImGuiTreeNodeFlags.None))
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
                                                e.eventName.Contains("Move", StringComparison.OrdinalIgnoreCase));

                                            if (last == default)
                                            {
                                                ImGui.TextDisabled($"[{k.id}] {k.name} -> Move payload yok");
                                                continue;
                                            }

                                            var cands = DecodeCandidatesFromPayload(
                                                last.payload,
                                                true, true, true, true, true,
                                                true, true, true, true, true,
                                                true, true, true, true, true);

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

                                ImGui.TreePop();
                            }

                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();

                            // TreeNode 1: Player Decode
                            if (ImGui.TreeNodeEx("Player Decode", ImGuiTreeNodeFlags.None))
                            {
                                ImGui.Spacing();
                                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Player nesnesi yok. Oyuna girmeniz lazim.");
                                ImGui.TreePop();
                            }

                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();
                            */
                            // TreeNode 2: Ports
                            if (ImGui.TreeNodeEx("Ports", ImGuiTreeNodeFlags.None))
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

                                ImGui.TreePop();
                            }

                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();

                            // TreeNode 3: Parser
                            if (ImGui.TreeNodeEx("Parser", ImGuiTreeNodeFlags.None))
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

                                        // --- RAW VERİYİ GÖSTEREN DETAYLI İSİM ---
                                        string dbName = (_mobDatabase.TryGetValue(m.TypeId, out var info) && !string.IsNullOrWhiteSpace(info.Name))
                                            ? info.Name : "";
                                        string rawName = m.Name ?? "";
                                        string cleanedRaw = !string.IsNullOrWhiteSpace(rawName) ? CleanName(rawName) : "";

                                        // MobType etiketini belirle
                                        string typeTag;
                                        bool isLiving = m.Type == AlbionDataHandlers.Enums.MobTypes.LivingHarvestable ||
                                                        m.Type == AlbionDataHandlers.Enums.MobTypes.LivingSkinnable;
                                        if (isLiving)
                                            typeTag = "LR"; // Living Resource
                                        else if (m.Type == AlbionDataHandlers.Enums.MobTypes.Boss || m.Type == AlbionDataHandlers.Enums.MobTypes.MistBoss)
                                            typeTag = "BOSS";
                                        else if (m.Type == AlbionDataHandlers.Enums.MobTypes.MiniBoss)
                                            typeTag = "MINI";
                                        else if (m.Type == AlbionDataHandlers.Enums.MobTypes.Treasure)
                                            typeTag = "CHEST";
                                        else if (m.Type == AlbionDataHandlers.Enums.MobTypes.Drone)
                                            typeTag = "DRONE";
                                        else
                                            typeTag = "MOB";

                                        // Gösterilecek isim: Önce DB ismi, yoksa temizlenmiş raw name
                                        string bestName = !string.IsNullOrWhiteSpace(dbName) ? dbName
                                            : !string.IsNullOrWhiteSpace(cleanedRaw) ? cleanedRaw
                                            : $"TypeId:{m.TypeId}";

                                        // Tier + Enchant etiketi
                                        var typeInfo = AlbionDataHandlers.Mappers.MobMapper.Instance.GetMobInfo(m.TypeId);
                                        int dispTier = (int)(typeInfo?.Tier ?? 0);
                                        if (dispTier <= 0) dispTier = m.NetworkTier;
                                        string tierStr = dispTier > 0
                                            ? (m.EnchantmentLevel > 0 ? $"T{dispTier}.{m.EnchantmentLevel}" : $"T{dispTier}")
                                            : "";

                                        // Raw name bilgisi (parazit kısmı)
                                        string rawSuffix = !string.IsNullOrWhiteSpace(rawName) && rawName != bestName
                                            ? $" | raw:{rawName}" : "";

                                        nearbyEntityMap[m.Id] = $"[{typeTag}:{m.TypeId}] {tierStr} {bestName}{rawSuffix}";
                                    }

                                    // --- HARVESTABLE'LARI DA EKLE ---
                                    _harvestBuffer.Clear();
                                    _gameStateManager.GetHarvestables(_harvestBuffer);
                                    foreach (var h in _harvestBuffer)
                                    {
                                        if (h.Id <= 0) continue;
                                        if (nearbyEntityMap.ContainsKey(h.Id)) continue;

                                        var cat = GetCategoryFromTypeId(h.Type);
                                        string tierStr = h.EnchantmentLevel > 0
                                            ? $"T{h.Tier}.{h.EnchantmentLevel}" : $"T{h.Tier}";
                                        nearbyEntityMap[h.Id] = $"[RES:{h.Type}] {tierStr} {cat} ({h.Count}/{h.Capacity})";
                                    }

                                    // --- DUNGEON'LARI DA EKLE ---
                                    _dungeonBuffer.Clear();
                                    _gameStateManager.GetDungeons(_dungeonBuffer);
                                    foreach (var d in _dungeonBuffer)
                                    {
                                        if (d.Id <= 0) continue;
                                        int dId = (int)d.Id;
                                        if (nearbyEntityMap.ContainsKey(dId)) continue;
                                        
                                        string typeStr = d.Type == "1" ? "SOLO" :
                                                         d.Type == "2" ? "GROUP" :
                                                         d.Type == "3" ? "CORRUPT" :
                                                         d.Type == "4" ? "HELLGATE" :
                                                         d.Type == "5" ? "SOLO_BOSS_LAIR" :
                                                         d.Type == "6" ? "GROUP_BOSS_LAIR" :
                                                         d.Type == "7" ? "MISTS" :
                                                         d.Type == "8" ? "AVALON" :
                                                         d.Type == "Exit" ? "EXIT" : "UNKNOWN";
                                        nearbyEntityMap[dId] = $"[DUNGEON:{typeStr}] Enchant:{d.EnchantmentLevel}";
                                    }
                                }

                                var nearbyPlayers = nearbyEntityMap
                                    .Select(x => (id: x.Key, name: x.Value))
                                    .ToList();

                                var allKnown = PlayerParserTraceStore.GetKnownPlayersSnapshot(nearbyPlayers);
                                IEnumerable<(int id, string name)> source = allKnown;

                                ImGui.Spacing();
                                if (ImGui.BeginTabBar("ParserTabs"))
                                {
                                    if (ImGui.BeginTabItem("Players")) { _parserSelectedTab = 0; ImGui.EndTabItem(); }
                                    if (ImGui.BeginTabItem("Mobs & Res")) { _parserSelectedTab = 1; ImGui.EndTabItem(); }
                                    if (ImGui.BeginTabItem("Dungeons")) { _parserSelectedTab = 2; ImGui.EndTabItem(); }
                                    if (ImGui.BeginTabItem("Global")) { _parserSelectedTab = 3; ImGui.EndTabItem(); }
                                    ImGui.EndTabBar();
                                }

                                source = source.Where(x => {
                                    bool isMob = x.name.StartsWith("[MOB:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[BOSS:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[MINI:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[CHEST:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[DRONE:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[RES:", StringComparison.Ordinal) || 
                                                 x.name.StartsWith("[LR:", StringComparison.Ordinal);
                                    bool isDungeon = x.name.StartsWith("[DUNGEON:", StringComparison.Ordinal);
                                    
                                    if (_parserSelectedTab == 1) return isMob;
                                    if (_parserSelectedTab == 2) return isDungeon;
                                    if (_parserSelectedTab == 3) return false;
                                    return !isMob && !isDungeon;
                                });

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
                                if (_parserSelectedTab != 3)
                                {
                                    if (ImGui.BeginChild("ParserPlayers", new Vector2(420, 450f), ImGuiChildFlags.Borders))
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

                                            // Entity türüne göre renk kodu
                                            bool isEntity = displayName.StartsWith("[", StringComparison.Ordinal);
                                            bool isLivingRes = displayName.StartsWith("[LR:", StringComparison.Ordinal);
                                            bool isBoss = displayName.StartsWith("[BOSS:", StringComparison.Ordinal) || displayName.StartsWith("[MINI:", StringComparison.Ordinal);
                                            bool isHarvestable = displayName.StartsWith("[RES:", StringComparison.Ordinal);
                                            bool isDrone = displayName.StartsWith("[DRONE:", StringComparison.Ordinal);
                                            bool isChest = displayName.StartsWith("[CHEST:", StringComparison.Ordinal);
                                            bool isMob = displayName.StartsWith("[MOB:", StringComparison.Ordinal);

                                            if (isLivingRes)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f)); // Turuncu
                                            else if (isBoss)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 1f, 1f)); // Mor
                                            else if (isHarvestable)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 1f, 1f)); // Cyan
                                            else if (isDrone)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.8f, 1f, 1f)); // Açık Mavi
                                            else if (isChest)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 0.4f, 1f)); // Sarı
                                            else if (isMob)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f)); // Kırmızı
                                            else if (isEntity)
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.8f, 1f)); // Gri
                                            else
                                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1f, 0.4f, 1f)); // Yeşil (Oyuncu)

                                            string label = $"[{item.id}] {displayName}";
                                            if (ImGui.Checkbox(label, ref checkbox))
                                            {
                                                if (checkbox) _parserSelectedPlayerId = item.id;
                                                else if (_parserSelectedPlayerId == item.id) _parserSelectedPlayerId = -1;
                                            }
                                            ImGui.PopStyleColor();

                                            // Mob/Entity ise isim değiştirme butonu göster
                                            if (isEntity && !isHarvestable)
                                            {
                                                ImGui.SameLine();
                                                if (ImGui.SmallButton($"Isim##ParserRename{item.id}"))
                                                {
                                                    _parserMobRenameTargetId = item.id;
                                                    // Tag'i temizleyip sadece isim kısmını al
                                                    string cleanDisplayName = displayName;
                                                    int closeBracket = cleanDisplayName.IndexOf(']');
                                                    if (closeBracket >= 0 && closeBracket + 1 < cleanDisplayName.Length)
                                                        cleanDisplayName = cleanDisplayName.Substring(closeBracket + 1).Trim();
                                                    _parserMobRenameInput = _parserMobNameOverrides.TryGetValue(item.id, out var existing)
                                                        ? existing
                                                        : cleanDisplayName;
                                                }
                                            }
                                        }
                                    }
                                }
                            } // Close the outer if (_parserSelectedTab != 3) block

                            if (_parserSelectedTab != 3)
                            {
                                ImGui.EndChild();
                                ImGui.SameLine();
                            }

                                if (ImGui.BeginChild("ParserDump", new Vector2(0, 450f), ImGuiChildFlags.Borders))
                                {
                                    if (_parserSelectedTab != 3 && _parserSelectedPlayerId <= 0)
                                    {
                                        ImGui.TextDisabled("Soldan bir oyuncu sec.");
                                    }
                                    else
                                    {
                                        var entries = _parserSelectedTab == 3 
                                            ? PlayerParserTraceStore.GetGlobalEntries() 
                                            : PlayerParserTraceStore.GetPlayerEntries(_parserSelectedPlayerId);

                                        if (!string.IsNullOrWhiteSpace(_parserEventFilter))
                                        {
                                            string ef = _parserEventFilter.Trim();
                                            if (ef.StartsWith("!"))
                                            {
                                                var parts = ef.Substring(1).Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                                                entries = entries.Where(e =>
                                                    !parts.Any(p => e.eventName.Contains(p, StringComparison.OrdinalIgnoreCase) || e.eventCode.ToString().Contains(p)))
                                                    .ToList();
                                            }
                                            else
                                            {
                                                var parts = ef.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                                                entries = entries.Where(e =>
                                                    parts.Any(p => e.eventName.Contains(p, StringComparison.OrdinalIgnoreCase) || e.eventCode.ToString().Contains(p)))
                                                    .ToList();
                                            }
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
                                                            _parserByteDecodeResults.Clear();
                                                            for (int off = 0; off <= maxOffset; off++)
                                                            {
                                                                int ia = BitConverter.ToInt32(ba, off);
                                                                int ib = BitConverter.ToInt32(bb, off);
                                                                _parserByteDecodeResults.Add((off, ia, ib));
                                                            }
                                                        }

                                                        if (ImGui.BeginChild("ParserByteDecodeResults", new Vector2(0, 120), ImGuiChildFlags.Borders))
                                                        {
                                                            int shown = 0;
                                                            foreach (var (off, ia, ib) in _parserByteDecodeResults)
                                                            {
                                                                int d = Math.Abs(ia - ib);
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

                                ImGui.TreePop();
                            }

                            ImGui.EndTabItem();
                        }

                        /*
                        #region Sekme 3 [Live Tracker]
                        if (ImGui.BeginTabItem("Canli Konumlar (Live)"))
                        {
                            ImGui.TextColored(new Vector4(0, 1, 1, 1), "Haritadaki Anlik Konumlar:");
                            ImGui.Separator();
                            
                            if (ImGui.BeginChild("TrackerList", new Vector2(0, 0), ImGuiChildFlags.Borders))
                            {
                                var mainPlayer = _gameStateManager?.GetPlayer();
                                if (mainPlayer != null)
                                {
                                    ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"[BENIM KONUMUM] X:{mainPlayer.PositionX:F1}, Y:{mainPlayer.PositionY:F1} ({mainPlayer.Name})");
                                    ImGui.Separator();
                                }

                                if (ImGui.Button("Vurguyu Kaldir")) _devHighlightEntityId = -1;
                                ImGui.Separator();

                                foreach (var p in _playersBuffer)
                                {
                                    if (ImGui.Button($"Vurgula##P{p.Id}")) _devHighlightEntityId = p.Id;
                                    ImGui.SameLine();
                                    ImGui.Text($"Player ID: {p.Id} -> Konum: X:{p.PositionX:F1}, Y:{p.PositionY:F1} ({p.Name})");
                                }
                                foreach (var m in _mobBuffer)
                                {
                                    if (ImGui.Button($"Vurgula##M{m.Id}")) _devHighlightEntityId = m.Id;
                                    ImGui.SameLine();
                                    ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), $"Mob ID: {m.Id} -> Konum: X:{m.PositionX:F1}, Y:{m.PositionY:F1} ({m.Name})");
                                }
                            }
                            ImGui.EndChild();
                            ImGui.EndTabItem();
                        }
                        #endregion
                        */

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
                                            ImGui.SameLine(ImGui.GetWindowWidth() - 140);

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
                                            ImGui.SameLine(ImGui.GetWindowWidth() - 140);

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
                            ImGui.Checkbox(Lang.Get("Dev_TrackerShowResIcon") ?? "Kaynak İkonunu Göster", ref _trackerShowResourceIcons);
                            ImGui.Checkbox(Lang.Get("Dev_TrackerVip") ?? "Vip", ref _trackerEnableVipMobs);
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
                                        if (ImGui.TreeNodeEx($"{cat} ({mobsInCat.Count})##TrkCat{cat}", ImGuiTreeNodeFlags.None))
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
                        #region Sekme 6 [Console]
                        // ========================================================
                        // --- UI CONSOLE (LOGLAR VE RAW DUMP) ---
                        // ========================================================
                        if (ImGui.BeginTabItem(Lang.Get("Dev_TabConsole") ?? "Console"))
                        {
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
                                                string.Format(Lang.Get("Console_SelfFormat") ?? "[Self] ID: {0} | Name: {1} | X:{2:F1} Y:{3:F1} | PX:{4:F1} PY:{5:F1}",
                                                self.Id, self.Name, self.CurrentLerpedX, self.CurrentLerpedY, self.PositionX, self.PositionY));
                                        }

                                        // 2. Diğer oyuncuları yazdır (ilk 20, mesafe ile)
                                        int playerDumpLimit = 20;
                                        int playerTotal = _playersBuffer.Count;
                                        AddUIConsoleLog(string.Format(Lang.Get("Console_PlayersTotal") ?? "[Players] Total: {0}", playerTotal));

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
                                                string.Format(Lang.Get("Console_PlayerFormat") ?? "[Player] ID:{0} | Name:{1} | X:{2:F1} Y:{3:F1} | Dist:{4:F1}m",
                                                p.Id, p.Name, p.CurrentLerpedX, p.CurrentLerpedY, dist));
                                        }



                                    }
                                }
                            }




                            ImGui.Separator();
                            // 3. KONSOLU SADECE 1 KERE ÇİZ
                            UIConsole.DrawConsoleWindow();

                            ImGui.EndTabItem();
                        }
                        #endregion

                        ImGui.EndTabBar();

                    }
        }

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

            if (upper.Contains("CRYSTAL") || upper.Contains("SPIDER") || upper.Contains("KRİSTAL") || upper.Contains("KRISTAL") || upper.Contains("ÖRÜMCEK"))
                return "Crystals";
            if (upper.Contains("DRONE") || upper.Contains("SNIFFER") ||
                upper.Contains("GRIFFIN") || upper.Contains("FEY") || upper.Contains("FAIRY") ||
                upper.Contains("VEILWEAVER") || upper.Contains("WEAVER"))
                return "Sniffer";
            if (upper.Contains("BOSS") || (upper.Contains("TITAN") && !upper.Contains("TITANIUM")) || upper.Contains("ANCIENT") ||
                upper.Contains("OLD_WHITE") || upper.Contains("MAMMOTH"))
                return "Boss";
            if (upper.Contains("VETERAN") || upper.Contains("CHAMPION") || upper.Contains("ASPECT"))
                return "Miniboss";
            return "Mob";
        }
    }
}



