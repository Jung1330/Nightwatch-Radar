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
        private void RenderPlayersTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Player_Title") ?? "Players Settings", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("PlayersSettings", 0);
            
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ShowOthers") ?? "Show Players", ref _showPlayers);
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ShowNames") ?? "Show Names", ref _showPlayerName);
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ShowGuild") ?? "Show Guild", ref _showGuild);
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ShowCount") ?? "Show Count", ref _showPlayerCount);
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ShowList") ?? "Show List Window", ref _showPlayerList);
            if (_showPlayerList)
            {
                MentalityTheme.AnimatedToggle(Lang.Get("Player_MoveList") ?? "Unlock List Position", ref _playerListMoveable);
            }

            ImGui.Spacing();
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Player_EnemyCountHold") ?? "Enemy Count Hold (s)");
            ImGui.SliderFloat("##EnemyCountHold", ref _enemyCountHoldSeconds, 0.1f, 3.0f, "%.1f");
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Settings_EquipCards") ?? "Equipment Cards", MentalityTheme.Colors.AccentSecondary);
            MentalityTheme.BeginCard("EquipmentCardsSettings", 0);

            MentalityTheme.AnimatedToggle(Lang.Get("Settings_EquipCards") ?? "Enable Equipment Cards", ref _showEquipmentCards);
            if (_showEquipmentCards)
            {
                MentalityTheme.GradientSeparator();
                MentalityTheme.AnimatedToggle(Lang.Get("Player_EquipCardsMove") ?? "Unlock Cards Position", ref _equipmentCardsMoveable);
                
                ImGui.Spacing();
                ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Player_EquipCardsLimit") ?? "Card Limit");
                ImGui.SliderInt("##EquipCardsLimit", ref _equipmentCardsMaxSlots, 1, _equipCardSlots.Length);
                
                ImGui.Spacing();
                ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Player_EquipCardsMemory") ?? "Card Memory (s)");
                ImGui.SliderFloat("##EquipCardsMemory", ref _equipmentCardsMemorySeconds, 0f, 30f, "%.0f");
            }
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Player_Whitelist") ?? "Whitelist", MentalityTheme.Colors.AccentSuccess);
            MentalityTheme.BeginCard("WhitelistSettings", 0);

            MentalityTheme.AnimatedToggle(Lang.Get("Player_ImportSameGuild") ?? "Auto-Whitelist Same Guild", ref _whitelistImportSameGuild);
            MentalityTheme.AnimatedToggle(Lang.Get("Player_ImportSameAlliance") ?? "Auto-Whitelist Same Alliance", ref _whitelistImportSameAlliance);

            MentalityTheme.GradientSeparator();

            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Player_WhitelistAdd") ?? "Add Player:");
            ImGui.SetNextItemWidth(220);
            ImGui.InputText("##WlAdd", ref _whitelistInput, 32);
            
            ImGui.SameLine();
            if (MentalityTheme.Button(Lang.Get("Player_WhitelistBtn") ?? "Add", new Vector2(80, 24)))
            {
                if (!string.IsNullOrEmpty(_whitelistInput))
                {
                    _whitelist.Add(_whitelistInput);
                    ImportWhitelistByGuildAlliance(_whitelistInput);
                    SaveWhitelist();
                    _whitelistInput = "";
                }
            }
            
            ImGui.Spacing(); ImGui.Spacing();
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, "Whitelisted Players");
            
            if (ImGui.BeginChild("WlScroll", new Vector2(0, 150), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
                string nameToRemove = null;
                foreach (var name in _whitelist)
                {
                    ImGui.TextColored(MentalityTheme.Colors.AccentSuccess, name);
                    
                    float avail = ImGui.GetContentRegionAvail().X;
                    if (avail > 80) ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                    else ImGui.SameLine();
                    
                    if (MentalityTheme.SmallButton($"{Lang.Get("Player_WhitelistRemove") ?? "DEL"}##{name}", MentalityTheme.Colors.Border)) nameToRemove = name;
                }
                ImGui.PopStyleVar();
                if (nameToRemove != null) { _whitelist.Remove(nameToRemove); SaveWhitelist(); }
            }
            ImGui.EndChild();
            MentalityTheme.EndCard();
        }
    }
}
