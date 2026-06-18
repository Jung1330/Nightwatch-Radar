using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using ClickableTransparentOverlay;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        private void RenderSidebar(float sidebarWidth)
        {
            ImGui.BeginChild("Sidebar", new Vector2(sidebarWidth, 0), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
            {
                ImGui.Spacing(); ImGui.Spacing();

                // Logo section
                string logoPath;
                if (_resourceCache.TryGetValue("logo", out string? cachedLogo) && !string.IsNullOrEmpty(cachedLogo)) { logoPath = cachedLogo; }
                else { using (var bmp = Nightwatch.Properties.Resources.Nightwatch) { logoPath = GetResourceToTemp(bmp, "logo"); } }

                if (File.Exists(logoPath))
                {
                    AddOrGetImagePointer(logoPath, true, out IntPtr logoTex, out uint lw, out uint lh);
                    if (logoTex != IntPtr.Zero)
                    {
                        float imgSize = 100f;
                        ImGui.SetCursorPosX((sidebarWidth - imgSize) / 2f);
                        ImGui.Image(logoTex, new Vector2(imgSize, imgSize));
                    }
                }

                // App Name under logo
                ImGui.Spacing();
                Nightwatch.UserControls.MentalityTheme.GradientSeparator(Nightwatch.UserControls.MentalityTheme.Colors.Border, 1f);
                ImGui.Spacing();

                // Sidebar Tabs
                for (int i = 0; i < 7; i++)
                {
                    if (i == 5) continue; // Skip Settings for bottom

                    bool isActive = (_activeTab == i);
                    float btnHeight = 44f;
                    float btnWidth = sidebarWidth - 20f;

                    ImGui.SetCursorPosX(10f);
                    Vector2 startPos = ImGui.GetCursorScreenPos();

                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0, 0, 0, 0));
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0, 0, 0, 0));

                    if (ImGui.Selectable($"##tab{i}", isActive, ImGuiSelectableFlags.None, new Vector2(btnWidth, btnHeight)))
                        _activeTab = i;

                    bool isHovered = ImGui.IsItemHovered();
                    ImGui.PopStyleColor(3);

                    var dl = ImGui.GetWindowDrawList();
                    
                    // Background hover/active with smooth gradient
                    if (isActive || isHovered)
                    {
                        uint bgStart = isActive ? Nightwatch.UserControls.MentalityTheme.Colors.GlowPurpleU32 : Nightwatch.UserControls.MentalityTheme.Colors.BorderU32;
                        uint bgEnd = 0x00000000;
                        dl.AddRectFilledMultiColor(startPos, startPos + new Vector2(btnWidth, btnHeight), bgStart, bgEnd, bgEnd, bgStart);
                    }

                    // Active accent bar
                    if (isActive)
                    {
                        dl.AddRectFilled(startPos, startPos + new Vector2(4f, btnHeight), Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryU32, 2f);
                    }

                    // Icons
                    string tabName = "tab_" + i;
                    string iconPath = "";

                    if (_resourceCache.TryGetValue(tabName, out string? cachedTab) && !string.IsNullOrEmpty(cachedTab)) { iconPath = cachedTab; }
                    else
                    {
                        using (System.Drawing.Bitmap? currentIcon = i switch
                        {
                            0 => Nightwatch.Properties.Resources.ResourcesPNG,
                            1 => Nightwatch.Properties.Resources.MobMistPNG,
                            2 => Nightwatch.Properties.Resources.PlayersPNG,
                            3 => Nightwatch.Properties.Resources.ConfigPNG,
                            4 => Nightwatch.Properties.Resources.DevToolsPNG,
                            6 => Nightwatch.Properties.Resources.SettingsPNG,
                            _ => null
                        })
                        {
                            if (currentIcon != null) iconPath = GetResourceToTemp(currentIcon, tabName);
                        }
                    }

                    if (IsImageExistsCached(iconPath))
                    {
                        AddOrGetImagePointer(iconPath, true, out IntPtr tex, out uint iw, out uint ih);
                        if (tex != IntPtr.Zero)
                        {
                            float iconSize = 24f;
                            float offY = (btnHeight - iconSize) / 2f;
                            uint tint = isActive ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimaryU32 : (isHovered ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimaryU32 : Nightwatch.UserControls.MentalityTheme.Colors.TextSecondaryU32);

                            dl.AddImage(tex, startPos + new Vector2(20, offY), startPos + new Vector2(20 + iconSize, offY + iconSize), Vector2.Zero, Vector2.One, tint);

                            ImGui.SetCursorScreenPos(startPos + new Vector2(56, (btnHeight - ImGui.GetTextLineHeight()) / 2f));
                            ImGui.TextColored(isActive ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : (isHovered ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : Nightwatch.UserControls.MentalityTheme.Colors.TextSecondary), _tabs[i]);
                        }
                    }
                    ImGui.Dummy(new Vector2(0, 4f));
                }

                // Update Status Badge - Program.UpdateStatusText already contains translated text
                string statusDisplay = Program.UpdateStatusText ?? "...";

                ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 100f);
                ImGui.SetCursorPosX(0f); // Reset cursor before StatusBadge computes centering
                Nightwatch.UserControls.MentalityTheme.StatusBadge(statusDisplay, Program.UpdateStatusColor, sidebarWidth);

                // Settings Icon at the bottom
                ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 60f);
                Nightwatch.UserControls.MentalityTheme.GradientSeparator(Nightwatch.UserControls.MentalityTheme.Colors.Border, 1f);

                bool isSetActive = (_activeTab == 5);
                float setBtnHeight = 44f;
                float setBtnWidth = sidebarWidth - 20f;

                ImGui.SetCursorPosX(10f);
                Vector2 setPos = ImGui.GetCursorScreenPos();

                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0, 0, 0, 0));
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0, 0, 0, 0));
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0, 0, 0, 0));

                if (ImGui.Selectable("##SettingsIcon", isSetActive, ImGuiSelectableFlags.None, new Vector2(setBtnWidth, setBtnHeight)))
                    _activeTab = 5;

                bool isSetHovered = ImGui.IsItemHovered();
                ImGui.PopStyleColor(3);

                var dl2 = ImGui.GetWindowDrawList();
                if (isSetActive || isSetHovered)
                {
                    uint bgStart = isSetActive ? Nightwatch.UserControls.MentalityTheme.Colors.GlowPurpleU32 : Nightwatch.UserControls.MentalityTheme.Colors.BorderU32;
                    uint bgEnd = 0x00000000;
                    dl2.AddRectFilledMultiColor(setPos, setPos + new Vector2(setBtnWidth, setBtnHeight), bgStart, bgEnd, bgEnd, bgStart);
                }

                if (isSetActive)
                {
                    dl2.AddRectFilled(setPos, setPos + new Vector2(4f, setBtnHeight), Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryU32, 2f);
                }

                string setPath;
                if (_resourceCache.TryGetValue("settings", out string? cachedSet) && !string.IsNullOrEmpty(cachedSet)) { setPath = cachedSet; }
                else { using (var bmp = Nightwatch.Properties.Resources.SettingsPNG) { setPath = GetResourceToTemp(bmp, "settings"); } }

                if (IsImageExistsCached(setPath))
                {
                    AddOrGetImagePointer(setPath, true, out IntPtr setTex, out uint sw, out uint sh);
                    if (setTex != IntPtr.Zero)
                    {
                        float iconSize = 24f;
                        float offY = (setBtnHeight - iconSize) / 2f;
                        uint tint = isSetActive ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimaryU32 : (isSetHovered ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimaryU32 : Nightwatch.UserControls.MentalityTheme.Colors.TextSecondaryU32);

                        dl2.AddImage(setTex, setPos + new Vector2(20, offY), setPos + new Vector2(20 + iconSize, offY + iconSize), Vector2.Zero, Vector2.One, tint);

                        ImGui.SetCursorScreenPos(setPos + new Vector2(56, (setBtnHeight - ImGui.GetTextLineHeight()) / 2f));
                        string settingsLabel = Lang.Get("Sidebar_Settings") ?? "Settings";
                        ImGui.TextColored(isSetActive ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : (isSetHovered ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : Nightwatch.UserControls.MentalityTheme.Colors.TextSecondary), settingsLabel);
                    }
                }
            }
            ImGui.EndChild();
        }
    }
}
