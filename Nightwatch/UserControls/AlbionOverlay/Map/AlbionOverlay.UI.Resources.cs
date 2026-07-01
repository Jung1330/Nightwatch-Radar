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
        private void RenderResourcesTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Res_Title") ?? "Resources Settings", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("ResourcesSettings", 0);
            
            MentalityTheme.AnimatedToggle(Lang.Get("Res_ShowOnMap") ?? "Show on Map", ref _showResources);
            if (_showResources)
            {
                ImGui.Indent();
                if (ImGui.TreeNodeEx("Resource Settings", ImGuiTreeNodeFlags.None))
                {
                    MentalityTheme.AnimatedToggle(Lang.Get("Res_ShowIcons") ?? "Show Icons", ref _showResourceIcons);
                    MentalityTheme.AnimatedToggle(Lang.Get("Res_Label") ?? "Show Labels", ref _showResourceLabels);
                    MentalityTheme.AnimatedToggle(Lang.Get("Res_TrackerOnly") ?? "Tracker Only", ref _resourceTrackerOnlyMode);
                    // MentalityTheme.AnimatedToggle(Lang.Get("Res_EnchantedOnly") ?? "Enchanted Only", ref _resourceShowOnlyEnchanted);

                    ImGui.Spacing();
                    ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Res_IconSize") ?? "Icon Size");
                    ImGui.SliderFloat("##IconSizeRes", ref _globalIconSize, 10, 80);
                    ImGui.TreePop();
                }
                ImGui.Unindent();
            }
            
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader("Resource Filters", MentalityTheme.Colors.AccentSecondary);

            var cats = Enum.GetValues(typeof(HarvestableCategory)).Cast<HarvestableCategory>();
            
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
            foreach (var cat in cats)
            {
                if (cat == HarvestableCategory.None) continue;
                if (!_resourceMasterToggles.ContainsKey(cat)) _resourceMasterToggles[cat] = true;
                if (!_resourceFilters.ContainsKey(cat)) { 
                    var m = new bool[8, 4]; 
                    for (int i = 0; i < 8; i++) for (int j = 0; j < 4; j++) m[i, j] = true; 
                    _resourceFilters[cat] = m; 
                }
                
                bool on = _resourceMasterToggles[cat];
                string displayCatName = Lang.Get(cat.ToString()) != cat.ToString() ? Lang.Get(cat.ToString()) : cat.ToString();
                
                MentalityTheme.BeginCard($"Card_{cat}");
                
                bool isOpen = MentalityTheme.TreeNode(displayCatName, false);
                
                if (isOpen)
                {
                    bool onRef = _resourceMasterToggles[cat];
                    MentalityTheme.AnimatedToggle($"Show {displayCatName}", ref onRef);
                    _resourceMasterToggles[cat] = onRef;

                    MentalityTheme.GradientSeparator();
                    
                    if (ImGui.BeginTable($"TIER_{cat}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn(Lang.Get("Res_Enchantment") ?? "Enchant");
                        ImGui.TableSetupColumn("0");
                        ImGui.TableSetupColumn("1");
                        ImGui.TableSetupColumn("2");
                        ImGui.TableSetupColumn("3");
                        ImGui.TableHeadersRow();
                        
                        for (int t = 0; t < 8; t++)
                        {
                            ImGui.TableNextRow(); 
                            ImGui.TableSetColumnIndex(0); 
                            ImGui.TextColored(MentalityTheme.Colors.AccentPrimaryLt, $"Tier {t + 1}");
                            
                            for (int e = 0; e < 4; e++)
                            {
                                ImGui.TableSetColumnIndex(e + 1); 
                                if (t < 3 && e > 0) continue;
                                
                                ImGui.PushID($"{cat}{t}{e}"); 
                                ImGui.Checkbox("", ref _resourceFilters[cat][t, e]); 
                                ImGui.PopID();
                            }
                        }
                        ImGui.EndTable();
                    }
                    ImGui.TreePop();
                }
                
                MentalityTheme.EndCard();
            }
            ImGui.PopStyleVar();
        }
    }
}
