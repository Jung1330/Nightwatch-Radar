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
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), Lang.Get("Device_NetworkSettings") ?? "Ağ Bağdaştırıcısı Ayarları (VPN & Booster Support)");
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.TextWrapped(Lang.Get("Device_VPN") ?? "Hiçbir ağ kartı bulunamadı!");
                    ImGui.Spacing();

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
                        ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), Lang.Get("Device_NoAdapterFoundNpcap") ?? "Hiçbir ağ kartı bulunamadı! Npcap kurduğunuzdan emin olun.");
                    }

                    ImGui.Spacing();

                    if (ImGui.Button(Lang.Get("Device_Button1") ?? "Uygulamayı Yeniden Başlat (Restart)", new Vector2(300, 35)))
                    {
                        System.Windows.Forms.Application.Restart();
                        Environment.Exit(0);
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // ==========================================
                    // YENİ: AĞ TANILAMA (DIAGNOSTIC) BÖLÜMÜ
                    // ==========================================
                    ImGui.TextColored(new Vector4(0f, 1f, 1f, 1f), Lang.Get("Device_Discovery"));
                    ImGui.TextWrapped(Lang.Get("Device_DiscoveryText") ?? "Hangi adaptörünüzün Albion Online verisi (5055/5056/5057 UDP) aldığını tespit etmek için oyuna girip hareket ederken bu testi başlatın.");

                    if (_isTestingAdapters)
                    {
                        // Test sırasında ekran donmasın diye adama "Bekle" yazısı gösteriyoruz
                        ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), Lang.Get("Device_Search") ?? "3 Saniye Bekleyiniz.");
                    }
                    else
                    {
                        if (ImGui.Button(Lang.Get("Device_Button2") ?? "Ağları Test Et (Albion Trafiği Ara)", new Vector2(300, 35)))
                        {
                            _isTestingAdapters = true;
                            _adapterTestResults.Clear();

                            // Arayüz donmasın diye testi arka planda (Task) çalıştırıyoruz
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                _adapterTestResults = PacketEngine.TestAllAdaptersForAlbion();
                                _isTestingAdapters = false;
                            });
                        }
                    }

                    // Test sonuçları geldiyse ekrana bas
                    if (_adapterTestResults.Count > 0 && !_isTestingAdapters)
                    {
                        ImGui.Spacing();
                        if (ImGui.BeginChild("AdapterTestResults", new Vector2(0, 200), ImGuiChildFlags.Borders))
                        {
                            foreach (var kvp in _adapterTestResults)
                            {
                                if (kvp.Value)
                                {
                                    // Trafik olan kartı YEŞİL ile kocaman [ YES ] yazarak gösteriyoruz
                                    ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), $"[ YES ] 5055/5056/5057 -> {kvp.Key}");
                                }
                                else
                                {
                                    // Boş kartları GRİ/SÖNÜK şekilde gösteriyoruz
                                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"[ NO  ] 5055/5056/5057 -> {kvp.Key}");
                                }
                            }
                        }
                        ImGui.EndChild();
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), Lang.Get("Device_Info") ?? "* Lütfen [ YES ] yazan adaptörü yukarıdaki menüden seçip 'Restart' atın.");
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // ==========================================
                    // GITHUB DUYURU KONSOLU
                    // ==========================================
                    ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "Updates & Current Info");
                    
                    if (!_announcementFetched)
                    {
                        FetchAnnouncement();
                    }

                    // Konsol görünümü için arka planı siyah, kenarlıkları yeşil yapıyoruz
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.02f, 0.02f, 0.02f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.2f, 0.8f, 0.2f, 0.5f));

                    // Sadece yazı gösteren, tıklanamayan ve kopyalanamayan veya link çalışmayan bir kutu
                    if (ImGui.BeginChild("AnnouncementConsole", new Vector2(0, 150), ImGuiChildFlags.Borders))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.8f, 0.2f, 1.0f)); // Hacker yeşili
                        
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
                    
                    ImGui.PopStyleColor(2);
        }
    }
}
