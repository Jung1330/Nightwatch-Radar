using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbionDataHandlers.Packets
{
    /// <summary>
    /// EKSIK KOD YAKALAYICI (catcher)
    /// ------------------------------------------------------------------
    /// NEDEN: Bazi event kodlari HIC kaydimizda gorunmedi (asagidaki liste).
    /// Oyunda denk gelirse kacirmayalim: paket geldigi anda diske yazilir.
    ///
    /// NASIL KULLANILIR:
    ///   1) Uygulamayi normal kullan.
    ///   2) Uygulama klasorunde  log\catcher\  altina bak:
    ///        - catch_555_NewHuntTrack.jsonl  gibi dosya OLUSTUYSA -> o kod geldi demektir.
    ///        - her satir: {"t":"...","code":555,"kind":"EVENT","p":{...}}  (PacketRecorder ile ayni bicim)
    ///   3) Dosyayi bana ver -> icindeki alanlara gore model/handler yazariz.
    ///
    /// MALIYET: Izlenen kod listesi kucuk; diger paketlerde hicbir is yapilmaz (Contains kontrolu).
    /// </summary>
    public static class MissingCodeCatcher
    {
        /// <summary>Hic kaydimizda gorunmeyen, yakalanmasi istenen kodlar (kullanici karari).</summary>
        private static readonly HashSet<int> Watched = new HashSet<int>
        {
            38, 251, 323, 355, 359, 360, 392, 396, 401, 402, 449, 483, 528, 529, 555, 556, 558, 603, 638, 639
        };

        private static readonly object _lock = new object();
        private static string _dir;
        private static readonly HashSet<int> _seen = new HashSet<int>();

        public static bool IsWatched(int code) => Watched.Contains(code);

        /// <summary>Izlenen bir kod geldiyse diske yazar. Digerlerinde hicbir sey yapmaz.</summary>
        public static void Capture(int code, string kind, Dictionary<byte, object> parameters)
        {
            if (!Watched.Contains(code) || parameters == null) return;
            if (!AlbionDataHandlers.Utils.PacketLogSettings.AllLogInfo) return;   // AllLogInfo kapaliysa dosya yazilmaz

            try
            {
                if (_dir == null)
                    _dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", "catcher");
                Directory.CreateDirectory(_dir);

                string safeKind = string.IsNullOrEmpty(kind) ? "EVENT" : kind.Replace('/', '_');
                string path = Path.Combine(_dir, string.Format(CultureInfo.InvariantCulture, "catch_{0}_{1}.jsonl", code, safeKind));

                var obj = new JObject
                {
                    ["t"] = DateTime.Now.ToString("HH:mm:ss.fff"),
                    ["code"] = code,
                    ["kind"] = safeKind,
                    ["utc"] = DateTime.UtcNow.ToString("o")
                };
                var p = new JObject();
                foreach (var kv in parameters)
                {
                    if (kv.Key == 252 || kv.Key == 253) continue;
                    p[kv.Key.ToString(CultureInfo.InvariantCulture)] = Encode(kv.Value);
                }
                obj["p"] = p;
                string line = obj.ToString(Formatting.None);

                lock (_lock)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                    if (_seen.Add(code))
                    {
                        // Konsola tek satir iz (uygulama ici Console'da gorunur)
                        try { Console.WriteLine($"[CATCH] {code} {safeKind} yakalandi -> log\\catcher\\{Path.GetFileName(path)}"); } catch { }
                    }
                }
            }
            catch { /* yakalayici asla akisi bozmaz */ }
        }

        /// <summary>PacketRecorder ile ayni kodlama: byte[] -> base64, IList -> dizi, digerleri ham.</summary>
        private static JToken Encode(object v)
        {
            if (v == null) return JValue.CreateNull();
            if (v is byte[] bytes) return new JObject { ["__b64"] = Convert.ToBase64String(bytes) };
            if (v is float[] f) return new JObject { ["__f32"] = new JArray(f.Select(x => (double)x)) };
            if (v is double[] d) return new JObject { ["__f64"] = new JArray(d) };
            if (v is int[] i) return new JObject { ["__i32"] = new JArray(i.Select(x => (long)x)) };
            if (v is short[] s) return new JObject { ["__i16"] = new JArray(s.Select(x => (long)x)) };
            if (v is long[] l) return new JObject { ["__i64"] = new JArray(l) };
            if (v is string[] sa) return new JObject { ["__str"] = new JArray(sa) };
            if (v is System.Collections.IDictionary map)
            {
                var o = new JObject();
                foreach (System.Collections.DictionaryEntry e in map)
                    o[Convert.ToString(e.Key, CultureInfo.InvariantCulture)] = Encode(e.Value);
                return new JObject { ["__map"] = o };
            }
            if (v is System.Collections.IEnumerable en && !(v is string))
            {
                var arr = new JArray();
                foreach (var x in en) arr.Add(Encode(x));
                return new JObject { ["__list"] = arr };
            }
            if (v is bool b) return new JValue(b);
            if (v is string str) return new JValue(str);
            if (v is float fl) return new JValue((double)fl);
            if (v is double db) return new JValue(db);
            if (v is sbyte || v is byte || v is short || v is ushort || v is int || v is uint || v is long || v is ulong)
                return new JValue(Convert.ToInt64(v, CultureInfo.InvariantCulture));
            return new JValue(v.ToString());
        }
    }
}
