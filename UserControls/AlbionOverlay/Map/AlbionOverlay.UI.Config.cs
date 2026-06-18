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
        private void RenderConfigTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Config_Title") ?? "Configuration Management", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("ConfigSave", 0);
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Config_NameInput") ?? "Config Name:");
            ImGui.SetNextItemWidth(150);
            ImGui.InputText("##Name", ref _configFileNameInput, 32);
            
            ImGui.SameLine();
            if (MentalityTheme.Button(Lang.Get("Config_SaveBtn") ?? "Save", new Vector2(80, 24)))
            {
                if (!string.IsNullOrWhiteSpace(_configFileNameInput))
                {
                    SaveConfig(_configFileNameInput);
                }
            }
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader("Available Configurations", MentalityTheme.Colors.AccentSecondary);
            MentalityTheme.BeginCard("ConfigList", 160);
            
            if (ImGui.BeginChild("CfgList", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                for (int i = 0; i < _availableConfigs.Length; i++)
                {
                    bool isSelected = _selectedConfigIndex == i;
                    
                    if (isSelected)
                    {
                        ImGui.TextColored(MentalityTheme.Colors.AccentPrimary, _availableConfigs[i]);
                    }
                    else
                    {
                        ImGui.TextColored(MentalityTheme.Colors.TextPrimary, _availableConfigs[i]);
                    }

                    if (ImGui.IsItemClicked())
                    {
                        _selectedConfigIndex = i; 
                        _configFileNameInput = _availableConfigs[i];
                    }
                    
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.GetWindowDrawList().AddRectFilled(
                            ImGui.GetItemRectMin(),
                            ImGui.GetItemRectMax(),
                            MentalityTheme.Colors.BorderU32,
                            4f
                        );
                    }
                }
            }
            ImGui.EndChild();
            MentalityTheme.EndCard();

            ImGui.Spacing();

            if (MentalityTheme.Button(Lang.Get("Config_LoadBtn") ?? "Load Selected")) 
            {
                if (_selectedConfigIndex >= 0) LoadConfig(_availableConfigs[_selectedConfigIndex]);
            }
            
            ImGui.SameLine();
            
            if (MentalityTheme.Button(Lang.Get("Config_RefreshBtn") ?? "Refresh List")) 
            {
                RefreshConfigList();
            }
        }
    }
}
