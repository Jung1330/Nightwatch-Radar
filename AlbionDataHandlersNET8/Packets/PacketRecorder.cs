using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbionDataHandlers.Packets
{
    /// <summary>
    /// Gelen paketleri diske kaydeder (JSONL) -> sonra --replay ile OYUNSUZ oynatilir.
    ///
    /// NEDEN: her test icin oyuna girmek zorunda kalmayalim. Bir kez kaydet,
    /// sonra sinirsiz kez tekrar oynat (parser + handler davranisini dogrula).
    ///
    /// VARSAYILAN: KAPALI. Kapaliyken hicbir sey yapmaz (sifir maliyet).
    /// Acikken: yazma islemi AYRI THREAD'de yapilir, paket thread'i beklemez.
    /// </summary>
    public static class PacketRecorder
    {
        private static BlockingCollection<string> _queue =
            new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);

        private static Thread _writer;
        private static volatile bool _recording;
        private static string _file;
        private static long _written;
        private static long _dropped;

        public static bool IsRecording => _recording;
        public static string CurrentFile => _file;
        public static long WrittenCount => Interlocked.Read(ref _written);
        public static long DroppedCount => Interlocked.Read(ref _dropped);

        /// <summary>Kaydi baslatir; olusan dosya yolunu dondurur.</summary>
        public static string Start(string directory = null)
        {
            if (_recording) return _file;

            try
            {
                // Yeniden kayit icin kuyrugu tazele (CompleteAdding sonrasi tekrar kullanilamaz)
                _queue = new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);
                _written = 0;
                _dropped = 0;
                if (string.IsNullOrEmpty(directory))
                    directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
                Directory.CreateDirectory(directory);

                _file = Path.Combine(directory,
                    "packets_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl");

                _recording = true;
                _writer = new Thread(WriterLoop) { IsBackground = true, Name = "PacketRecorder" };
                _writer.Start();
                return _file;
            }
            catch
            {
                _recording = false;
                return null;
            }
        }

        public static void Stop()
        {
            _recording = false;
            try { _queue.CompleteAdding(); } catch { }
        }

        public static void Add(int code, string kind, Dictionary<byte, object> parameters)
        {
            if (!_recording || parameters == null) return;

            try
            {
                var obj = new JObject
                {
                    ["t"] = DateTime.Now.ToString("HH:mm:ss.fff"),
                    ["code"] = code,
                    ["kind"] = kind ?? "EVENT"
                };

                var p = new JObject();
                foreach (var kv in parameters)
                {
                    if (kv.Key == 252 || kv.Key == 253) continue; // kod alanlari zaten code'da
                    p[kv.Key.ToString()] = Encode(kv.Value);
                }
                obj["p"] = p;

                string line = obj.ToString(Formatting.None);
                if (!_queue.TryAdd(line)) Interlocked.Increment(ref _dropped);
            }
            catch { }
        }

        private static void WriterLoop()
        {
            try
            {
                using (var sw = new StreamWriter(_file, append: false))
                {
                    foreach (string line in _queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            sw.WriteLine(line);
                            Interlocked.Increment(ref _written);
                            if (_written % 200 == 0) sw.Flush();
                        }
                        catch { }
                    }
                    sw.Flush();
                }
            }
            catch { }
        }

        /// <summary>Photon degerlerini kayipsiz tasiyacak sekilde JSON'a cevirir.</summary>
        private static JToken Encode(object v)
        {
            if (v == null) return JValue.CreateNull();
            if (v is bool b) return new JValue(b);
            if (v is string s) return new JValue(s);
            if (v is byte by) return new JValue((long)by);
            if (v is sbyte sb) return new JValue((long)sb);
            if (v is short sh) return new JValue((long)sh);
            if (v is ushort us) return new JValue((long)us);
            if (v is int i) return new JValue((long)i);
            if (v is long l) return new JValue(l);
            if (v is float f) return new JValue((double)f);
            if (v is double d) return new JValue(d);

            if (v is byte[] bytes) return new JObject { ["__b64"] = Convert.ToBase64String(bytes) };

            if (v is float[] fa) return new JObject { ["__f32"] = new JArray(fa.Select(x => (double)x)) };
            if (v is double[] da) return new JObject { ["__f64"] = new JArray(da) };
            if (v is int[] ia) return new JObject { ["__i32"] = new JArray(ia.Select(x => (long)x)) };
            if (v is short[] sa) return new JObject { ["__i16"] = new JArray(sa.Select(x => (long)x)) };
            if (v is long[] la) return new JObject { ["__i64"] = new JArray(la) };

            if (v is System.Collections.IDictionary dict)
            {
                var o = new JObject();
                foreach (System.Collections.DictionaryEntry de in dict)
                    o[de.Key?.ToString() ?? "?"] = Encode(de.Value);
                return new JObject { ["__map"] = o };
            }

            if (v is System.Collections.IList list)
            {
                var arr = new JArray();
                foreach (object item in list) arr.Add(Encode(item));
                return new JObject { ["__list"] = arr };
            }

            return new JValue(v.ToString());
        }

        /// <summary>--replay tarafi: kaydedilmis JSON degerini Photon tipine geri cevirir.</summary>
        public static object Decode(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;

            if (t is JObject o)
            {
                if (o["__b64"] != null) return Convert.FromBase64String((string)o["__b64"]);
                if (o["__f32"] != null) return o["__f32"].Select(x => (float)x).ToArray();
                if (o["__f64"] != null) return o["__f64"].Select(x => (double)x).ToArray();
                if (o["__i32"] != null) return o["__i32"].Select(x => (int)x).ToArray();
                if (o["__i16"] != null) return o["__i16"].Select(x => (short)x).ToArray();
                if (o["__i64"] != null) return o["__i64"].Select(x => (long)x).ToArray();
                if (o["__list"] != null)
                {
                    var list = new List<object>();
                    foreach (var item in o["__list"]) list.Add(Decode(item));
                    return list;
                }
                if (o["__map"] != null)
                {
                    var map = new Dictionary<object, object>();
                    foreach (var prop in ((JObject)o["__map"]).Properties())
                        map[prop.Name] = Decode(prop.Value);
                    return map;
                }
            }

            switch (t.Type)
            {
                case JTokenType.Boolean: return (bool)t;
                case JTokenType.String: return (string)t;
                case JTokenType.Integer:
                    {
                        long lv = (long)t;
                        if (lv >= int.MinValue && lv <= int.MaxValue) return (int)lv;
                        return lv;
                    }
                case JTokenType.Float: return (float)(double)t;
            }

            return t.ToString();
        }
    }
}
