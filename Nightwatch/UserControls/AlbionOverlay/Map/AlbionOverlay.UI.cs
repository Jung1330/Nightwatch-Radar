#region Using Directives
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

            var matches = Regex.Matches(payload, @"\[(\d+)\]=");
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

        #region UI nin ÅŸekilleri vs.
        private void ApplyModernStyle()
        {
            var style = ImGui.GetStyle();

            // Köşe Yumuşatmaları
            style.WindowRounding = 12f;
            style.ChildRounding = 10f;
            style.FrameRounding = 8f;
            style.PopupRounding = 10f;
            style.ScrollbarRounding = 12f;
            style.GrabRounding = 8f;

            style.WindowBorderSize = 1f;
            style.ChildBorderSize = 1f;
            style.FrameBorderSize = 0f;

            // ==========================================
            // 2. ARKA PLAN VE BAŞLIK EŞİTLEMESİ (Kusursuz Görünüm)
            // ==========================================
            // Senin verdiğin RGB: 1, 2, 3 (İçi ve başlığı aynı renk yapıyoruz ki çizgi olmasın)
            Vector4 mainBgColor = new Vector4(1f / 255f, 2f / 255f, 3f / 255f, 0.98f); // 0.98f hafif saydamlık

            style.Colors[(int)ImGuiCol.WindowBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.ChildBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.PopupBg] = mainBgColor;

            // BAŞLIK ÇUBUĞUNU (TASI BENI YAZAN YERİ) GİZLEYEN SİHİRLİ KISIM:
            style.Colors[(int)ImGuiCol.TitleBg] = mainBgColor;
            style.Colors[(int)ImGuiCol.TitleBgActive] = mainBgColor;
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = mainBgColor;

            // Çerçeve (Border) rengini ana renkten çok hafif daha açık yapıyoruz ki tatlı bir sınırı olsun
            style.Colors[(int)ImGuiCol.Border] = new Vector4(35f / 255f, 38f / 255f, 45f / 255f, 1.0f);

            // Frame'ler (Kutucuklar, ComboBox'lar, alt planlar)
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(30f / 255f, 33f / 255f, 40f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(40f / 255f, 44f / 255f, 52f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(45f / 255f, 49f / 255f, 58f / 255f, 1.0f);

            // ==========================================
            // 3. ANA VURGU RENGİ (Accent Color)
            // ==========================================
            // Midnight mor vurgu rengi
            Vector4 accentColor = new Vector4(92f / 255f, 40f / 255f, 120f / 255f, 1.0f);
            Vector4 accentHover = new Vector4(118f / 255f, 62f / 255f, 150f / 255f, 1.0f);
            Vector4 accentActive = new Vector4(72f / 255f, 30f / 255f, 98f / 255f, 1.0f);
            Vector4 accentMuted = new Vector4(accentColor.X, accentColor.Y, accentColor.Z, 0.35f);

            // Butonlar, Sekmeler ve Sliderlar
            style.Colors[(int)ImGuiCol.Button] = accentColor;
            style.Colors[(int)ImGuiCol.ButtonHovered] = accentHover;
            style.Colors[(int)ImGuiCol.ButtonActive] = accentActive;

            style.Colors[(int)ImGuiCol.Header] = accentMuted;
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(accentColor.X, accentColor.Y, accentColor.Z, 0.60f);
            style.Colors[(int)ImGuiCol.HeaderActive] = accentColor;

            // Sekme (Tab) renkleri - Midnight mor (seçili daha açık)
            Vector4 tabMidnight = new Vector4(140f / 255f, 85f / 255f, 175f / 255f, 1.0f);
            Vector4 tabMidnightHover = new Vector4(165f / 255f, 110f / 255f, 195f / 255f, 1.0f);
            Vector4 tabMidnightActive = new Vector4(185f / 255f, 130f / 255f, 210f / 255f, 1.0f);
            style.Colors[(int)ImGuiCol.Tab] = tabMidnight;
            style.Colors[(int)ImGuiCol.TabHovered] = tabMidnightHover;

            // ImGuiCol enumunda TabActive yoksa index ile güvenli şekilde ayarla
            int tabActiveIndex = (int)ImGuiCol.TabHovered + 1;
            if (style.Colors.Count > tabActiveIndex)
            {
                style.Colors[tabActiveIndex] = tabMidnightActive;

                int tabUnfocusedIndex = tabActiveIndex + 1;
                if (style.Colors.Count > tabUnfocusedIndex)
                {
                    style.Colors[tabUnfocusedIndex] = tabMidnight;
                }

                int tabUnfocusedActiveIndex = tabUnfocusedIndex + 1;
                if (style.Colors.Count > tabUnfocusedActiveIndex)
                {
                    style.Colors[tabUnfocusedActiveIndex] = tabMidnight;
                }
            }

            style.Colors[(int)ImGuiCol.CheckMark] = accentColor;
            style.Colors[(int)ImGuiCol.SliderGrab] = accentColor;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = accentActive;

            style.Colors[(int)ImGuiCol.Separator] = style.Colors[(int)ImGuiCol.Border];
            style.Colors[(int)ImGuiCol.SeparatorHovered] = accentHover;
            style.Colors[(int)ImGuiCol.SeparatorActive] = accentActive;

            // ==========================================
            // 4. METİN RENKLERİ
            // ==========================================
            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.92f, 0.92f, 0.95f, 1.00f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.55f, 1.00f);
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


