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
        private string _announcementText = "";
        private bool _announcementFetched = false;
        private string _announcementLang = "";
        // Site kokundeki duyuru dosyalari: App.txt (yedek) + App_{DIL}.txt
        private const string AnnouncementUrlBase = "https://raw.githubusercontent.com/Jung1330/Nightwatch-Radar/refs/heads/Website/App";

        /// <summary>
        /// "Updates &amp; Current Info" konsolunu SECILI DILE gore doldurur.
        /// Kaynak sirasi: App_{DIL}.txt -> App.txt -> Assets\announcement_{DIL}.txt
        /// -> Assets\announcement.txt -> yerel hata mesaji.
        /// Dil degisirse kendini yeniler.
        /// </summary>
        private void FetchAnnouncement()
        {
            string dil = (Lang.CurrentLang ?? "EN").ToUpperInvariant();
            if (_announcementFetched && _announcementLang == dil) return;
            _announcementFetched = true;
            _announcementLang = dil;
            _announcementText = Lang.Get("Device_AnnouncementLoading") ?? "Please Wait...";

            System.Threading.Tasks.Task.Run(async () =>
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (string url in new[] { $"{AnnouncementUrlBase}_{dil}.txt", $"{AnnouncementUrlBase}.txt" })
                {
                    try
                    {
                        string text = await client.GetStringAsync(url);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _announcementText = text;
                            return;
                        }
                    }
                    catch (Exception) { /* sonraki kaynaga gec */ }
                }

                try
                {
                    string yerel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"announcement_{dil}.txt");
                    if (!File.Exists(yerel))
                        yerel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "announcement.txt");
                    if (File.Exists(yerel))
                    {
                        _announcementText = File.ReadAllText(yerel);
                        return;
                    }
                }
                catch (Exception) { }

                _announcementText = Lang.Get("Device_AnnouncementError") ?? "Please Check Network Connection";
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

            MentalityTheme.SectionHeader(Lang.Get("Device_UpdatesInfo") ?? "Updates & Current Info", MentalityTheme.Colors.AccentSuccess);
            MentalityTheme.BeginCard("ConsoleLog", 0);

            // H2: çalışan sürüm + güncelleme durumu (sürüm Directory.Build.props'ten gelir)
            ImGui.TextColored(MentalityTheme.Colors.TextSecondary, $"Nightwatch {Nightwatch.Program.VersionText}");
            ImGui.TextColored(Nightwatch.Program.UpdateStatusColor, Nightwatch.Program.UpdateStatusText);

            // Dil degisirse FetchAnnouncement kendi kendine yeniler
            FetchAnnouncement();

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
