#region Kütüphaneler (Using Directives)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Utils;
using AlbionDataHandlers.Mappers;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
#endregion

namespace Nightwatch
{
    public partial class AlbionOverlay
    {

        private static Dictionary<int, string> ParsePayloadToMap(string payload)
        {
            var map = new Dictionary<int, string>();
            if (string.IsNullOrWhiteSpace(payload)) return map;

            var matches = Regex.Matches(payload, @"\[(\d+)(?::[^\]]+)?\]=");
            for (int i = 0; i < matches.Count; i++)
            {
                var current = matches[i];
                if (!int.TryParse(current.Groups[1].Value, out int key)) continue;

                int valueStart = current.Index + current.Length;
                int valueEnd = (i + 1 < matches.Count) ? matches[i + 1].Index : payload.Length;
                if (valueEnd < valueStart) continue;

                string value = payload.Substring(valueStart, valueEnd - valueStart);
                value = value.Trim().TrimEnd('|').Trim();
                map[key] = value;
            }

            return map;
        }

        private static byte[] ParseByteArrayFromValueString(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<byte>();
            var m = Regex.Match(value, @"byte\[\d+\]\((.*)\)");
            if (!m.Success) return Array.Empty<byte>();

            string inside = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(inside)) return Array.Empty<byte>();

            var tokens = inside.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(x => x.Trim())
                               .Where(x => x != "...")
                               .ToList();

            var list = new List<byte>(tokens.Count);
            foreach (var t in tokens)
            {
                if (byte.TryParse(t, out byte b)) list.Add(b);
            }
            return list.ToArray();
        }

        private static int ReadInt32LE(byte[] src, int offset)
        {
            if (offset < 0 || offset + 4 > src.Length) return 0;
            return src[offset]
                | (src[offset + 1] << 8)
                | (src[offset + 2] << 16)
                | (src[offset + 3] << 24);
        }

        private static bool TryParseFloatInvariant(string? s, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool IsValidDecodePos(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
            if (Math.Abs(x) >= 4000f || Math.Abs(y) >= 4000f) return false;
            return Math.Abs(x) > 0.1f || Math.Abs(y) > 0.1f;
        }

        private static float DistSq(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        private static void AddDecodeCandidate(List<(string mode, float x, float y)> list, string mode, float x, float y)
        {
            if (IsValidDecodePos(x, y)) list.Add((mode, x, y));
        }

        private static List<(string mode, float x, float y)> DecodeCandidatesFromPayload(
            string payload,
            bool d01, bool d02, bool d03, bool d04, bool d05,
            bool d06, bool d07, bool d08, bool d09, bool d10,
            bool d11, bool d12, bool d13, bool d14, bool d15)
        {
            var result = new List<(string mode, float x, float y)>();
            var map = ParsePayloadToMap(payload);

            // Param yolları
            if (d13 && map.TryGetValue(4, out var p4) && map.TryGetValue(5, out var p5)
                && TryParseFloatInvariant(p4, out var x45) && TryParseFloatInvariant(p5, out var y45))
            {
                AddDecodeCandidate(result, "13:[4,5]", x45, y45);
            }

            if (d14 && map.TryGetValue(19, out var p19) && map.TryGetValue(25, out var p25)
                && TryParseFloatInvariant(p19, out var x1925) && TryParseFloatInvariant(p25, out var y1925))
            {
                AddDecodeCandidate(result, "14:[19,25]", x1925, y1925);
            }

            // Byte/List payload yolu
            if (!map.TryGetValue(1, out var p1Raw)) return result;

            byte[] bytes = ParseByteArrayFromValueString(p1Raw);
            if (bytes.Length < 13) return result;

            if (d15)
            {
                // p1 list [0,1] genelde byte list olabilir; parse stringinden ilk iki byte'ı dene
                float lx = bytes[0];
                float ly = bytes[1];
                AddDecodeCandidate(result, "15:list[0,1]", lx, ly);
            }

            if (bytes.Length >= 13)
            {
                if (d01) AddDecodeCandidate(result, "01:i[1,9]/1e7", ReadInt32LE(bytes, 1) / 10_000_000f, ReadInt32LE(bytes, 9) / 10_000_000f);
                if (d02) AddDecodeCandidate(result, "02:i[1,9]/1e6", ReadInt32LE(bytes, 1) / 1_000_000f, ReadInt32LE(bytes, 9) / 1_000_000f);
                if (d03) AddDecodeCandidate(result, "03:i[1,9]/1e5", ReadInt32LE(bytes, 1) / 100_000f, ReadInt32LE(bytes, 9) / 100_000f);
                if (d04) AddDecodeCandidate(result, "04:i[1,9]/100", ReadInt32LE(bytes, 1) / 100f, ReadInt32LE(bytes, 9) / 100f);
                if (d05 && bytes.Length >= 13) AddDecodeCandidate(result, "05:f[1,9]", BitConverter.ToSingle(bytes, 1), BitConverter.ToSingle(bytes, 9));
            }

            if (bytes.Length >= 17)
            {
                if (d06) AddDecodeCandidate(result, "06:i[9,13]/1e7", ReadInt32LE(bytes, 9) / 10_000_000f, ReadInt32LE(bytes, 13) / 10_000_000f);
                if (d07) AddDecodeCandidate(result, "07:i[9,13]/1e6", ReadInt32LE(bytes, 9) / 1_000_000f, ReadInt32LE(bytes, 13) / 1_000_000f);
                if (d08) AddDecodeCandidate(result, "08:i[9,13]/1e5", ReadInt32LE(bytes, 9) / 100_000f, ReadInt32LE(bytes, 13) / 100_000f);
                if (d09) AddDecodeCandidate(result, "09:i[9,13]/100", ReadInt32LE(bytes, 9) / 100f, ReadInt32LE(bytes, 13) / 100f);
                if (d10) AddDecodeCandidate(result, "10:f[9,13]", BitConverter.ToSingle(bytes, 9), BitConverter.ToSingle(bytes, 13));
                if (d11) AddDecodeCandidate(result, "11:x=i/100 y=f", ReadInt32LE(bytes, 9) / 100f, BitConverter.ToSingle(bytes, 13));
                if (d12) AddDecodeCandidate(result, "12:x=f y=i/100", BitConverter.ToSingle(bytes, 9), ReadInt32LE(bytes, 13) / 100f);
            }

            return result;
        }

        private List<(string mode, float x, float y)> PointerScanCandidatesFromPayload(string payload, int maxOffset)
        {
            var result = new List<(string mode, float x, float y)>();
            var map = ParsePayloadToMap(payload);

            if (map.TryGetValue(4, out var p4) && map.TryGetValue(5, out var p5)
                && TryParseFloatInvariant(p4, out var x45) && TryParseFloatInvariant(p5, out var y45))
            {
                AddDecodeCandidate(result, "P:[4,5]", x45, y45);
            }

            if (map.TryGetValue(19, out var p19) && map.TryGetValue(25, out var p25)
                && TryParseFloatInvariant(p19, out var x1925) && TryParseFloatInvariant(p25, out var y1925))
            {
                AddDecodeCandidate(result, "P:[19,25]", x1925, y1925);
            }

            if (!map.TryGetValue(1, out var p1Raw)) return result;
            byte[] bytes = ParseByteArrayFromValueString(p1Raw);
            if (bytes.Length < 8) return result;

            int safeMax = Math.Max(4, Math.Min(maxOffset, bytes.Length - 4));

            for (int ox = 0; ox <= safeMax; ox++)
            {
                for (int oy = 0; oy <= safeMax; oy++)
                {
                    if (ox + 4 > bytes.Length || oy + 4 > bytes.Length) continue;

                    int ix = ReadInt32LE(bytes, ox);
                    int iy = ReadInt32LE(bytes, oy);

                    AddDecodeCandidate(result, $"I/1e7 [{ox},{oy}]", ix / 10_000_000f, iy / 10_000_000f);
                    AddDecodeCandidate(result, $"I/1e6 [{ox},{oy}]", ix / 1_000_000f, iy / 1_000_000f);
                    AddDecodeCandidate(result, $"I/1e5 [{ox},{oy}]", ix / 100_000f, iy / 100_000f);
                    AddDecodeCandidate(result, $"I/100 [{ox},{oy}]", ix / 100f, iy / 100f);

                    if (ox + 4 <= bytes.Length && oy + 4 <= bytes.Length)
                    {
                        try
                        {
                            AddDecodeCandidate(result, $"F [{ox},{oy}]", BitConverter.ToSingle(bytes, ox), BitConverter.ToSingle(bytes, oy));
                            AddDecodeCandidate(result, $"XF/YI [{ox},{oy}]", BitConverter.ToSingle(bytes, ox), iy / 100f);
                            AddDecodeCandidate(result, $"XI/YF [{ox},{oy}]", ix / 100f, BitConverter.ToSingle(bytes, oy));
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return result;
        }

        #region UI nin şekilleri vs.
        private void ApplyModernStyle()
        {
            Nightwatch.UserControls.MentalityTheme.Setup();
            
            var style = ImGui.GetStyle();
            
            // Core colors from MentalityTheme mapping to ImGui
            style.Colors[(int)ImGuiCol.WindowBg] = Nightwatch.UserControls.MentalityTheme.Colors.Background;
            style.Colors[(int)ImGuiCol.ChildBg] = Nightwatch.UserControls.MentalityTheme.Colors.Card;
            style.Colors[(int)ImGuiCol.PopupBg] = Nightwatch.UserControls.MentalityTheme.Colors.Sidebar;
            
            style.Colors[(int)ImGuiCol.TitleBg] = Nightwatch.UserControls.MentalityTheme.Colors.Background;
            style.Colors[(int)ImGuiCol.TitleBgActive] = Nightwatch.UserControls.MentalityTheme.Colors.Background;
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = Nightwatch.UserControls.MentalityTheme.Colors.Background;
            
            style.Colors[(int)ImGuiCol.Border] = Nightwatch.UserControls.MentalityTheme.Colors.Border;
            
            style.Colors[(int)ImGuiCol.FrameBg] = Nightwatch.UserControls.MentalityTheme.Colors.InputBg;
            style.Colors[(int)ImGuiCol.FrameBgHovered] = Nightwatch.UserControls.MentalityTheme.Colors.InputBgHover;
            style.Colors[(int)ImGuiCol.FrameBgActive] = Nightwatch.UserControls.MentalityTheme.Colors.CardHover;
            
            style.Colors[(int)ImGuiCol.Button] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.ButtonHovered] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryLt;
            style.Colors[(int)ImGuiCol.ButtonActive] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
            
            style.Colors[(int)ImGuiCol.Header] = new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.X, Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Y, Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Z, 0.35f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.X, Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Y, Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary.Z, 0.60f);
            style.Colors[(int)ImGuiCol.HeaderActive] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
            
            style.Colors[(int)ImGuiCol.Text] = Nightwatch.UserControls.MentalityTheme.Colors.TextPrimary;
            style.Colors[(int)ImGuiCol.TextDisabled] = Nightwatch.UserControls.MentalityTheme.Colors.TextMuted;
            
            style.Colors[(int)ImGuiCol.CheckMark] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.SliderGrab] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryLt;
            
            style.Colors[(int)ImGuiCol.Separator] = Nightwatch.UserControls.MentalityTheme.Colors.Border;
            style.Colors[(int)ImGuiCol.SeparatorHovered] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimaryLt;
            style.Colors[(int)ImGuiCol.SeparatorActive] = Nightwatch.UserControls.MentalityTheme.Colors.AccentPrimary;
        }
        #endregion

        #region Tabs
        private void RenderActiveTab()
        {
            switch (_activeTab)
                    {
                        #region Resources
                        case 0: // Kaynaklar
                            // [Kaynakların haritada gösterimi, ikon boyutları ve etiketlerinin ayarlandığı sekme]
                    RenderResourcesTab();
                    break;
                #endregion

                #region Mobs and Mists
                case 1: // Mob/Mist
                    // [Haritadaki yaratıkların (Boss, Miniboss, Normal) ve gizli geçitlerin görünürlüğünü yöneten kısım]
                    RenderMobsTab();
                    break;
                #endregion

                #region Players
                case 2: // Oyuncular
                    // [Çevredeki diğer oyuncuların (düşman/dost), lonca isimlerinin, sayı analizinin ve Whitelist'in kontrol edildiği sekme]
                    RenderPlayersTab();
                    break;
                #endregion

                #region Config
                case 3: // Config
                    // [Radar ve Overlay ayarlarının kaydedilip, daha sonra farklı profiller (isim) altında tekrar yüklenmesini sağlayan konfigürasyon paneli]
                    RenderConfigTab();
                    break;
                #endregion

                #region Settings
                case 5: // Ayarlar
                    // [Radarın yakınlaştırma seviyesi, UI teması, kısayol tuşları ve dil gibi temel sistem ayarlarının yapıldığı alan]
                    RenderSettingsTab();
                    break;
                #endregion
                #region Device (Adapter & VPN)
                case 6: // Ağ Ayarları
                    // [Uygulamanın internet paketlerini yakalarken hangi ağ bağdaştırıcısını (Ethernet, Wi-Fi, VPN, PingBooster vb.) dinleyeceğinin seçildiği alan]
                    RenderDeviceTab();
                    break;
                #endregion
                #region Geliştirme Araçları
                case 4: // Dev Tools
                    // [Uygulama geliştiricileri için RAM analizleri, sahte Mob yaratma simülatörleri ve veri paketi çözücülerinin yer aldığı test alanı]
                    RenderDevToolsTab();
                    break;
                #endregion
            }
        }
        #endregion




    }
}





