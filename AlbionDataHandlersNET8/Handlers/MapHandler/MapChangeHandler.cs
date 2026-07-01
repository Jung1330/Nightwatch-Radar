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
            // KORUMA 1: Eer haritay ChangeCluster ile hafzaya aldysak, 
            // sadece karakter fiziksel olarak yere bastnda (JoinFinished) radara onayla.
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
            // KESN ZM (KORUMA 2): PlayerJoiningMap (2) (Ykleme Ekran / Teleport)
            // Inlanma veya Journey Back kullanldnda harita ID'sini dorudan burdan yakalar!
            if (code == ResponseCodes.PlayerJoiningMap)
            {
                // Parametre indeksini bilmediimiz iin gelen tm verileri tarayp Harita ID'sini buluyoruz
                foreach (var kvp in parameters)
                {
                    if (kvp.Value is string val && IsLikelyMapId(val))
                    {
                        _pendingMapId = val;
                        _lastMapId = val;
                        _onMapChangedAction?.Invoke(val);
                        return; // Bulduk, k.
                    }
                }
            }

            // KORUMA 3: Portaln yanndan geerken gelen sahte yklemeleri (Preload) engeller.
            // Sadece hafzaya alr, haritay annda DETRMEZ. (JoinFinished eventini bekler)
            // GÜNCELLEME: Harita değişimi başladı (yükleme ekranı). 
            // Eski verileri hemen temizle ve yeni haritayı aktifleştir ki yükleme esnasında gelen ilk paketler (Zindan, Kafes, Kaçakçı vb.) silinmesin!
            if (code == ResponseCodes.PlayerChangeCluster)
            {
                if (parameters.TryGetValue(0, out object mapIdObj) && mapIdObj != null)
                {
                    _pendingMapId = mapIdObj.ToString();
                    _lastMapId = _pendingMapId;
                    _onMapChangedAction?.Invoke(_pendingMapId);
                }
            }
        }

        // Albion Map ID'lerinin karakteristik yapsn tanyan Akll Filtre
        private bool IsLikelyMapId(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;

            // Standart 4 haneli ID'ler (rn: "4301", "0000") veya uzantl halleri ("4007-HALL-01")
            if (val.Length >= 4 && char.IsDigit(val[0]) && char.IsDigit(val[1]) && char.IsDigit(val[2]) && char.IsDigit(val[3]))
                return true;

            // Zindan, snak, arena, ada vs. ID'leri
            string upper = val.ToUpperInvariant();
            if (upper.StartsWith("DNG") || upper.StartsWith("TNL") || upper.StartsWith("PSG") ||
                upper.StartsWith("HIDEOUT") || upper.StartsWith("ISLAND") ||
                upper.StartsWith("ARENA") || upper.StartsWith("CORRUPT"))
                return true;

            return false;
        }
    }
}


