using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AlbionDataHandlers.Packets;
using ImGuiNET;

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        // ============================================================
        // PACKET INSPECTOR
        // ------------------------------------------------------------
        // Gelen paketleri OKUNABILIR sekilde gosterir:
        //   [kod] NewLootChest  ->  ChestId = 2149 · Position = [x, y] · Rarity = 6
        //
        // Alan adlari: uretilen paket modellerinden (AlbionDataHandlers.Packets.*)
        //              reflection ile okunur -> elle guncelleme gerekmez.
        // Anlamlar   : PacketMeta.Meanings (uretilen tablo, canli dogrulananlar [CANLI])
        //
        // Veri kaynagi: AlbionDataHandlers.Packets.PacketInspectorStore
        //   (VARSAYILAN KAPALI -> kapaliyken sifir maliyet)
        // ============================================================

        private bool _piEnabled;
        private bool _piPaused;
        private bool _piOnlyKnown = true;
        private int _piSelected = -1;
        private string _piFilter = string.Empty;
        private string _piRecordFile = string.Empty;
        private DateTime _piLastRefresh = DateTime.MinValue;
        private PacketInspectorStore.Entry[] _piFrozen = Array.Empty<PacketInspectorStore.Entry>();

        private static Dictionary<int, Type> _piTypes;
        private static Dictionary<int, FieldInfo[]> _piFields;

        private static readonly Vector4 PI_GREEN = new Vector4(0.40f, 1f, 0.60f, 1f);
        private static readonly Vector4 PI_BLUE = new Vector4(0.60f, 0.80f, 1f, 1f);
        private static readonly Vector4 PI_YELLOW = new Vector4(0.95f, 0.85f, 0.35f, 1f);

        /// <summary>Uretilen paket modellerini (kod -> tip / alanlar) bir kez indeksler.</summary>
        private static void EnsurePiTypeIndex()
        {
            if (_piTypes != null) return;

            var map = new Dictionary<int, Type>();
            var fmap = new Dictionary<int, FieldInfo[]>();
            try
            {
                Assembly asm = typeof(PacketInspectorStore).Assembly;
                foreach (Type t in asm.GetTypes())
                {
                    if (t.Namespace == null) continue;
                    if (!t.Namespace.StartsWith("AlbionDataHandlers.Packets", StringComparison.Ordinal)) continue;

                    FieldInfo cf = t.GetField("Code", BindingFlags.Public | BindingFlags.Static);
                    if (cf == null) continue;

                    int code;
                    try { code = Convert.ToInt32(cf.GetRawConstantValue()); }
                    catch { continue; }

                    if (!map.ContainsKey(code)) map[code] = t;
                    fmap[code] = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch { }

            _piTypes = map;
            _piFields = fmap;
        }

        private static string PiPacketName(int code)
        {
            if (_piTypes != null && _piTypes.TryGetValue(code, out Type t) && t != null)
            {
                FieldInfo nf = t.GetField("PacketName", BindingFlags.Public | BindingFlags.Static);
                if (nf != null)
                {
                    try { return (string)nf.GetRawConstantValue(); } catch { }
                }
            }
            return "?";
        }

        private static string PiFieldName(int code, int index)
        {
            if (_piFields != null && _piFields.TryGetValue(code, out FieldInfo[] fs) && fs != null
                && index >= 0 && index < fs.Length)
                return fs[index].Name;
            return "F" + index;
        }

        private static string PiMeaning(int code, int index)
        {
            try
            {
                if (PacketMeta.Meanings.TryGetValue(code, out Dictionary<int, string> m) && m != null
                    && m.TryGetValue(index, out string s))
                    return s;
            }
            catch { }
            return string.Empty;
        }

        private static string PiValue(object v)
        {
            if (v == null) return "(null)";
            if (v is byte[] bytes) return "byte[" + bytes.Length + "]";
            if (v is float[] floats) return "[" + string.Join(", ", floats.Take(6).Select(x => x.ToString("0.###"))) + "]";
            if (v is int[] ints) return "[" + string.Join(", ", ints.Take(8)) + "]";
            if (v is string str) return str;
            if (v is System.Collections.IList list) return "List[" + list.Count + "]";
            return v.ToString();
        }

        private void RenderPacketInspectorTab()
        {
            EnsurePiTypeIndex();

            // ---------------- ust bar ----------------
            bool en = _piEnabled;
            if (ImGui.Checkbox("Kaydet", ref en))
            {
                _piEnabled = en;
                PacketInspectorStore.Enabled = en;
            }
            ImGui.SameLine();
            ImGui.Checkbox("Dondur", ref _piPaused);
            ImGui.SameLine();
            if (ImGui.Button("Temizle"))
            {
                PacketInspectorStore.Clear();
                _piFrozen = Array.Empty<PacketInspectorStore.Entry>();
                _piSelected = -1;
            }
            ImGui.SameLine();
            bool rec = PacketRecorder.IsRecording;
            if (ImGui.Checkbox("Diske kaydet (JSONL)", ref rec))
            {
                if (rec) _piRecordFile = PacketRecorder.Start();
                else PacketRecorder.Stop();
            }
            if (PacketRecorder.IsRecording)
            {
                ImGui.SameLine();
                ImGui.TextColored(PI_GREEN, string.Format("kaydediliyor: {0} paket", PacketRecorder.WrittenCount));
            }
            if (!string.IsNullOrEmpty(_piRecordFile))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("-> " + System.IO.Path.GetFileName(_piRecordFile));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Oynatmak icin:\nNightwatch.exe --replay \"" + _piRecordFile + "\"");
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##piFilter", "kod veya isim filtrele...", ref _piFilter, 64);
            ImGui.SameLine();
            ImGui.Checkbox("Sadece bilinen kodlar", ref _piOnlyKnown);

            if (!_piEnabled)
            {
                ImGui.TextColored(PI_YELLOW,
                    "KAPALI. 'Kaydet' isaretlenince paketler listelenir. (Kapaliyken hicbir ek maliyet yok.)");
            }

            // ---------------- anlik goruntu (4/sn yenilenir) ----------------
            if (!_piPaused && (DateTime.UtcNow - _piLastRefresh).TotalMilliseconds >= 250)
            {
                _piLastRefresh = DateTime.UtcNow;
                try { _piFrozen = PacketInspectorStore.Snapshot(); } catch { }
            }

            PacketInspectorStore.Entry[] list = _piFrozen ?? Array.Empty<PacketInspectorStore.Entry>();
            string filt = (_piFilter ?? string.Empty).Trim().ToLowerInvariant();

            // ---------------- sol: paket listesi ----------------
            ImGui.BeginChild("piList", new Vector2(360, 0), ImGuiChildFlags.Borders);
            int shown = 0;
            for (int i = list.Length - 1; i >= 0; i--)
            {
                PacketInspectorStore.Entry e = list[i];
                if (e == null) continue;

                string pname = PiPacketName(e.Code);
                if (_piOnlyKnown && pname == "?") continue;
                if (filt.Length > 0
                    && !e.Code.ToString().Contains(filt)
                    && !pname.ToLowerInvariant().Contains(filt))
                    continue;

                string label = string.Format("{0:HH:mm:ss.fff}  [{1}]  {2}##pi{3}", e.Time, e.Code, pname, i);
                if (ImGui.Selectable(label, _piSelected == i)) _piSelected = i;
                shown++;
            }
            if (shown == 0)
                ImGui.TextDisabled(_piEnabled ? "Henuz paket yok..." : "Kayit kapali.");
            ImGui.EndChild();

            ImGui.SameLine();

            // ---------------- sag: alan detayi ----------------
            ImGui.BeginChild("piDetail", new Vector2(0, 0), ImGuiChildFlags.Borders);

            if (_piSelected >= 0 && _piSelected < list.Length && list[_piSelected] != null)
            {
                PacketInspectorStore.Entry e = list[_piSelected];

                ImGui.TextColored(PI_GREEN, string.Format("[{0}] {1}", e.Code, PiPacketName(e.Code)));
                ImGui.SameLine();
                ImGui.TextDisabled(string.Format("· {0} · {1:HH:mm:ss.fff}", e.Kind, e.Time));

                int modelFields = -1;
                if (_piFields != null && _piFields.TryGetValue(e.Code, out FieldInfo[] fs) && fs != null)
                    modelFields = fs.Length;

                if (modelFields >= 0)
                {
                    int got = e.Params?.Count ?? 0;
                    ImGui.TextColored(PI_YELLOW, string.Format(
                        "Model: {0} alan · telde gelen: {1} · eksik: {2}   (Photon null alanlari hic gondermez)",
                        modelFields, got, Math.Max(0, modelFields - got)));
                }
                else
                {
                    ImGui.TextColored(PI_YELLOW, "Bu kod icin uretilmis model yok (yalnizca ham alanlar).");
                }

                ImGui.Separator();

                if (ImGui.BeginTable("piFieldsTbl", 4,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupColumn("indeks", ImGuiTableColumnFlags.WidthFixed, 55);
                    ImGui.TableSetupColumn("alan", ImGuiTableColumnFlags.WidthFixed, 190);
                    ImGui.TableSetupColumn("deger", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("anlam", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();

                    if (e.Params != null)
                    {
                        foreach (KeyValuePair<byte, object> kv in e.Params.OrderBy(k => k.Key))
                        {
                            if (kv.Key == 252 || kv.Key == 253 || kv.Key == 254) continue; // protokol alanlari

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(kv.Key.ToString());

                            ImGui.TableNextColumn();
                            ImGui.TextColored(PI_BLUE, PiFieldName(e.Code, kv.Key));

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(PiValue(kv.Value));

                            ImGui.TableNextColumn();
                            ImGui.TextDisabled(PiMeaning(e.Code, kv.Key));
                        }
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.TextDisabled("Soldan bir paket sec -> alanlari isimleriyle/anlamlariyla gor.");
            }

            ImGui.EndChild();
        }
    }
}
