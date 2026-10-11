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

        /// <summary>
        /// EK: teshis izi (PacketPipeline -> uygulama konsolu). Mists instance kimligi gibi
        /// bilgileri gorunur kilar; davranisi ETKILEMEZ.
        /// </summary>
        public static event Action<string>? DebugLog;

        /// <summary>
        /// Degisim anahtari: harita gercekten degisti mi?
        /// Mists'te alan 8 = "@MISTS@&lt;guid&gt;" (instance kimligi), alan 61 = "TNL-xxx" (sablon).
        /// Sablon kimligi her mistte ayni oldugu icin tek basina "degisti" olcutu degildir.
        /// </summary>
        private string _lastChangeKey = string.Empty;

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

        /// <summary>
        /// Mists zindani (Knightfall Abbey) icin kullanilan STATIK zone id'si.
        /// Cluster XML: ref="MISTS_DNG_UND_MINIMAP_STATIC_01" (statik minimap).
        /// Gorsel: Assets/Maps/DNG-MISTS-UND-WIP-01-01.webp (mevcut).
        /// </summary>
        public const string AbbeyZoneId = "DNG-MISTS-UND-WIP-01-01";

        public void OnResponse(ResponseCodes code, Dictionary<byte, object> parameters)
        {
            // KESİN ÇÖZÜM (KORUMA 2): PlayerJoiningMap (2) (Yükleme Ekranı / Teleport)
            // Işınlanma veya Journey Back kullanıldığında harita ID'sini doğrudan buradan yakalar!
            if (code == ResponseCodes.PlayerJoiningMap)
            {
                // Parametre indeksini bilmediğimiz için gelen tüm verileri tarayıp harita ID'sini buluyoruz.
                string displayId = null;
                string mistsId = null;
                string dungeonId = null;
                foreach (var kvp in parameters)
                {
                    if (!(kvp.Value is string s) || string.IsNullOrEmpty(s)) continue;

                    // MISTS ZINDANI (Knightfall Abbey): "@MISTSDUNGEON@<guid>"
                    // (canli kanit: 001419/234758 kayitlari -> OP-YANIT 2 alan [8])
                    if (s.StartsWith("@MISTSDUNGEON@", StringComparison.OrdinalIgnoreCase))
                    {
                        dungeonId = s;
                        continue;
                    }

                    // EK: mists instance kimligi ("@MISTS@<guid>") -> harita id'si (asagida)
                    if (s.StartsWith("@MISTS@", StringComparison.OrdinalIgnoreCase))
                    {
                        mistsId = s;
                        continue;
                    }

                    if (displayId == null && IsLikelyMapId(s)) displayId = s;
                }

                // MISTS ZINDANI (Knightfall Abbey): STATIK abbey haritasi kullanilir.
                // Kanit: cluster XML ref="MISTS_DNG_UND_MINIMAP_STATIC_01" (STATIK minimap) ve
                // Assets/Maps icinde DNG-MISTS-UND-WIP-01-01.webp mevcut (bounds -430..430 / -330..530).
                if (!string.IsNullOrEmpty(dungeonId))
                {
                    DebugLog?.Invoke($"[Mists] zindan (Knightfall Abbey) instance id: {dungeonId}");
                    // Ayni zindanda tekrar tekrar harita degisimi ilan edilmesin: dedupe anahtari
                    // instance kimligi, harita id'si ise STATIK abbey zone'u.
                    if (dungeonId != _lastChangeKey)
                    {
                        _lastChangeKey = dungeonId;
                        _pendingMapId = AbbeyZoneId;
                        _lastMapId = AbbeyZoneId;
                        _onMapChangedAction?.Invoke(AbbeyZoneId);
                    }
                    return;
                }

                // KULLANICI KARARI (13): mists instance kimligi ("@MISTS@<guid>") artik HARITA ID'si olarak
                // kullanilir; boylece her mists ayri gorunur ve Minimap bu id icin arka plan CIZMEZ
                // (yanlis tunel/zindan gorseli yerine bos arka plan). Konsola da iz dusurulur.
                if (!string.IsNullOrEmpty(mistsId))
                {
                    DebugLog?.Invoke($"[Mists] instance id: {mistsId}");
                    if (mistsId != _lastChangeKey)
                    {
                        _lastChangeKey = mistsId;
                        _pendingMapId = mistsId;
                        _lastMapId = mistsId;
                        _onMapChangedAction?.Invoke(mistsId);
                    }
                    return; // Bulduk, k.
                }

                if (displayId != null)
                {
                    // DUZELTME: ayni harita tekrar tekrar "yeni harita" ilan ediliyordu; her ilanda
                    // Program.cs -> engine.PurgeQueue() calisip paket kuyrugu bosaliyordu.
                    // Artik yalnizca GERCEKTEN degisti ise ilan edilir.
                    if (displayId != _lastChangeKey)
                    {
                        _lastChangeKey = displayId;
                        _pendingMapId = displayId;
                        _lastMapId = displayId;
                        _onMapChangedAction?.Invoke(displayId);
                    }
                    return; // Bulduk, k.
                }
            }

            // KORUMA 3: Portalın yanından geçerken gelen sahte yüklemeleri (Preload) engeller.
            // Sadece hafızaya alır, haritayı anında DETİRMEZ. (JoinFinished eventini bekler)
            // GÜNCELLEME: Harita değişimi başladı (yükleme ekranı). 
            // Eski verileri hemen temizle ve yeni haritayı aktifleştir ki yükleme esnasında gelen ilk paketler (Zindan, Kafes, Kaçakçı vb.) silinmesin!
            if (code == ResponseCodes.PlayerChangeCluster)
            {
                if (parameters.TryGetValue(0, out object mapIdObj) && mapIdObj != null)
                {
                    string id = mapIdObj.ToString();
                    // DUZELTME: ayni harita icin tekrar gelen ChangeCluster yanitlari harita degisimi
                    // sayilip kuyrugu bosaltiyordu (canli log: 20 sn'de 10 kez). Artik degismediyse atlanir.
                    if (id != _lastChangeKey)
                    {
                        _lastChangeKey = id;
                        _pendingMapId = id;
                        _lastMapId = id;
                        _onMapChangedAction?.Invoke(id);
                    }
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

        // KULLANICI KARARI (9): paket PARAMETRELERINI icerdigi icin GERI KONULDU.
        // (Su an cagrilmiyor; harita paketi parametrelerini MapPacketTrace.txt'ye dokmek icin hazir arac.)
        private void LogMapPacket(string type, int code, Dictionary<byte, object> parameters)
        {
            try
            {
                string log = $"[{DateTime.Now:HH:mm:ss}] [{type}] Code={code}\n";
                foreach (var kvp in parameters)
                {
                    string valStr = kvp.Value?.ToString() ?? "null";
                    if (kvp.Value is System.Collections.IList list)
                    {
                        valStr = "[ " + string.Join(", ", System.Linq.Enumerable.Cast<object>(list)) + " ]";
                    }
                    log += $"   Key {kvp.Key} : {valStr}\n";
                }
                log += "----------------------------------------\n";
                if (AlbionDataHandlers.Utils.PacketLogSettings.AllLogInfo)
                    System.IO.File.AppendAllText("MapPacketTrace.txt", log);
            }
            catch { }
        }

    }
}


