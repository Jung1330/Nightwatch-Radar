using System;
using System.Collections.Generic;
using AlbionDataHandlers.Enums;

namespace AlbionDataHandlers.Handlers.UnknownPacketHandler
{
    public class UnknownPacketHandler : IEventHandler
    {
        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            // Eger paket EventCodes enum'unda tanimlanmamissa veya izole etmek istedigimiz bir paketse:
            if (!Enum.IsDefined(typeof(EventCodes), eventCode) || (int)eventCode > 300)
            {
                // Cok tekrar eden "bilinen ama onemsiz" paketleri filtreleyelim (Ornegin 300+ ID'li bazi onemsiz network paketleri)
                var spamCodes = new HashSet<int> { 
                    310, 319, 386, 394, 444, 445, 446, 492, 493, 494, 531, 311, 312, 313, 314, 315, 316, 317, 318, 
                    320, 321, 322, 323, 324, 325, 326, 327, 328, 329, 330, 331, 332, 333, 334, 335, 336, 337, 338, 
                    339, 340, 341, 342, 343, 344, 345, 346, 347, 348, 349, 350, 351, 352, 353, 354, 355, 356, 357, 
                    358, 359, 360, 361, 362, 363, 364, 365, 366, 367, 368, 369, 370, 371, 372, 373, 374, 375, 376, 
                    377, 378, 379, 380, 381, 382, 383, 384, 385, 387, 388, 389, 390, 391, 392, 393, 395, 396, 583, 
                    584, 397, 442, 443, 456, 461, 463, 472, 473, 474, 484, 485, 486, 487, 491, 496, 497, 499, 518, 
                    519, 520, 521, 522, 523, 525, 529, 530, 532, 537, 538, 539, 540, 541, 542, 544, 556, 558, 585, 
                    586, 596, 598, 600, 602, 604, 606, 612, 617, 666, 668 
                };
                // if (spamCodes.Contains((int)eventCode)) return;

                LogUnknownPacket("EVENT", (int)eventCode, parameters);
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
                string logLine = $"[{DateTime.Now:HH:mm:ss}] UNKNOWN {type} (Code: {code}):\n";
                foreach (var kvp in parameters)
                {
                    string valueStr = kvp.Value?.ToString() ?? "null";
                    if (kvp.Value is byte[] bArr) valueStr = "byte[" + bArr.Length + "]";
                    else if (kvp.Value is System.Collections.IList list) valueStr = "List[" + list.Count + "]";
                    
                    logLine += $"  [{kvp.Key}] = {valueStr} ({kvp.Value?.GetType().Name})\n";
                }
                logLine += "--------------------------------------------------\n";
                
                lock (_logLock)
                {
                    System.IO.File.AppendAllText("UnknownPacketsTrace.txt", logLine);
                }
            }
            catch { }
        }
    }
}
