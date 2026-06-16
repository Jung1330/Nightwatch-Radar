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
        private void RenderMobsTab()
        {
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Mob_Title") ?? "Mobs");
                    ImGui.Checkbox(Lang.Get("Mob_ShowNormal") ?? "Show Normal", ref _showNormalMobs);
                    ImGui.Checkbox(Lang.Get("Mob_ShowBoss") ?? "Show Bosses", ref _showBosses);
                    ImGui.Checkbox(Lang.Get("Mob_ShowMist") ?? "Show Mists", ref _showMists);
                    ImGui.Checkbox(Lang.Get("Mob_ShowNames") ?? "Show Names", ref _showMobNames);
                    string[] truthModes = { "Name First", "Network First", "Metadata First" };
                    ImGui.SetNextItemWidth(220);
                    ImGui.Combo(Lang.Get("Mob_ResourceTruthMode") ?? "Resource Truth Mode", ref _resourceTruthMode, truthModes, truthModes.Length);

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
        }
    }
}
