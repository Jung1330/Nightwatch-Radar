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
        private string _announcementText = "Please Wait...";
        private bool _announcementFetched = false;
        // BURAYA GITHUB RAW LINKINI YAZACAKSIN
        private readonly string _announcementUrl = "https://raw.githubusercontent.com/Jung1330/Nightwatch-Radar/refs/heads/Website/App.txt";

        private void FetchAnnouncement()
        {
            if (_announcementFetched) return;
            _announcementFetched = true;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var text = await client.GetStringAsync(_announcementUrl);
                    _announcementText = text;
                }
                catch (Exception)
                {
                    _announcementText = "Please Check Network Connection";
                }
            });
        }

        private void RenderDeviceTab()
        {
            MentalityTheme.SectionHeader(Lang.Get("Device_NetworkSettings") ?? "Network Adapter Settings", MentalityTheme.Colors.AccentPrimary);

            MentalityTheme.BeginCard("NetworkSettings", 0);
            
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Device_VPN") ?? "No network adapter found!");

            if (!_adaptersLoaded)
            {
                _availableAdapters = PacketEngine.GetAvailableAdapters();
                _adaptersLoaded = true;

                string saved = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "last_adapter.txt"))
                    ? File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "last_adapter.txt")).Trim() : "";

                int idx = _availableAdapters.IndexOf(saved);
                if (idx != -1) _selectedAdapterIndex = idx;
            }

            if (_availableAdapters.Count > 0)
            {
                ImGui.SetNextItemWidth(400);
                if (ImGui.Combo("##NetworkAdapter", ref _selectedAdapterIndex, _availableAdapters.ToArray(), _availableAdapters.Count))
                {
                    PacketEngine.SaveSelectedAdapter(_availableAdapters[_selectedAdapterIndex]);
                }
            }
            else
            {
                MentalityTheme.StatusBadge(Lang.Get("Device_NoAdapterFoundNpcap") ?? "No network adapter found! Make sure Npcap is installed.", MentalityTheme.Colors.AccentDanger);
            }

            if (MentalityTheme.Button(Lang.Get("Device_Button1") ?? "Restart Application", new Vector2(300, 40)))
            {
                System.Windows.Forms.Application.Restart();
                Environment.Exit(0);
            }

            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader(Lang.Get("Device_Discovery") ?? "Network Diagnostic", MentalityTheme.Colors.AccentSecondary);
            MentalityTheme.BeginCard("NetworkDiagnostic", 0);

            ImGui.TextWrapped(Lang.Get("Device_DiscoveryText") ?? "To find out which adapter receives Albion Online data (5055/5056/5057 UDP), log into the game and move around while running this test.");

            if (_isTestingAdapters)
            {
                MentalityTheme.StatusBadge(Lang.Get("Device_Search") ?? "Please wait 3 seconds...", MentalityTheme.Colors.AccentWarning);
            }
            else
            {
                if (MentalityTheme.Button(Lang.Get("Device_Button2") ?? "Test Networks", new Vector2(300, 40)))
                {
                    _isTestingAdapters = true;
                    _adapterTestResults.Clear();

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        _adapterTestResults = PacketEngine.TestAllAdaptersForAlbion();
                        _isTestingAdapters = false;
                    });
                }
            }

            if (_adapterTestResults.Count > 0 && !_isTestingAdapters)
            {
                MentalityTheme.GradientSeparator();
                
                if (ImGui.BeginChild("AdapterTestResults", new Vector2(0, 150), ImGuiChildFlags.None))
                {
                    foreach (var kvp in _adapterTestResults)
                    {
                        if (kvp.Value)
                        {
                            ImGui.TextColored(MentalityTheme.Colors.AccentSuccess, $"[ YES ] 5055/5056/5057 -> {kvp.Key}");
                        }
                        else
                        {
                            ImGui.TextColored(MentalityTheme.Colors.TextMuted, $"[ NO  ] 5055/5056/5057 -> {kvp.Key}");
                        }
                    }
                }
                ImGui.EndChild();
                ImGui.TextColored(MentalityTheme.Colors.TextSecondary, Lang.Get("Device_Info") ?? "* Please select the adapter that says [ YES ] from the menu above and click 'Restart'.");
            }
            
            MentalityTheme.EndCard();

            MentalityTheme.SectionHeader("Updates & Current Info", MentalityTheme.Colors.AccentSuccess);
            MentalityTheme.BeginCard("ConsoleLog", 0);

            if (!_announcementFetched)
            {
                FetchAnnouncement();
            }

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.02f, 0.02f, 0.03f, 1.0f));
            
            if (ImGui.BeginChild("AnnouncementConsole", new Vector2(0, 200), ImGuiChildFlags.Borders))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, MentalityTheme.Colors.AccentSuccess);
                
                string[] lines = _announcementText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    foreach (var line in lines)
                    {
                        ImGui.TextWrapped(line);
                    }
                }
                else
                {
                    ImGui.TextWrapped(_announcementText);
                }
                
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            
            MentalityTheme.EndCard();
        }
    }
}
