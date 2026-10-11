using System;
using System.Collections.Generic;
using System.Numerics;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Entities;

namespace AlbionDataHandlers.Handlers.DungeonHandler
{
    public class DungeonHandler : IEventHandler
    {
        public event Action<Dungeon>? DungeonDetected;
        public event Action<long>? DungeonLeft;
        
        public static event Action<string>? DebugLog;

        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            int code = (int)eventCode;
            if (eventCode == EventCodes.NewRandomDungeonExit || 
                eventCode == EventCodes.NewMistsDungeonExit || 
                eventCode == EventCodes.RandomDungeonPositionInfo ||
                eventCode == EventCodes.NewMistsStaticEntrance ||
                eventCode == EventCodes.NewMistsCityEntrance ||
                eventCode == EventCodes.NewMistsBorderExit ||
                eventCode == EventCodes.NewCagedObject ||
                eventCode == EventCodes.CagedObjectStateUpdated ||
                eventCode == EventCodes.NewPortalEntrance ||
                eventCode == EventCodes.NewPortalExit ||
                eventCode == EventCodes.NewMistsCityRoadsEntrance ||
                eventCode == EventCodes.NewHellDungeonStaticEntrance ||
                eventCode == EventCodes.NewCorruptedStaticEntrance ||
                eventCode == EventCodes.NewHellgateExitPortal ||
                eventCode == EventCodes.NewTunnelExit ||
                eventCode == EventCodes.NewTunnelExitTemp)
            {
                HandleDungeon(parameters, eventCode);
            }
            else if (eventCode == EventCodes.NewMistsOpenWorldExit)
            {
                HandleExitPortal(parameters, (int)code);
            }
            else if (eventCode == EventCodes.NewMistsWispSpawn)
            {
                HandleWispSpawn(parameters, (int)code);
            }
            else if (eventCode == EventCodes.DynamicTemplate)   // 605: yuva adi + durum (konum YOK)
            {
                HandleDynamicTemplate(parameters);
            }
            else if (eventCode == EventCodes.Leave)
            {
                if (parameters.TryGetValue(0, out var idObj))
                {
                    try
                    {
                        long id = idObj is byte[] b && b.Length == 8 ? BitConverter.ToInt64(b, 0) : Convert.ToInt64(idObj);
                        DungeonLeft?.Invoke(id);
                    }
                    catch { }
                }
            }
        }

        public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters) { }
        public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters) { }

        private void HandleDungeon(Dictionary<byte, object> parameters, EventCodes code)
        {
            try
            {
                // Paket alanlari URETILEN MODELDEN okunur -> indeks numaralari tek tabloda
                // (AlbionDataHandlersNET8\Packets\Events.g.cs) durur. Model bos/0 donerse
                // asagidaki ham okuma gecerli kalir (yedek); diger kodlar icin ham okumalar AYNEN.
                AlbionDataHandlers.Packets.Events.NewRandomDungeonExit p325 =
                    code == EventCodes.NewRandomDungeonExit
                        ? AlbionDataHandlers.Packets.Events.NewRandomDungeonExit.From(parameters)
                        : null;
                // 523 (NewMistsStaticEntrance): [0] id · [1] GUID (16 bayt) · [2] KONUM
                // DUZELTME: eskiden konum [1]'de araniyordu; [1] bir GUID oldugu icin ilk iki
                // bayti koordinat saniliyor ve isaret YANLIS yere dusuyordu (ornek: (8,115) -> (58,234)).
                AlbionDataHandlers.Packets.Events.NewMistsStaticEntrance p523 =
                    code == EventCodes.NewMistsStaticEntrance
                        ? AlbionDataHandlers.Packets.Events.NewMistsStaticEntrance.From(parameters)
                        : null;
                // 533 (NewCagedObject): [0] id · [2] KONUM · [4] kafes adi
                AlbionDataHandlers.Packets.Events.NewCagedObject p533 =
                    code == EventCodes.NewCagedObject
                        ? AlbionDataHandlers.Packets.Events.NewCagedObject.From(parameters)
                        : null;
                // 534 (CagedObjectStateUpdated): [0] id · [1] durum (konum YOK)
                AlbionDataHandlers.Packets.Events.CagedObjectStateUpdated p534 =
                    code == EventCodes.CagedObjectStateUpdated
                        ? AlbionDataHandlers.Packets.Events.CagedObjectStateUpdated.From(parameters)
                        : null;

                long id = 0;
                if (parameters.ContainsKey(0))
                {
                    if (parameters[0] is byte[] b && b.Length == 8) id = BitConverter.ToInt64(b, 0);
                    else long.TryParse(parameters[0].ToString(), out id);
                }
                if (p325 != null && p325.F0 != 0) id = p325.F0;   // [0] EntityId (model)
                if (p523 != null && p523.F0 != 0) id = p523.F0;   // [0] EntityId (model)
                if (p533 != null && p533.F0 != 0) id = p533.F0;   // [0] EntityId (model)
                if (p534 != null && p534.F0 != 0) id = p534.F0;   // [0] EntityId (model)

                float[] pos = null;
                object posObj = null;
                // Pos objesi [1] ya da [2]'de gelebilir. Ancak sadece float[] ya da IList ise koordinattir.
                if (parameters.TryGetValue(1, out var p1) && (p1 is float[] || p1 is System.Collections.IList)) posObj = p1;
                else if (parameters.TryGetValue(2, out var p2) && (p2 is float[] || p2 is System.Collections.IList)) posObj = p2;
                if (p325 != null && p325.F1 != null) posObj = p325.F1;   // [1] Position (model)
                if (p523 != null && p523.F2 != null) posObj = p523.F2;   // [2] Position (model) - GUID tuzagini atlar
                if (p533 != null && p533.F2 != null) posObj = p533.F2;   // [2] Position (model)

                if (posObj != null)
                {
                    if (posObj is float[] fArr) pos = fArr;
                    else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                    else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
                }
                // NOT: ToUpperInvariant - Turkce sistemde ToUpper() "i" harfini "I" (noktali) yapiyor
                // ve "TUNNEL_EXIT" gibi karsilastirmalar sessizce tutmuyordu (canli kayitta goruldu).
                string rawType = parameters.ContainsKey(3) ? parameters[3].ToString().ToUpperInvariant() : "";
                if (p325 != null) rawType = (p325.F3 ?? "").ToUpperInvariant();   // [3] tip metni (model)
                // prefab: bazı event'lerde [4] (Cage), [5], bazılarında [6]'da geliyor (Event 325 gibi)
                string prefab = "";
                if ((code == EventCodes.NewCagedObject || code == EventCodes.CagedObjectStateUpdated) && parameters.ContainsKey(4)) prefab = parameters[4].ToString();
                else if (parameters.ContainsKey(5)) prefab = parameters[5].ToString();
                else if (parameters.ContainsKey(6)) prefab = parameters[6].ToString();
                // [6] zaten farklı paket varsa üste yaz
                if (string.IsNullOrEmpty(prefab) && parameters.ContainsKey(6)) prefab = parameters[6].ToString();
                else if (parameters.ContainsKey(6) && !string.IsNullOrEmpty(parameters[6]?.ToString()))
                {
                    string p6 = parameters[6].ToString().ToUpperInvariant();
                    if (p6.Contains("MIST") || p6.Contains("ABBEY") || p6.Contains("DUNGEON")) prefab = parameters[6].ToString();
                }
                // 533 (NewCagedObject): kafes adi [4]'te -> model alani (ayni deger; okuma tek tabloda)
                if (p533 != null && !string.IsNullOrEmpty(p533.F4)) prefab = p533.F4;
                string tag15 = parameters.ContainsKey(15) ? parameters[15].ToString().ToUpperInvariant() : "";
                string tag16 = parameters.ContainsKey(16) ? parameters[16].ToString().ToUpperInvariant() : "";
                byte enchant = 0;
                // Yeni guncellemede Enchantment 9 numara olarak geliyor, 8 artik bir Boolean.
                if (parameters.ContainsKey(9)) byte.TryParse(parameters[9].ToString(), out enchant);
                if (p325 != null) enchant = p325.F9;   // [9] Enchantment (model)

                int groupSize = 1;
                if (parameters.ContainsKey(17)) int.TryParse(parameters[17].ToString(), out groupSize);

                string tag18 = parameters.ContainsKey(18) ? parameters[18].ToString().ToUpperInvariant() : "";

                string prefabUpper = prefab.ToUpperInvariant();
                // "_" normalize: MISTS_PERSONAL_SMALL gibi isimlerde MIST tespiti için
                string rawTypeNorm = rawType.Replace("_", " ");
                string prefabNorm = prefabUpper.Replace("_", " ");
                string tag15Norm = tag15.Replace("_", " ");
                string tag16Norm = tag16.Replace("_", " ");
                string tag18Norm = tag18.Replace("_", " ");
                string type = "";

                // UI Console'da ne yakalandığını görmek için logla
                DebugLog?.Invoke($"[Dungeon] code={(int)code} | rawType='{rawType}' | prefab='{prefab}' | tag15='{tag15}' | tag16='{tag16}' | [18]={(parameters.ContainsKey(18) ? parameters[18] : 0)}");

                if (rawType.Contains("SMUGGLER") || prefabUpper.Contains("SMUGGLER") || tag15.Contains("SMUGGLER") || tag16.Contains("SMUGGLER") || tag18.Contains("SMUGGLER"))
                {
                    type = "10";
                }
                else if (rawType.Contains("CAGE") || prefabUpper.Contains("CAGE") || tag15.Contains("CAGE") || tag16.Contains("CAGE") || tag18.Contains("CAGE"))
                {
                    type = "9";
                }
                else if (rawType.Contains("MIST_DUNGEON_ENTRANCE") || rawType.Contains("MISTS") || rawType.Contains("MIST") || rawType.Contains("ABBEY") 
                    || prefabUpper.Contains("ABBEY") || prefabUpper.Contains("MIST") || prefabNorm.Contains("MIST") || prefabUpper.Contains("WISP") 
                    || tag15.Contains("MIST") || tag15.Contains("ABBEY") || tag15Norm.Contains("MIST")
                    || tag16.Contains("MIST") || tag16Norm.Contains("MIST") || tag16.Contains("ABBEY")
                    || tag18.Contains("MIST") || tag18Norm.Contains("MIST") || tag18.Contains("ABBEY"))
                {
                    type = "7";
                }
                else if (rawType.Contains("BOSSROOM_SOLO") || rawType.Contains("BOSSLAIR_SOLO"))
                {
                    type = "5";
                }
                else if (rawType.Contains("BOSSROOM") || rawType.Contains("BOSSLAIR"))
                {
                    type = "6";
                }
                else if (rawType.Contains("CORRUPT") || prefabUpper.Contains("CORRUPT"))
                {
                    type = "3";
                }
                else if (rawType.Contains("HELLGATE") || prefabUpper.Contains("HELLGATE"))
                {
                    type = "4";
                }
                else if (prefabUpper.Contains("ELITE") || prefabUpper.Contains("AVALON") || rawType.Contains("AVALON"))
                {
                    type = "8";
                }
                // --- YENI: mists/tunel giris-cikislari (onceden hepsi varsayilan "1" = "Solo Dungeon" gorunuyordu) ---
                // Olcut EVENT KODU (metin aramasi degil) -> oyun guncellemesinde metin degisse de calisir.
                else if (code == EventCodes.NewMistsStaticEntrance)
                {
                    type = "11";   // Mists Entrance (523)
                }
                else if (code == EventCodes.NewMistsBorderExit || code == EventCodes.NewMistsDungeonExit)
                {
                    type = "12";   // Mists Exit (538 / 539)
                }
                else if (code == EventCodes.NewTunnelExitTemp || code == EventCodes.NewTunnelExit)
                {
                    type = "13";   // Tunnel Exit (525 / 449)
                }
                else
                {
                    // Tablo analizi: [18] parametresi her zaman dogru sinyali veriyor.
                    // 1 = Solo Dungeon, 2 veya 3 = Group Dungeon
                    int dungeonGroupSize = 1;
                    if (parameters.ContainsKey(18)) int.TryParse(parameters[18].ToString(), out dungeonGroupSize);
                    type = dungeonGroupSize <= 1 ? "1" : "2";
                }

                if (type != "0" && id != 0 && pos != null && pos.Length >= 2)
                {
                    var dbInfo = AlbionDataHandlers.Utils.DungeonDatabase.ParseDungeon(prefab);

                    string dName = dbInfo.Name;
                    if (!string.IsNullOrEmpty(tag15) && tag15 != "0") 
                    {
                        dName = tag15;
                    }
                    if (type == "9" || type == "10") dName = ""; // Cage'lerin üzerinde isim yazmasın

                    var d = new Dungeon
                    {
                        Id = id,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        Type = type,
                        Prefab = prefab,
                        Tier = dbInfo.Tier,
                        Name = dName,
                        EnchantmentLevel = enchant,
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1]
                    };
                    DungeonDetected?.Invoke(d);
                }
            }
            catch { }
        }

        private void HandleExitPortal(Dictionary<byte, object> parameters, int code)
        {
            try
            {
                long id = 0;
                if (parameters.ContainsKey(0))
                {
                    if (parameters[0] is byte[] b && b.Length == 8) id = BitConverter.ToInt64(b, 0);
                    else long.TryParse(parameters[0].ToString(), out id);
                }
                float[] pos = null;
                if (parameters.TryGetValue(1, out var posObj))
                {
                    if (posObj is float[] fArr) pos = fArr;
                    else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                    else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
                }
                string name = "Exit";

                if (id != 0 && pos != null && pos.Length >= 2)
                {
                    var d = new Dungeon
                    {
                        Id = id,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        Type = "Exit",
                        Prefab = "Exit",
                        Tier = 0,
                        Name = name,
                        EnchantmentLevel = 0,
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1]
                    };
                    DungeonDetected?.Invoke(d);
                }
            }
            catch { }
        }
        private void HandleWispSpawn(Dictionary<byte, object> parameters, int code)
        {
            try
            {
                // URETILEN PAKET MODELI: [0] = entity ID, [1] = konum (Vector2)
                // (okunabilirlik: parameters[0]/[1] yerine modelin alan adlari)
                var wisp = AlbionDataHandlers.Packets.Events.NewMistsWispSpawn.From(parameters);
                if (wisp == null) return;

                long id = wisp.F0;
                float[] pos = wisp.F1;
                // ISIM YOK: bu nesneler mist degil (Mists'in dekoratif wisp/bitki assetleri).
                // Etiket cizilmiyor (Minimap tip 14) + veride de isim tutulmuyor ki
                // tooltip/liste/rapor gibi baska bir yerde "Wisp" yazmasin.
                string name = string.Empty;

                if (id != 0 && pos != null && pos.Length >= 2)
                {
                    var d = new Dungeon
                    {
                        Id = id,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        // NOT: 526 = NewMistsWispSpawn. Paket TIP/ISIM tasimiyor (sadece id+konum+float+bool);
                        // bu nesneler mob da kaynak da degil -> Mists'teki dekoratif "wisp" (bitki/agac gorunumlu).
                        // Bu yuzden AYRI tip "14" kullanilir ve ekranda yalnizca "Wisp" yazar ("Mist" DEGIL).
                        Type = "14", // Wisp (mist degil)
                        Prefab = "WISP",
                        Tier = 0,
                        Name = name,
                        EnchantmentLevel = 0,
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1]
                    };
                    DungeonDetected?.Invoke(d);
                }
            }
            catch { }
        }
        /// <summary>
        /// 605 = DynamicTemplate: yuva adi (orn. "instanceslot_29") + durum + katman.
        /// KONUM YOK -> haritada isaret uretilemez. Bu yuzden SADECE uygulama konsoluna
        /// iz dusurulur; hicbir durum/veri degistirilmez, hicbir isaret uretilmez.
        /// (607 = NewOutlandsTeleportationPortal BILEREK islenmiyor.)
        /// </summary>
        private void HandleDynamicTemplate(Dictionary<byte, object> parameters)
        {
            try
            {
                // URETILEN PAKET MODELI (kod 605 = DynamicTemplate):
                //   [0] yuva adi (metin) · [1] durum · [2] katman · [3] zaman damgasi
                var dt = AlbionDataHandlers.Packets.Events.DynamicTemplate.From(parameters);
                if (dt == null) return;

                DebugLog?.Invoke($"[DynamicTemplate] slot='{dt.F0}' durum={dt.F1} katman={dt.F2}");
            }
            catch { }
        }
    }
}
