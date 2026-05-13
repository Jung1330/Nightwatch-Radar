using AlbionDataHandlers.Enums;
using System.Collections.Generic;
using System;

namespace AlbionDataHandlers.Handlers.MapHandler
{
    public class MapChangeHandler : IEventHandler
    {
        private readonly Action<string> _onMapChangedAction;
        private string _lastMapId = string.Empty;
        private string _pendingMapId = string.Empty;

        public MapChangeHandler(Action<string> onMapChangedAction)
        {
            _onMapChangedAction = onMapChangedAction;
        }

        public void OnEvent(EventCodes code, Dictionary<byte, object> parameters)
        {
            // KORUMA 1: Eðer haritayý ChangeCluster ile hafýzaya aldýysak, 
            // sadece karakter fiziksel olarak yere bastýðýnda (JoinFinished) radara onayla.
            if (code == EventCodes.JoinFinished)
            {
                if (!string.IsNullOrEmpty(_pendingMapId) && _pendingMapId != _lastMapId)
                {
                    _lastMapId = _pendingMapId;
                    _onMapChangedAction?.Invoke(_lastMapId);
                }
            }
        }

        public void OnRequest(RequestCodes code, Dictionary<byte, object> parameters) { }

        public void OnResponse(ResponseCodes code, Dictionary<byte, object> parameters)
        {
            // KESÝN ÇÖZÜM (KORUMA 2): PlayerJoiningMap (2) (Yükleme Ekraný / Teleport)
            // Iþýnlanma veya Journey Back kullanýldýðýnda harita ID'sini doðrudan burdan yakalar!
            if (code == ResponseCodes.PlayerJoiningMap)
            {
                // Parametre indeksini bilmediðimiz için gelen tüm verileri tarayýp Harita ID'sini buluyoruz
                foreach (var kvp in parameters)
                {
                    if (kvp.Value is string val && IsLikelyMapId(val))
                    {
                        _pendingMapId = val;
                        _lastMapId = val;
                        _onMapChangedAction?.Invoke(val);
                        return; // Bulduk, çýk.
                    }
                }
            }

            // KORUMA 3: Portalýn yanýndan geçerken gelen sahte yüklemeleri (Preload) engeller.
            // Sadece hafýzaya alýr, haritayý anýnda DEÐÝÞTÝRMEZ. (JoinFinished eventini bekler)
            if (code == ResponseCodes.PlayerChangeCluster)
            {
                if (parameters.TryGetValue(0, out object mapIdObj) && mapIdObj != null)
                {
                    _pendingMapId = mapIdObj.ToString();
                }
            }
        }

        // Albion Map ID'lerinin karakteristik yapýsýný tanýyan Akýllý Filtre
        private bool IsLikelyMapId(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;

            // Standart 4 haneli ID'ler (Örn: "4301", "0000") veya uzantýlý halleri ("4007-HALL-01")
            if (val.Length >= 4 && char.IsDigit(val[0]) && char.IsDigit(val[1]) && char.IsDigit(val[2]) && char.IsDigit(val[3]))
                return true;

            // Zindan, sýðýnak, arena, ada vs. ID'leri
            string upper = val.ToUpperInvariant();
            if (upper.StartsWith("DNG") || upper.StartsWith("TNL") || upper.StartsWith("PSG") ||
                upper.StartsWith("HIDEOUT") || upper.StartsWith("ISLAND") ||
                upper.StartsWith("ARENA") || upper.StartsWith("CORRUPT"))
                return true;

            return false;
        }
    }
}


