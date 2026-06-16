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
        private void RenderSettingsTab()
        {
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
                    ImGui.TextColored(new Vector4(0.00f, 1.00f, 1.00f, 1f), Lang.Get("Settings_UITheme") ?? "UI Tema");
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

                        // Seçilen dili Config/lang.txt dosyasına kaydet ki bir sonraki açılışta aynı dil yüklensin
                        try
                        {
                            string langPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "lang.txt");
                            System.IO.File.WriteAllText(langPath, newLang);
                        }
                        catch (Exception ex) { UIConsole.Log($"[HATA] Dil kaydedilemedi: {ex.Message}", LogLevel.Error); }

                        MobMapper.Instance.Reload($"Assets/Helper/mobs_{newLang}.min.json");
                        CheckAndLoadDatabase();  // Mob veritabanı
                        LoadItemDatabaseTXT();   // Item veritabanı (İKSİR, SİLAH vb.)

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

        }
    }
}
