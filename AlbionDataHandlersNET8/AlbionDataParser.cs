using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Utils;
using BaseUtils.Logger.Impl;
using PhotonPackageParser;
using System.Collections.Generic;
using System.Text;

namespace AlbionDataHandlers
{
    public class AlbionDataParser : PhotonParser18
    {
        private readonly List<IEventHandler> _eventHandlers = new List<IEventHandler>();
        private readonly object _handlerLock = new object();

        public AlbionDataParser()
        {
            UseProtocol18 = true;
            Debug = false;
        }

        // ==================== TANILAMA (canlı doğrulama) ====================
        // Amaç: oyunun CANLI olarak hangi event/operasyon kodlarını gönderdiğini görmek.
        //
        // 1) event_codes_seen.log : her kod oturumda BİR kez yazılır (enum'daki adıyla).
        //       Örn:  "EVENT 393 = NewLootChest"      -> enum ile UYUŞUYOR
        //             "EVENT 391 = TriggerHintBox"    -> enum ile uyuşuyor ama chest bekliyorsan ŞÜPHELİ
        // 2) event_watch.log : WATCH_CODES listesindeki paketlerin TÜM parametreleri
        //       (indeks + tip + değer) yazılır. Kod başına en fazla WATCH_MAX_PER_CODE kez.
        //
        // Dosyalar exe'nin yanında oluşur (bin\Debug veya bin\Release klasörü).
        private static readonly HashSet<string> _seenCodes = new HashSet<string>();
        private static readonly Dictionary<int, int> _watchCount = new Dictionary<int, int>();
        private const int WATCH_MAX_PER_CODE = 5;

        // İzlemek istediğin kodları buraya yaz:
        // 393 = NewLootChest · 312 = NewMountObject · 211 = Mounted · 3 = Move
        private static readonly int[] WATCH_CODES = { 393, 312, 211, 3 };

        // Enum'un alt tipi short da olabilir int de -> doğru tipi seçip isim bulur.
        // NOT: RequestCodes / ResponseCodes bilerek ALT KUME'dir; bu yuzden op kodlari
        // orada bulunamayip "ENUM'DA YOK!" gorunuyordu. Artik bulunamazsa oyunun
        // TAM listesi olan OperationCodes'tan isim aranir.
        private static string CodeName(int code, Type enumType)
        {
            try
            {
                string name = CodeNameIn(code, enumType);
                if (name != null) return name;

                name = CodeNameIn(code, typeof(OperationCodes));
                if (name != null) return name;

                return "ENUM'DA YOK!";
            }
            catch { return "?"; }
        }

        private static string CodeNameIn(int code, Type enumType)
        {
            object val = Enum.GetUnderlyingType(enumType) == typeof(short)
                ? (object)(short)code
                : (object)code;

            return Enum.IsDefined(enumType, val)
                ? (Enum.GetName(enumType, val) ?? "?")
                : null;
        }

        private static void LogSeenCode(int code, string kind, Type enumType)
        {
            if (code <= 0) return;
            lock (_seenCodes)
            {
                if (!_seenCodes.Add(kind + ":" + code)) return;
            }

            string name = CodeName(code, enumType);

            string line = $"[{DateTime.Now:HH:mm:ss}] {kind} {code} = {name}";
            try { Console.WriteLine(line); } catch { }
            if (Utils.PacketLogSettings.AllLogInfo)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "event_codes_seen.log"),
                        line + Environment.NewLine);
                }
                catch { }
            }
        }

        private static void DumpWatched(int code, Dictionary<byte, object> parameters)
        {
            if (parameters == null) return;
            if (Array.IndexOf(WATCH_CODES, code) < 0) return;

            lock (_watchCount)
            {
                _watchCount.TryGetValue(code, out int n);
                if (n >= WATCH_MAX_PER_CODE) return;
                _watchCount[code] = n + 1;
            }

            string eventName = CodeName(code, typeof(EventCodes));

            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] EVENT {code} = {eventName}   (parametre sayisi: {parameters.Count})");
            foreach (var kv in parameters)
            {
                object v = kv.Value;
                string s;
                if (v == null) s = "";
                else if (v is System.Collections.IList list) s = $"List[{list.Count}] ({string.Join(", ", list.Cast<object>().Take(6))})";
                else s = v.ToString();

                string tip = v == null ? "null" : v.GetType().Name;
                sb.AppendLine($"  [{kv.Key}] = {s}   <{tip}>");
            }
            sb.AppendLine("--------------------------------------------------");

            if (Utils.PacketLogSettings.AllLogInfo)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "event_watch.log"),
                        sb.ToString());
                }
                catch { }
            }
        }
        // ===================================================================

        // --- YENİ EKLENEN GÜVENLİK DUVARI (ŞİFRELİ PAKETLERİ ATLATMA) ---
        public new void ReceivePacket(byte[] payload)
        {
            // 1. Paket geçersizse veya çok küçükse atla
            if (payload == null || payload.Length < 12)
                return;

            // 2. Go referansındaki gibi Flag kontrolü (3. byte yani Offset 2)
            byte flags = payload[2];

            // 3. Sifreli bayrakli paketler de islenir (konum verisi icin gerekli).

            // 4. Şifresizse normal parser'a (base class) gönder, okumaya ve radara çizmeye devam etsin!
            base.ReceivePacket(payload);
        }
        // ----------------------------------------------------------------

        public void RegisterEventHandler(IEventHandler handler)
        {
            if (handler == null) return;
            lock (_handlerLock)
            {
                if (!_eventHandlers.Contains(handler))
                    _eventHandlers.Add(handler);
            }
        }

        public void UnregisterEventHandler(IEventHandler handler)
        {
            if (handler == null) return;
            lock (_handlerLock)
            {
                _eventHandlers.Remove(handler);
            }
        }

        private IEventHandler[] SnapshotHandlers()
        {
            lock (_handlerLock)
            {
                return _eventHandlers.ToArray();
            }
        }

        public void InjectParsedEvent(byte code, Dictionary<byte, object> parameters)
        {
            OnEvent(code, parameters);
        }

        public void InjectParsedRequest(byte code, Dictionary<byte, object> parameters)
        {
            OnRequest(code, parameters);
        }

        public void InjectParsedResponse(byte code, Dictionary<byte, object> parameters)
        {
            OnResponse(code, 0, string.Empty, parameters);
        }

        protected override void OnEvent(byte code, Dictionary<byte, object> parameters)
        {
            int integerCode;
            if (code == 1 && parameters.TryGetValue(252, out var val) && val != null)
            {
                if (int.TryParse(val.ToString(), out int parsedVal))
                {
                    integerCode = parsedVal;
                }
                else
                {
                    return;
                }
            }
            else
            {
                integerCode = code;
            }

            EventCodes eventCode;
            try
            {
                eventCode = (EventCodes)integerCode;
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 0 | {ex.Message}");
                return;
            }

            foreach (var handler in SnapshotHandlers())
            {
                try
                {
                    handler.OnEvent(eventCode, parameters);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 66 | {ex.Message}");
                }
            }

            // --- CANLI TANILAMA ---
            LogSeenCode(integerCode, "EVENT", typeof(EventCodes));
            DumpWatched(integerCode, parameters);
            Packets.PacketInspectorStore.Add(integerCode, "EVENT", parameters);
            Packets.PacketRecorder.Add(integerCode, "EVENT", parameters);
            // EKSIK KOD YAKALAYICI: kayitlarimizda hic gorunmeyen kodlar gelirse diske yazar
            Packets.MissingCodeCatcher.Capture(integerCode, "EVENT", parameters);
            // ----------------------

            try { PlayerParserTraceStore.CaptureEvent(eventCode, parameters); } catch { }
        }

        // OnRequest ve OnResponse kısımları aynı kalabilir (Zaten çalışıyordu)
        protected override void OnRequest(byte operationCode, Dictionary<byte, object> parameters)
        {
            int integerCode = operationCode;
            if (parameters.TryGetValue(253, out var val) && val != null)
                if (int.TryParse(val.ToString(), out int parsedVal)) integerCode = parsedVal;

            RequestCodes requestCode;
            try { requestCode = (RequestCodes)integerCode; }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 1 | {ex.Message}");
                return;
            }
            foreach (var handler in SnapshotHandlers())
            {
                try
                {
                    handler.OnRequest(requestCode, parameters);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 67 | {ex.Message}");
                }
            }

            // --- CANLI TANILAMA ---
            LogSeenCode(integerCode, "OP-ISTEK", typeof(RequestCodes));
            DumpWatched(integerCode, parameters);
            Packets.PacketInspectorStore.Add(integerCode, "OP-ISTEK", parameters);
            Packets.PacketRecorder.Add(integerCode, "OP-ISTEK", parameters);
            Packets.MissingCodeCatcher.Capture(integerCode, "OP-ISTEK", parameters);   // eksik kod yakalayici
            // ----------------------

            try { PlayerParserTraceStore.CaptureRequest(requestCode, parameters); } catch { }
        }

        protected override void OnResponse(byte operationCode, short returnCode, string debugMessage, Dictionary<byte, object> parameters)
        {
            int integerCode = operationCode;
            if (parameters.TryGetValue(253, out var val) && val != null)
                if (int.TryParse(val.ToString(), out int parsedVal)) integerCode = parsedVal;

            ResponseCodes eventCode;
            try { eventCode = (ResponseCodes)integerCode; }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 2 | {ex.Message}");
                return;
            }
            foreach (var handler in SnapshotHandlers())
            {
                try
                {
                    handler.OnResponse(eventCode, parameters);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 68 | {ex.Message}");
                }
            }

            // --- CANLI TANILAMA ---
            LogSeenCode(integerCode, "OP-YANIT", typeof(ResponseCodes));
            DumpWatched(integerCode, parameters);
            Packets.PacketInspectorStore.Add(integerCode, "OP-YANIT", parameters);
            Packets.PacketRecorder.Add(integerCode, "OP-YANIT", parameters);
            Packets.MissingCodeCatcher.Capture(integerCode, "OP-YANIT", parameters);   // eksik kod yakalayici
            // ----------------------

            try { PlayerParserTraceStore.CaptureResponse(eventCode, parameters); } catch { }
        }
    }
}


