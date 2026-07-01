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
using Nightwatch.UserControls;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        private void RenderMobsTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Mob_Title") ?? "Mobs Settings", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("MobsSettings", 0);
            
            MentalityTheme.AnimatedToggle(Lang.Get("Mob_ShowNormal") ?? "Show Normal", ref _showNormalMobs);
            MentalityTheme.AnimatedToggle(Lang.Get("Mob_ShowBoss") ?? "Show Bosses", ref _showBosses);
            if (_showBosses)
            {
                ImGui.Indent();
                ImGui.SliderFloat("Boss Icon Size", ref _bossIconSize, 10, 100);
                ImGui.Unindent();
            }
            MentalityTheme.AnimatedToggle(Lang.Get("Mob_ShowHiddenChests") ?? "Show Hidden Chests", ref _showHiddenChests);
            MentalityTheme.AnimatedToggle(Lang.Get("Mob_ShowMist") ?? "Show Mists", ref _showMists);
            MentalityTheme.AnimatedToggle("Show Exits (Geçit Çıkışları)", ref _showExits);
            MentalityTheme.AnimatedToggle("Show Mist Cages (Sis Kafesleri)", ref _showWispCages);
            MentalityTheme.AnimatedToggle("Show Smugglers (Kaçakçılar)", ref _showSmugglers);
            MentalityTheme.AnimatedToggle(Lang.Get("Dungeon_ShowIcons") ?? "Dungeons Show With Icon", ref _showDungeonIcons);
            if (_showDungeonIcons)
            {
                ImGui.Indent();
                if (ImGui.TreeNodeEx(Lang.Get("Dungeon_Filters") ?? "Dungeon Filters", ImGuiTreeNodeFlags.None))
                {
                    if (ImGui.TreeNodeEx(Lang.Get("Dungeon_Solo") ?? "Solo Dungeons", ImGuiTreeNodeFlags.None))
                    {
                        MentalityTheme.AnimatedToggle(Lang.Get("Dungeon_ShowSolo") ?? "Show Solo", ref _showSoloDungeons);
                        if (_showSoloDungeons)
                        {
                            ImGui.Indent();
                            for (int i = 0; i <= 4; i++)
                                MentalityTheme.AnimatedToggle($"Enchantment {i}", ref _showSoloEnchantments[i]);
                            MentalityTheme.AnimatedToggle("Boss Lair", ref _showSoloBossLair);
                            ImGui.Unindent();
                        }
                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNodeEx(Lang.Get("Dungeon_Group") ?? "Group Dungeons", ImGuiTreeNodeFlags.None))
                    {
                        MentalityTheme.AnimatedToggle(Lang.Get("Dungeon_ShowGroup") ?? "Show Group", ref _showGroupDungeons);
                        if (_showGroupDungeons)
                        {
                            ImGui.Indent();
                            for (int i = 0; i <= 4; i++)
                                MentalityTheme.AnimatedToggle($"Enchantment {i}", ref _showGroupEnchantments[i]);
                            MentalityTheme.AnimatedToggle("Boss Lair", ref _showGroupBossLair);
                            ImGui.Unindent();
                        }
                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNodeEx(Lang.Get("Dungeon_Corrupted") ?? "Corrupted Dungeons", ImGuiTreeNodeFlags.None))
                    {
                        MentalityTheme.AnimatedToggle("Show Corrupted", ref _showCorruptedDungeons);
                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNodeEx(Lang.Get("Dungeon_Hellgate") ?? "Hellgates", ImGuiTreeNodeFlags.None))
                    {
                        MentalityTheme.AnimatedToggle("Show Hellgate", ref _showHellgateDungeons);
                        ImGui.TreePop();
                    }
                    if (ImGui.TreeNodeEx("Avalonian Dungeons", ImGuiTreeNodeFlags.None))
                    {
                        MentalityTheme.AnimatedToggle("Show Avalonian", ref _showAvalonianDungeons);
                        if (_showAvalonianDungeons)
                        {
                            ImGui.Indent();
                            for (int i = 4; i <= 8; i++) // Tiers 4 to 8
                                MentalityTheme.AnimatedToggle($"Tier {i}", ref _showAvalonianTiers[i]);
                            ImGui.Unindent();
                        }
                        ImGui.TreePop();
                    }
                    ImGui.TreePop();
                }
                ImGui.Unindent();
            }
            MentalityTheme.AnimatedToggle(Lang.Get("Mob_ShowNames") ?? "Show Names", ref _showMobNames);

            ImGui.Spacing();
            if (ImGui.TreeNodeEx("Trackers (İz Sürme)", ImGuiTreeNodeFlags.None))
            {
                MentalityTheme.AnimatedToggle("Show Tracks (İzleri Göster)", ref _showTrackers);
                if (_showTrackers)
                {
                    ImGui.Indent();
                    MentalityTheme.AnimatedToggle("Bear (Ayı)", ref _trackBear);
                    MentalityTheme.AnimatedToggle("Wolf (Kurt)", ref _trackWolf);
                    MentalityTheme.AnimatedToggle("Panther (Panter)", ref _trackPanther);
                    MentalityTheme.AnimatedToggle("Humanoid (İnsansı)", ref _trackHumanoid);
                    MentalityTheme.AnimatedToggle("Elemental (Elementel)", ref _trackElemental);
                    MentalityTheme.AnimatedToggle("Ent (Ağaç)", ref _trackEnt);
                    MentalityTheme.AnimatedToggle("Imp (İblis)", ref _trackImp);
                    MentalityTheme.AnimatedToggle("Golem", ref _trackGolem);
                    MentalityTheme.AnimatedToggle("Werewolf (Kurt Adam)", ref _trackWerewolf);
                    ImGui.Unindent();
                }
                ImGui.TreePop();
            }

            ImGui.Spacing();
            string[] truthModes = { "Name First", "Network First", "Metadata First" };
            ImGui.SetNextItemWidth(220);
            ImGui.Combo(Lang.Get("Mob_ResourceTruthMode") ?? "Resource Truth Mode", ref _resourceTruthMode, truthModes, truthModes.Length);
            
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Mob_BlacklistTitle") ?? "Mob Blacklist", MentalityTheme.Colors.AccentDanger);

            MentalityTheme.BeginCard("BlacklistSearch", 0);
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Mob_BlacklistSearch") ?? "Search:");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##Search", ref _blacklistSearchQuery, 64);
            ImGui.Spacing();

            if (ImGui.BeginChild("BlResults", new Vector2(0, 100), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
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

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
                    foreach (var m in blMatches)
                    {
                        if (ImGui.Selectable($"[{m.Key}] {m.Value.Name}", _selectedMobIdForBlacklist == m.Key))
                            _selectedMobIdForBlacklist = m.Key;
                    }
                    ImGui.PopStyleVar();
                }
            }
            ImGui.EndChild();
            
            ImGui.Spacing();
            if (MentalityTheme.Button(Lang.Get("Mob_BlacklistAdd") ?? "Add to Blacklist") && _selectedMobIdForBlacklist != -1)
            {
                _ignoredMobIds.Add(_selectedMobIdForBlacklist);
                _selectedMobIdForBlacklist = -1;
            }
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Mob_BlacklistHeader") ?? "Ignored Mobs", MentalityTheme.Colors.TextMuted);

            MentalityTheme.BeginCard("IgnoredMobsList", 100);
            if (ImGui.BeginChild("HiddenList", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
                int idToRemove = -1;
                foreach (var id in _ignoredMobIds)
                {
                    string mName = _mobDatabase.ContainsKey(id) ? _mobDatabase[id].Name : "ID:" + id;
                    ImGui.TextColored(MentalityTheme.Colors.AccentDanger, mName); 
                    
                    float avail = ImGui.GetContentRegionAvail().X;
                    if (avail > 80) ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                    else ImGui.SameLine();

                    if (MentalityTheme.SmallButton($"{Lang.Get("Mob_BlacklistRemove") ?? "Remove"}##{id}", MentalityTheme.Colors.Border)) idToRemove = id;
                }
                ImGui.PopStyleVar();
                if (idToRemove != -1) _ignoredMobIds.Remove(idToRemove);
            }
            ImGui.EndChild();
            MentalityTheme.EndCard();
        }
    }
}
