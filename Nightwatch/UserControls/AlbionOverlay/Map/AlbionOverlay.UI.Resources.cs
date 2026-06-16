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
        private void RenderResourcesTab()
        {
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), Lang.Get("Res_Title") ?? "Resources");
                    ImGui.Checkbox(Lang.Get("Res_ShowIcons") ?? "Show Icons", ref _showResourceIcons);
                    ImGui.Checkbox(Lang.Get("Res_ShowOnMap") ?? "Show on Map", ref _showResources);
                    ImGui.Checkbox(Lang.Get("Res_TrackerOnly") ?? "Tracker only (hide radar dots)", ref _resourceTrackerOnlyMode);
                    ImGui.Checkbox(Lang.Get("Res_Label") ?? "Show Resource Labels", ref _showResourceLabels);
                    ImGui.SliderFloat(Lang.Get("Res_IconSize") ?? "Icon Size", ref _globalIconSize, 10, 80);

                    ImGui.Separator();
                    var cats = Enum.GetValues(typeof(HarvestableCategory)).Cast<HarvestableCategory>();
                    foreach (var cat in cats)
                    {
                        if (cat == HarvestableCategory.None) continue;
                        if (!_resourceMasterToggles.ContainsKey(cat)) _resourceMasterToggles[cat] = true;
                        if (!_resourceFilters.ContainsKey(cat)) { var m = new bool[8, 4]; for (int i = 0; i < 8; i++) for (int j = 0; j < 4; j++) m[i, j] = true; _resourceFilters[cat] = m; }
                        bool on = _resourceMasterToggles[cat];
                        string displayCatName = Lang.Get(cat.ToString()) != cat.ToString() ? Lang.Get(cat.ToString()) : cat.ToString();
                        if (ImGui.Checkbox(displayCatName, ref on)) _resourceMasterToggles[cat] = on;
                        if (on)
                        {
                            ImGui.Indent();
                            if (ImGui.TreeNode(string.Format(Lang.Get("Res_Filter") ?? "{0} Filter", cat)))
                            {
                                if (ImGui.BeginTable($"TÄ°ERR_{cat}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                                {
                                    ImGui.TableSetupColumn(Lang.Get("Res_Enchantment") ?? "Enchant"); ImGui.TableSetupColumn("0"); ImGui.TableSetupColumn("1"); ImGui.TableSetupColumn("2"); ImGui.TableSetupColumn("3"); ImGui.TableHeadersRow();
                                    for (int t = 0; t < 8; t++)
                                    {
                                        ImGui.TableNextRow(); ImGui.TableSetColumnIndex(0); ImGui.TextColored(new Vector4(1, 0.9f, 0, 1), $"T{t + 1}");
                                        for (int e = 0; e < 4; e++)
                                        {
                                            ImGui.TableSetColumnIndex(e + 1); if (t < 3 && e > 0) continue;
                                            ImGui.PushID($"{cat}{t}{e}"); ImGui.Checkbox("", ref _resourceFilters[cat][t, e]); ImGui.PopID();
                                        }
                                    }
                                    ImGui.EndTable();
                                }
                                ImGui.TreePop();
                            }
                            ImGui.Unindent();
                        }
                    }
        }
    }
}
