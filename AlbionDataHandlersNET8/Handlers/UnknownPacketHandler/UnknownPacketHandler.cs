using System;
using System.Collections.Generic;
using AlbionDataHandlers.Enums;

namespace AlbionDataHandlers.Handlers.UnknownPacketHandler
{
    public class UnknownPacketHandler : IEventHandler
    {
        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            // Kullanici istegi: Hareket vb. cok tekrar edenler (spam) disindaki TUM paketleri logla.
            // Sadece enum isimlerini kullaniyoruz, eski karmasik sayilari temizledik.
            var spamCodes = new HashSet<int> { 
                (int)EventCodes.Move, 
                (int)EventCodes.HealthUpdate, 
                (int)EventCodes.HealthUpdates,
                (int)EventCodes.CharacterStats, 
                (int)EventCodes.MobChangeState, 
                (int)EventCodes.UpdateLootChest,
                (int)EventCodes.CharacterEquipmentChanged,
                (int)EventCodes.RegenerationHealthChanged,
                (int)EventCodes.EnergyUpdate,
                (int)EventCodes.RegenerationEnergyChanged
            };

            if (!spamCodes.Contains((int)eventCode))
            {
                string eventName = Enum.IsDefined(typeof(EventCodes), eventCode) ? eventCode.ToString() : "UNKNOWN_EVENT";
                LogUnknownPacket(eventName, (int)eventCode, parameters);
            }
        }

        public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters) 
        { 
            // Bilinmeyen requestleri avlamak istersen burayi da acabiliriz.
            // LogUnknownPacket("REQUEST", (int)requestCode, parameters);
        }
        
        public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters) 
        { 
            // Bilinmeyen responselari avlamak istersen burayi da acabiliriz.
            // LogUnknownPacket("RESPONSE", (int)responseCode, parameters);
        }

        private readonly object _logLock = new object();
        private void LogUnknownPacket(string type, int code, Dictionary<byte, object> parameters)
        {
            try
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {type} (Code: {code}):\n";
                foreach (var kvp in parameters)
                {
                    string valueStr = kvp.Value?.ToString() ?? "null";
                    if (kvp.Value is byte[] bArr) 
                    {
                        string elems = string.Join(", ", bArr.Take(5));
                        valueStr = $"byte[{bArr.Length}] ({elems})";
                    }
                    else if (kvp.Value is System.Collections.IList list) 
                    {
                        var elems = new List<string>();
                        for(int i=0; i < Math.Min(list.Count, 5); i++) elems.Add(list[i]?.ToString() ?? "null");
                        valueStr = $"List[{list.Count}] ({string.Join(", ", elems)}) ({list.GetType().Name})";
                    }
                    
                    logLine += $"  [{kvp.Key}] = {valueStr}\n";
                }
                logLine += "--------------------------------------------------\n";
                
                if (AlbionDataHandlers.Utils.PacketLogSettings.AllLogInfo)
                {
                    lock (_logLock)
                    {
                        System.IO.File.AppendAllText("UnknownPacketsTrace.txt", logLine);
                    }
                }
            }
            catch { }
        }
    }
}
