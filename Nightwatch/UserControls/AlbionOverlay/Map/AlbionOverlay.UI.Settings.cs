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
        private void RenderSettingsTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Settings_CalibTitle") ?? "Radar Calibration", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("RadarCalibration", 0);
            
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_Zoom") ?? "Zoom");
            ImGui.SliderFloat("##Zoom", ref _zoom, 0.5f, 10.0f);
            
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_RadarSize") ?? "Size");
            ImGui.SliderFloat("##RadarSize", ref _radarSize, 200, 2500);
            
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_Render_Distance") ?? "Render Distance");
            ImGui.SliderFloat("##RenderDistance", ref _renderDistance, 10.0f, 2500.0f, "%.0f");
            
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Settings_MapBgTitle") ?? "Map Background", MentalityTheme.Colors.AccentSecondary);
            MentalityTheme.BeginCard("MapBackground", 0);
            MentalityTheme.AnimatedToggle(Lang.Get("Settings_ShowMapBg") ?? "Show Map Background", ref _showMapBackground);
            if (_showMapBackground)
            {
                MentalityTheme.GradientSeparator();
                ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_MapOpacity") ?? "Map Opacity");
                ImGui.SliderFloat("##MapOpacity", ref _mapOpacity, 0.1f, 1.0f, "%.2f");
            }
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Settings_WindowTitle") ?? "Window Settings", MentalityTheme.Colors.AccentWarning);
            MentalityTheme.BeginCard("WindowSettings", 0);
            
            MentalityTheme.AnimatedToggle(Lang.Get("Settings_DetachRadar") ?? "Detach Radar", ref _detachRadar);
            if (_detachRadar)
            {
                MentalityTheme.AnimatedToggle(Lang.Get("Settings_MoveRadar") ?? "Move Radar", ref _radarMoveable);
            }
            
            MentalityTheme.AnimatedToggle(Lang.Get("Settings_ShowWatermark") ?? "Watermark", ref _showWatermark);
            if (_showWatermark)
            {
                MentalityTheme.AnimatedToggle(Lang.Get("Settings_MoveWatermark") ?? "Move Watermark", ref _watermarkMoveable);
            }

            MentalityTheme.GradientSeparator();
            // MentalityTheme.AnimatedToggle(Lang.Get("Settings_DangerAlarm") ?? "Danger Compass", ref _showDangerCompass);
            MentalityTheme.AnimatedToggle(Lang.Get("Settings_EnableSound") ?? "Enable Sound Alerts", ref _enableSoundAlerts);
            
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader("System & Application", MentalityTheme.Colors.AccentSuccess);
            MentalityTheme.BeginCard("SystemApp", 0);

            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_UITheme") ?? "UI Theme");
            ImGui.SetNextItemWidth(180);
            string[] themeNames = { "Deep Space Black", "Obsidian", "Blood Moon" };
            if (ImGui.Combo("##ThemeSelect", ref _selectedTheme, themeNames, themeNames.Length))
            {
                MentalityTheme.SetTheme((ThemeType)_selectedTheme);
            }

            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_Language") ?? "Language");
            ImGui.SetNextItemWidth(180);
            int prevLangIdx = _selectedLangIndex;
            if (ImGui.Combo("##LangSettings", ref _selectedLangIndex, _languages, _languages.Length))
            {
                string newLang = _selectedLangIndex switch { 0 => "TR", 1 => "EN", 2 => "RU", 3 => "ZH", _ => "TR" };

                Lang.LoadLanguage(newLang);
                ApplyLanguageFont(newLang);

                try
                {
                    string langPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "lang.txt");
                    System.IO.File.WriteAllText(langPath, newLang);
                }
                catch (Exception ex) { Nightwatch.UIConsole.Log($"[ERROR] Lang save failed: {ex.Message}", Nightwatch.LogLevel.Error); }

                MobMapper.Instance.Reload($"Assets/Helper/mobs_{newLang}_min.json");
                CheckAndLoadDatabase();  
                LoadItemDatabaseTXT();   

                _lastTabLanguage = null;
            }

            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Settings_Stream_Module") ?? "Stream Bypass");
            bool prevStream = _streamModuleEnabled;
            MentalityTheme.AnimatedToggle((Lang.Get("Settings_OBS") ?? "OBS Bypass"), ref _streamModuleEnabled);
            if (_streamModuleEnabled != prevStream) ApplyStreamModule();

            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Settings_HotkeyTitle") ?? "Hotkeys", MentalityTheme.Colors.AccentDanger);
            MentalityTheme.BeginCard("HotkeysCard", 0);

            string btnText = _isChangingHotkey ? Lang.Get("Settings_HotkeyWait") ?? "Wait..." : string.Format(Lang.Get("Settings_HotkeyMenu") ?? "Toggle: {0}", GetKeyName(_toggleKey));
            if (MentalityTheme.Button(btnText, new Vector2(250, 40)))
            {
                _isChangingHotkey = true;
                _isChangingMuteHotkey = false;
            }

            ImGui.Spacing();

            string muteBtnText = _isChangingMuteHotkey ? Lang.Get("Settings_HotkeyWait") ?? "Wait..." : string.Format(Lang.Get("Settings_HotkeyMute") ?? "Mute: {0}", GetKeyName(_muteToggleKey));
            if (MentalityTheme.Button(muteBtnText, new Vector2(250, 40)))
            {
                _isChangingMuteHotkey = true;
                _isChangingHotkey = false;
                _isChangingHideAllHotkey = false;
            }

            ImGui.Spacing();

            string hideAllBtnText = _isChangingHideAllHotkey ? Lang.Get("Settings_HotkeyWait") ?? "Wait..." : string.Format(Lang.Get("Settings_HotkeyHideAll") ?? "Hide All: {0}", GetKeyName(_hideAllKey));
            if (MentalityTheme.Button(hideAllBtnText, new Vector2(250, 40)))
            {
                _isChangingHideAllHotkey = true;
                _isChangingHotkey = false;
                _isChangingMuteHotkey = false;
            }

            ImGui.SameLine();
            
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
            if (!_enableSoundAlerts)
                MentalityTheme.StatusBadge(Lang.Get("Settings_SoundOff") ?? "OFF", MentalityTheme.Colors.AccentDanger);
            else
                MentalityTheme.StatusBadge(Lang.Get("Settings_SoundOn") ?? "ON", MentalityTheme.Colors.AccentSuccess);

            if (_isChangingHotkey || _isChangingMuteHotkey || _isChangingHideAllHotkey)
            {
                int pressed = GetPressedKey();
                if (pressed != -1 && pressed != 0x01 && pressed != 0x02)
                {
                    if (pressed == 0x1B || pressed == 0x21 || pressed == 0x22 || pressed == 0x23 || pressed == 0x24)
                    {
                        _isChangingHotkey = false;
                        _isChangingMuteHotkey = false;
                        _isChangingHideAllHotkey = false;
                    }
                    else
                    {
                        if (_isChangingHotkey) _toggleKey = pressed;
                        if (_isChangingMuteHotkey) _muteToggleKey = pressed;
                        if (_isChangingHideAllHotkey) _hideAllKey = pressed;

                        _isChangingHotkey = false;
                        _isChangingMuteHotkey = false;
                        _isChangingHideAllHotkey = false;
                    }
                }
            }

            MentalityTheme.EndCard();
        }
    }
}


