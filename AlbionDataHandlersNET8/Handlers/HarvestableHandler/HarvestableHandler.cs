using System;
using System.Collections.Generic;
using System.Linq;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Utils;
using System.Collections;

namespace AlbionDataHandlers.Handlers
{
    public class HarvestableHandler : IEventHandler
    {
        public event Action<IEnumerable<Harvestable>> Harvestables;

        private readonly List<Harvestable> _harvestableList = new List<Harvestable>();
        private readonly object _lockObject = new object();

        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            if (eventCode == EventCodes.NewHarvestableObject)
            {
                HandleNewHarvestable(parameters);
            }
            else if (eventCode == EventCodes.NewSimpleHarvestableObject || eventCode == EventCodes.NewSimpleHarvestableObjectList)
            {
                HandleSimpleHarvestable(parameters);
            }
            else if (eventCode == EventCodes.HarvestableChangeState)
            {
                HandleHarvestableChangeState(parameters);
            }
            else if (eventCode == EventCodes.Leave)
            {
                HandleLeave(parameters);
            }
            // --- KAYNAK BİTİŞ EVENTİ ---
            // DÜZELTME (A9): Oyunun gerçek enum'unda 45 = NewBuilding'tir, "kaynak bitti" DEĞİLDİR.
            // Doğrusu: HarvestFinished = 61  (HarvestStart = 59, HarvestCancel = 60)
            // Kaynak: Extra\dump.cs -> "public enum EventCodes" (TypeDefIndex 15279)
            else if (eventCode == EventCodes.HarvestFinished)
            {
                HandleHarvestableFinished(parameters);
            }
        }

        // --- YENİ METOT: BİTEN KAYNAĞI SİLME ---
        private void HandleHarvestableFinished(Dictionary<byte, object> parameters)
        {
            try
            {
                // URETILEN PAKET MODELI (kod 61 = HarvestFinished): [0] = ResourceId
                // (ExtractLong: int ve byte[8] bicimlerinin ikisini de okur.)
                if (parameters == null || !parameters.ContainsKey(0)) return;
                var finished = AlbionDataHandlers.Packets.Events.HarvestFinished.From(parameters);
                if (finished == null) return;

                RemoveHarvestable(unchecked((int)finished.ResourceId));
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 3 | {ex.Message}");
            }
        }

        private void HandleNewHarvestable(Dictionary<byte, object> parameters)
        {
            // URETILEN PAKET MODELI (kod 40 = NewHarvestableObject):
            //   [0] ResourceId · [5] ResourceType · [7] Tier · [8] Position · [10] Size · [11] Enchant
            var pkt = AlbionDataHandlers.Packets.Events.NewHarvestableObject.From(parameters);
            if (pkt == null) return;

            int id = unchecked((int)pkt.ResourceId);
            byte type = pkt.ResourceType;
            byte tier = pkt.Tier;

            float posX = 0, posY = 0;
            if (pkt.Position != null && TryParsePosition(pkt.Position, out float x8, out float y8)) { posX = x8; posY = y8; }
            // OLU YOL (bilerek korundu): tabloya gore [4] bir konum DEGIL (int) ve canli
            // kayitta hic gelmiyor; eski kod buraya yedek olarak bakiyordu.
            else if (parameters.TryGetValue(4, out object posObj4) && TryParsePosition(posObj4, out float x4, out float y4)) { posX = x4; posY = y4; }

            // ESKI DAVRANIS KORUNUR: [10] byte okunuyordu -> 0-255 disi deger okunamaz ve
            // 0 kabul edilirdi (kaynak eklenmezdi). Model alani short gelir.
            int size = pkt.Size;
            if (size < 0 || size > byte.MaxValue) size = 0;
            byte enchant = pkt.Enchant;
/*
            try
            {
                if (enchant > 0 || tier >= 4)
                {
                    string log = $"[{System.DateTime.Now}] [NewHarvestable] Type={type}, Tier={tier}, Enchant={enchant}\n";
                    foreach (var kv in parameters)
                    {
                        string valStr = kv.Value?.ToString() ?? "null";
                        if (kv.Value is System.Collections.IList list)
                        {
                            valStr = "[ " + string.Join(", ", System.Linq.Enumerable.Cast<object>(list)) + " ]";
                        }
                        log += $"{kv.Key} : {valStr}\n";
                    }
                   // System.IO.File.AppendAllText("raw_resources.txt", log + "------------------\n");
                }
            }
            catch { }
*/
            AddOrUpdateHarvestable(id, type, tier, posX, posY, size, enchant);
        }

        private void HandleSimpleHarvestable(Dictionary<byte, object> parameters)
        {
            try
            {
                if (parameters.TryGetValue(0, out object idObj) && idObj is IList)
                {
                    // URETILEN PAKET MODELI (kod 39 = NewSimpleHarvestableObjectList):
                    //   [0] ID dizisi · [1] tip dizisi · [2] tier dizisi
                    //   [3] konum dizisi (x,y ciftleri) · [4] size dizisi
                    // NOT: tel formatinda [0] short[]/byte[] gelebiliyor; model alanlari
                    // esnek okuyucularla doldurulur (indeksler tek tabloda kalsin diye).
                    var list = AlbionDataHandlers.Packets.Events.NewSimpleHarvestableObjectList.From(parameters);
                    if (list == null) return;

                    long[] ids = list.F0;
                    byte[] types = list.F1;
                    byte[] tiers = list.F2;
                    float[] positions = list.F3;
                    short[] sizes = list.F4;

                    if (ids == null || types == null || tiers == null || positions == null) return;

                    int count = ids.Length;
                    lock (_lockObject)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int id = unchecked((int)ids[i]);
                            byte type = types[i];
                            byte tier = tiers[i];

                            if ((i * 2 + 1) < positions.Length)
                            {
                                float posX = positions[i * 2];
                                float posY = positions[i * 2 + 1];
                                byte size = (sizes != null && i < sizes.Length) ? (byte)sizes[i] : (byte)0;

                                AddOrUpdateHarvestableInternal(id, type, tier, posX, posY, size, 0);
                            }
                        }
                        Harvestables?.Invoke(_harvestableList);
                    }
                }
                else
                {
                    // URETILEN PAKET MODELI (kod 38 = NewSimpleHarvestableObject):
                    //   [0] ID · [1] tip · [2] tier · [3] konum · [4] size
                    var single = AlbionDataHandlers.Packets.Events.NewSimpleHarvestableObject.From(parameters);
                    if (single == null) return;

                    int id = unchecked((int)single.F0);
                    byte type = single.F1;
                    byte tier = single.F2;

                    float posX = 0, posY = 0;
                    if (single.F3 != null && TryParsePosition(single.F3, out float x, out float y)) { posX = x; posY = y; }

                    // ESKI DAVRANIS KORUNUR: [4] byte okunuyordu (0-255 disi -> 0).
                    int sizeRaw = single.F4;
                    if (sizeRaw < 0 || sizeRaw > byte.MaxValue) sizeRaw = 0;
                    byte size = (byte)sizeRaw;

                    lock (_lockObject)
                    {
                        AddOrUpdateHarvestableInternal(id, type, tier, posX, posY, size, 0);
                        Harvestables?.Invoke(_harvestableList);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 4 | {ex.Message}");
            }
        }

        private void HandleHarvestableChangeState(Dictionary<byte, object> parameters)
        {
            // URETILEN PAKET MODELI (kod 46 = HarvestableChangeState):
            //   [0] kaynak ID · [1] yeni miktar · [2] bayt
            var state = AlbionDataHandlers.Packets.Events.HarvestableChangeState.From(parameters);
            if (state == null) return;
            int id = unchecked((int)state.F0);

            // --- DÜZELTME BURADA (aynen korundu) ---
            // Oyun, kaynak 0 olduğunda paketin içine boyut (1) parametresini koymaz.
            // Bu yüzden eğer parametre yoksa, kaynağın bittiğini anlıyor ve 0 kabul ediyoruz.
            // Model alani da yoksa 0 doner -> ayni davranis.
            int newSize = state.F1;

            lock (_lockObject)
            {
                var existing = _harvestableList.FirstOrDefault(x => x.Id == id);
                if (existing != null)
                {
                    if (newSize <= 0)
                    {
                        // İçi tamamen boşaldığı an, radarda kalabalık yapmasın diye siliyoruz
                        _harvestableList.Remove(existing);
                    }
                    else
                    {
                        existing.Count = newSize;
                        existing.Size = newSize;
                    }
                    Harvestables?.Invoke(_harvestableList);
                }
            }
        }

        private void HandleLeave(Dictionary<byte, object> parameters)
        {
            try
            {
                if (parameters.TryGetValue(0, out object idObj))
                {
                    if (idObj is IList idList)
                    {
                        foreach (var item in idList)
                        {
                            long rawId = Convert.ToInt64(item);
                            RemoveHarvestable(unchecked((int)rawId));
                        }
                    }
                    else
                    {
                        long rawId = Convert.ToInt64(idObj);
                        RemoveHarvestable(unchecked((int)rawId));
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 5 | {ex.Message}");
            }
        }

        private bool TryParsePosition(object obj, out float x, out float y)
        {
            x = 0; y = 0;
            if (obj is IList list && list.Count >= 2)
            {
                try { x = Convert.ToSingle(list[0]); y = Convert.ToSingle(list[1]); return true; }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 6 | {ex.Message}");
                }
            }
            return false;
        }

        private void AddOrUpdateHarvestable(int id, int type, int tier, float x, float y, int size, int enchant)
        {
            lock (_lockObject)
            {
                AddOrUpdateHarvestableInternal(id, type, tier, x, y, size, enchant);
                Harvestables?.Invoke(_harvestableList);
            }
        }

        private void AddOrUpdateHarvestableInternal(int id, int type, int tier, float x, float y, int size, int enchant)
        {
            if (Math.Abs(x) < 0.01f && Math.Abs(y) < 0.01f) return;

            // Boyutu 0 veya negatif gelirse ekleme
            if (size <= 0) return;

            var existing = _harvestableList.FirstOrDefault(h => h.Id == id);
            if (existing != null)
            {
                existing.Type = type;
                existing.Tier = tier;
                existing.PositionX = x;
                existing.PositionY = y;
                existing.CurrentLerpedX = x;
                existing.CurrentLerpedY = y;
                existing.Size = size;
                existing.Count = size;
                if (existing.Capacity <= 0 || size > existing.Capacity)
                    existing.Capacity = size;
                existing.EnchantmentLevel = enchant;
            }
            else
            {
                var h = new Harvestable
                {
                    Id = id,
                    Type = type,
                    Tier = tier,
                    Size = size,
                    Count = size,
                    Capacity = size,
                    PositionX = x,
                    PositionY = y,
                    CurrentLerpedX = x,
                    CurrentLerpedY = y,
                    EnchantmentLevel = enchant
                };
                _harvestableList.Add(h);
            }
        }

        private void RemoveHarvestable(int id)
        {
            lock (_lockObject)
            {
                if (_harvestableList.RemoveAll(x => x.Id == id) > 0)
                    Harvestables?.Invoke(_harvestableList);
            }
        }

        public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters) { }

        public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters)
        {
            if (responseCode == ResponseCodes.PlayerJoiningMap || responseCode == ResponseCodes.PlayerChangeCluster)
            {
                lock (_lockObject)
                {
                    _harvestableList.Clear();
                    Harvestables?.Invoke(new List<Harvestable>());
                }
            }
        }
    }
}




