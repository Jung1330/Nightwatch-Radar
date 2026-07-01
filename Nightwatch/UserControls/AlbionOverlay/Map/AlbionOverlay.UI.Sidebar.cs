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
        private float _animatedTabY = -1f;

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
                var dl = ImGui.GetWindowDrawList();
                dl.ChannelsSplit(2);
                dl.ChannelsSetCurrent(1); // Text and Icons on channel 1 (Top)

                float targetTabY = -1f;

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

                    if (isActive)
                    {
                        targetTabY = startPos.Y;
                    }
                    
                    // Background hover/active with smooth gradient
                    if (isHovered && !isActive)
                    {
                        dl.ChannelsSetCurrent(0); // Draw hover under text
                        uint bgStart = Nightwatch.UserControls.MentalityTheme.Colors.BorderU32;
                        dl.AddRectFilledMultiColor(startPos, startPos + new Vector2(btnWidth, btnHeight), bgStart, 0, 0, bgStart);
                        dl.ChannelsSetCurrent(1);
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

                if (isSetActive)
                {
                    targetTabY = setPos.Y;
                }

                if (isSetHovered && !isSetActive)
                {
                    dl.ChannelsSetCurrent(0);
                    uint bgStart = Nightwatch.UserControls.MentalityTheme.Colors.BorderU32;
                    dl.AddRectFilledMultiColor(setPos, setPos + new Vector2(setBtnWidth, setBtnHeight), bgStart, 0, 0, bgStart);
                    dl.ChannelsSetCurrent(1);
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

                        dl.AddImage(setTex, setPos + new Vector2(20, offY), setPos + new Vector2(20 + iconSize, offY + iconSize), Vector2.Zero, Vector2.One, tint);

                        ImGui.SetCursorScreenPos(setPos + new Vector2(56, (setBtnHeight - ImGui.GetTextLineHeight()) / 2f));
                        string settingsLabel = Lang.Get("Sidebar_Settings") ?? "Settings";
                        ImGui.TextColored(isSetActive ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : (isSetHovered ? Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary : Nightwatch.UserControls.MentalityTheme.Colors.TextSecondary), settingsLabel);
                    }
                }

                // DRAW ANIMATED ACTIVE BAR
                if (targetTabY != -1f)
                {
                    if (_animatedTabY < 0) _animatedTabY = targetTabY;
                    
                    // Animate the active tab
                    _animatedTabY += (targetTabY - _animatedTabY) * ImGui.GetIO().DeltaTime * 15f;
                    
                    dl.ChannelsSetCurrent(0); // Draw under text
                    Vector2 animPos = new Vector2(ImGui.GetWindowPos().X + 10f, _animatedTabY);
                    
                    // Draw sliding background
                    uint glowCol = Nightwatch.UserControls.MentalityTheme.Colors.GlowPurpleU32;
                    dl.AddRectFilledMultiColor(animPos, animPos + new Vector2(sidebarWidth - 20f, 44f), glowCol, 0, 0, glowCol);
                    
                    // Draw sliding accent bar
                    dl.AddRectFilled(animPos, animPos + new Vector2(4f, 44f), Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryU32, 2f);
                    
                }
                
                dl.ChannelsMerge();
            }
            ImGui.EndChild();
        }
    }
}
