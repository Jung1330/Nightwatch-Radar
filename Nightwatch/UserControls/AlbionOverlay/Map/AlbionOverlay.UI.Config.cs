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
        private void RenderConfigTab()
        {
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
        }
    }
}
