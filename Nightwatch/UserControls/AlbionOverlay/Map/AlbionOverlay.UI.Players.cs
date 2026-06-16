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
        private void RenderPlayersTab()
        {
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Player_Title") ?? "Players");
                    ImGui.Checkbox(Lang.Get("Player_ShowOthers") ?? "Show Players", ref _showPlayers);
                    ImGui.Checkbox(Lang.Get("Player_ShowNames") ?? "Show Names", ref _showPlayerName);
                    ImGui.Checkbox(Lang.Get("Player_ShowGuild") ?? "Show Guild", ref _showGuild);
                    ImGui.Checkbox(Lang.Get("Player_ShowCount") ?? "Show Count", ref _showPlayerCount);
                    ImGui.SliderFloat(Lang.Get("Player_EnemyCountHold") ?? "Enemy Count Hold (s)", ref _enemyCountHoldSeconds, 0.1f, 3.0f, "%.1f");
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
        }
    }
}
