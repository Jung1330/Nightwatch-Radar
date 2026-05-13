using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Utils;
using BaseUtils.Logger.Impl;
using PhotonPackageParser;
using System.Collections.Generic;

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

        // --- YENÝ EKLENEN GÜVENLÝK DUVARI (ÞÝFRELÝ PAKETLERÝ ATLATMA) ---
        public new void ReceivePacket(byte[] payload)
        {
            // 1. Paket geçersizse veya çok küçükse atla
            if (payload == null || payload.Length < 12)
                return;

            // 2. Go referansýndaki gibi Flag kontrolü (3. byte yani Offset 2)
            byte flags = payload[2];

            // 3. Eðer flag 1 ise paket þifrelidir (BattlEye/Albion Korumasý). Okumadan çöpe at!
            if (flags == 1)
            {
                return;
            }

            // 4. Þifresizse normal parser'a (base class) gönder, okumaya ve radara çizmeye devam etsin!
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

            try { PlayerParserTraceStore.CaptureEvent(eventCode, parameters); } catch { }
        }

        // OnRequest ve OnResponse kýsýmlarý ayný kalabilir (Zaten çalýþýyordu)
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

            try { PlayerParserTraceStore.CaptureResponse(eventCode, parameters); } catch { }
        }
    }
}


