using System;
using System.Collections.Generic;

namespace AlbionDataHandlers.Packets
{
    /// <summary>
    /// Packet Inspector icin son paketlerin halka tamponu (ring buffer).
    ///
    /// ONEMLI: VARSAYILAN OLARAK KAPALI (Enabled = false) -> kapaliyken
    /// hicbir ek maliyet yoktur, hicbir sozluk kopyalanmaz.
    /// DevTools sekmesindeki "Packet Inspector" anahtarindan acilir.
    ///
    /// Acikken: son MaxEntries paket, parametrelerinin BIR KOPYASI ile saklanir
    /// (parser sozlugu tekrar kullanabilir diye kopya aliyoruz).
    /// </summary>
    public static class PacketInspectorStore
    {
        public sealed class Entry
        {
            public DateTime Time;
            public int Code;
            public string Kind;                       // EVENT / OP-YANIT / OP-ISTEK
            public Dictionary<byte, object> Params;   // kopya
        }

        private const int MaxEntries = 300;

        private static readonly Queue<Entry> _entries = new Queue<Entry>();
        private static readonly object _lock = new object();

        /// <summary>Kapaliyken hicbir sey kaydedilmez (sifir maliyet).</summary>
        public static volatile bool Enabled = false;

        public static int Count
        {
            get { lock (_lock) return _entries.Count; }
        }

        public static void Add(int code, string kind, Dictionary<byte, object> parameters)
        {
            if (!Enabled || parameters == null) return;
            try
            {
                var copy = new Dictionary<byte, object>(parameters);
                lock (_lock)
                {
                    _entries.Enqueue(new Entry
                    {
                        Time = DateTime.Now,
                        Code = code,
                        Kind = kind,
                        Params = copy
                    });
                    while (_entries.Count > MaxEntries) _entries.Dequeue();
                }
            }
            catch { }
        }

        /// <summary>En eskiden en yeniye dogru kopya dondurur.</summary>
        public static Entry[] Snapshot()
        {
            lock (_lock) return _entries.ToArray();
        }

        public static void Clear()
        {
            lock (_lock) _entries.Clear();
        }
    }
}
