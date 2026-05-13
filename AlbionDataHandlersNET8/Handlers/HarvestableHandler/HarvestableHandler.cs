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
            // --- EKLENEN KISIM: KAYNAK BÝTÝÞ EVENTÝ (45) ---
            else if (eventCode == (EventCodes)45)
            {
                HandleHarvestableFinished(parameters);
            }
        }

        // --- YENÝ METOT: BÝTEN KAYNAÐI SÝLME ---
        private void HandleHarvestableFinished(Dictionary<byte, object> parameters)
        {
            try
            {
                // Parametre 0 genellikle biten kaynaðýn ID'sidir
                if (parameters.TryGetValue(0, out object idObj))
                {
                    long rawId = Convert.ToInt64(idObj);
                    RemoveHarvestable(unchecked((int)rawId));
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error Code : 3 | {ex.Message}");
            }
        }

        private void HandleNewHarvestable(Dictionary<byte, object> parameters)
        {
            long rawId = EventHandlerUtils.ExtractValue<long>(parameters, 0);
            int id = unchecked((int)rawId);
            byte type = EventHandlerUtils.ExtractValue<byte>(parameters, 5);
            byte tier = EventHandlerUtils.ExtractValue<byte>(parameters, 7);

            float posX = 0, posY = 0;
            if (parameters.TryGetValue(8, out object posObj) && TryParsePosition(posObj, out float x8, out float y8)) { posX = x8; posY = y8; }
            else if (parameters.TryGetValue(4, out object posObj4) && TryParsePosition(posObj4, out float x4, out float y4)) { posX = x4; posY = y4; }

            byte size = EventHandlerUtils.ExtractValue<byte>(parameters, 10);
            byte enchant = EventHandlerUtils.ExtractValue<byte>(parameters, 11);

            AddOrUpdateHarvestable(id, type, tier, posX, posY, size, enchant);
        }

        private void HandleSimpleHarvestable(Dictionary<byte, object> parameters)
        {
            try
            {
                if (parameters.TryGetValue(0, out object idObj) && idObj is IList idList)
                {
                    var types = parameters[1] as IList;
                    var tiers = parameters[2] as IList;
                    var positions = parameters[3] as IList;
                    var sizes = parameters.ContainsKey(4) ? parameters[4] as IList : null;

                    if (types == null || tiers == null || positions == null) return;

                    int count = idList.Count;
                    lock (_lockObject)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            long rawId = Convert.ToInt64(idList[i]);
                            int id = unchecked((int)rawId);
                            byte type = Convert.ToByte(types[i]);
                            byte tier = Convert.ToByte(tiers[i]);

                            if ((i * 2 + 1) < positions.Count)
                            {
                                float posX = Convert.ToSingle(positions[i * 2]);
                                float posY = Convert.ToSingle(positions[i * 2 + 1]);
                                byte size = (sizes != null && i < sizes.Count) ? Convert.ToByte(sizes[i]) : (byte)0;

                                AddOrUpdateHarvestableInternal(id, type, tier, posX, posY, size, 0);
                            }
                        }
                        Harvestables?.Invoke(_harvestableList);
                    }
                }
                else
                {
                    long rawId = Convert.ToInt64(idObj);
                    int id = unchecked((int)rawId);
                    byte type = EventHandlerUtils.ExtractValue<byte>(parameters, 1);
                    byte tier = EventHandlerUtils.ExtractValue<byte>(parameters, 2);

                    float posX = 0, posY = 0;
                    if (parameters.TryGetValue(3, out object posRaw) && TryParsePosition(posRaw, out float x, out float y)) { posX = x; posY = y; }

                    byte size = EventHandlerUtils.ExtractValue<byte>(parameters, 4);

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
            long rawId = EventHandlerUtils.ExtractValue<long>(parameters, 0);
            int id = unchecked((int)rawId);

            // --- DÜZELTME BURADA ---
            // Oyun, kaynak 0 olduðunda paketin içine boyut (1) parametresini koymaz.
            // Bu yüzden eðer parametre yoksa, kaynaðýn bittiðini anlýyor ve 0 kabul ediyoruz.
            int newSize = 0;
            if (parameters.TryGetValue(1, out object sizeObj))
            {
                newSize = int.Parse(sizeObj.ToString());
            }

            lock (_lockObject)
            {
                var existing = _harvestableList.FirstOrDefault(x => x.Id == id);
                if (existing != null)
                {
                    if (newSize <= 0)
                    {
                        // Ýçi tamamen boþaldýðý an, radarda kalabalýk yapmasýn diye siliyoruz
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
            if (responseCode == ResponseCodes.PlayerJoiningMap || (short)responseCode == 35)
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


