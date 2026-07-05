using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Utils;
using System.Reactive.Subjects;
using System.Collections;

namespace AlbionDataHandlers.Handlers;

public class MobsHandler : IEventHandler
{
    private readonly object _lockObject = new();
    private readonly IList<Mob> _mobs = new List<Mob>();
    public ISubject<IEnumerable<Mob>> Mobs { get; } = new Subject<IEnumerable<Mob>>();

    public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
    {
        switch ((int)eventCode)
        {
            case (int)EventCodes.Leave:
                HandleLeave(parameters);
                break;

            case (int)EventCodes.Move:
                HandleMove(parameters);
                break;

            case (int)EventCodes.NewMob:
                HandleNewMob(parameters);
                break;
            case (int)EventCodes.MobChangeState:
                HandleMobChangeState(parameters);
                break;
            case 525:
            case 530:
            case 532: // GÜNCELLEME: Yeni kafes / smuggler event kodu!
                HandleWispCage(parameters);
                break;
            case 556:
            case 558: // GÜNCELLEME: Gerçek iz sürme (Track) paketleri bu kodla geliyor!
                HandleHuntTrack(parameters);
                break;
            case 518:
                HandleChestSpawn(parameters);
                break;
            case 529:
                HandleChestRarity(parameters);
                break;
            case 391:
                HandleLootChestSpawn(parameters);
                break;
        }
    }
    private void HandleMobChangeState(Dictionary<byte, object> parameters)
    {
        // [0] Entity ID
        if (!parameters.TryGetValue(0, out var idObj)) return;
        int entityId = GetIntSafe(idObj);
        if (entityId == 0) return;

        // [1] Enchantment Level (Oyun .2 parlamasını burada gönderiyor)
        int newEnchantLevel = EventHandlerUtils.ExtractValue<int>(parameters, 1, 0);

        lock (_lockObject)
        {
            var mobToUpdate = _mobs.FirstOrDefault(m => m.Id == entityId);
            if (mobToUpdate != null)
            {
                // Mobun enchant'ını güncelle (Code: 47 paketinden gelen bilgi)
                mobToUpdate.EnchantmentLevel = newEnchantLevel;

                // Mobs listesini UI'a gönder (Radar güncellensin)
                Mobs.OnNext(_mobs);
            }
        }
    }
    private void HandleNewMob(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(0, out var idObj)) return;
        int id = GetIntSafe(idObj);
        if (id == 0) return;

        int typeId = EventHandlerUtils.ExtractValue<int>(parameters, 1);
        int networkTier = EventHandlerUtils.ExtractValue<int>(parameters, 21, 0);
        if (networkTier <= 0)
            networkTier = EventHandlerUtils.ExtractValue<int>(parameters, 2, 0);

        float posX = 0f;
        float posY = 0f;
        if (!TryGetPosFromArrayParam(parameters, 7, out posX, out posY)
            && !TryGetPosFromArrayParam(parameters, 8, out posX, out posY)
            && parameters.TryGetValue(4, out var p4)
            && parameters.TryGetValue(5, out var p5))
        {
            posX = GetFloatSafe(p4);
            posY = GetFloatSafe(p5);
        }

        float experience = EventHandlerUtils.ExtractValue<float>(parameters, 13, 0);
        string name = EventHandlerUtils.ExtractValue<string>(parameters, 32)
                      ?? EventHandlerUtils.ExtractValue<string>(parameters, 31);
        int enchantmentLevel = EventHandlerUtils.ExtractValue<int>(parameters, 33, 0);
        int rarity = EventHandlerUtils.ExtractValue<int>(parameters, 34, 0);
/*
        try
        {
            if (enchantmentLevel > 0 || networkTier >= 4)
            {
                string log = $"[{System.DateTime.Now}] [NewMob] TypeId={typeId}, Name={name}, Enchant={enchantmentLevel}, NetworkTier={networkTier}\n";
                foreach (var kv in parameters)
                {
                    string valStr = kv.Value?.ToString() ?? "null";
                    if (kv.Value is IList list)
                    {
                        valStr = "[ " + string.Join(", ", list.Cast<object>()) + " ]";
                    }
                    log += $"{kv.Key} : {valStr}\n";
                }
                // System.IO.File.AppendAllText("raw_resources.txt", log + "------------------\n");
            }
        }
        catch { }
*/
        var mob = new Mob
        {
            Id = id,
            TypeId = typeId,
            Experience = experience,
            Name = name,
            EnchantmentLevel = enchantmentLevel,
            NetworkTier = networkTier,
            Rarity = rarity,
            PositionX = posX,
            PositionY = posY
        };

        lock (_lockObject)
        {
            var existingMob = _mobs.FirstOrDefault(m => m.Id == mob.Id);
            if (existingMob != null)
            {
                _mobs.Remove(existingMob);
            }

            _mobs.Add(mob);
            Mobs.OnNext(_mobs);
        }
    }

    private void HandleMove(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(0, out var idObj)) return;
        int id = GetIntSafe(idObj);
        if (id == 0) return;

        float posX = EventHandlerUtils.ExtractValue<float>(parameters, 4);
        float posY = EventHandlerUtils.ExtractValue<float>(parameters, 5);
        if (TryGetMovePositionFromRaw(parameters, out var rx, out var ry))
        {
            posX = rx;
            posY = ry;
        }

        lock (_lockObject)
        {
            var mobToUpdate = _mobs.FirstOrDefault(m => m.Id == id);
            if (mobToUpdate != null)
            {
                mobToUpdate.PositionX = posX;
                mobToUpdate.PositionY = posY;
                Mobs.OnNext(_mobs);
            }
        }
    }

    private void HandleLeave(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(0, out var idObj)) return;
        int id = GetIntSafe(idObj);
        if (id == 0) return;

        lock (_lockObject)
        {
            var mobToRemove = _mobs.FirstOrDefault(m => m.Id == id);
            if (mobToRemove != null)
            {
                _mobs.Remove(mobToRemove);
                Mobs.OnNext(_mobs);
            }
        }
    }

    public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters)
    {
        // No implementation required for OnRequest in the current context
    }

    public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters)
    {
        if (responseCode == ResponseCodes.PlayerJoiningMap)
        {
            HandlePlayerJoiningMap(parameters);
        }
    }

    private void HandlePlayerJoiningMap(Dictionary<byte, object> parameters)
    {
        lock (_lockObject)
        {
            _mobs.Clear();
            Mobs.OnNext(_mobs);
        }
    }

    private static int GetIntSafe(object? obj)
    {
        if (obj == null) return 0;
        try
        {
            if (obj is byte[] bytes)
            {
                if (bytes.Length == 4) return BitConverter.ToInt32(bytes, 0);
                if (bytes.Length == 2) return BitConverter.ToInt16(bytes, 0);
                if (bytes.Length == 1) return bytes[0];
                return 0;
            }

            if (obj is IConvertible)
                return Convert.ToInt32(obj, System.Globalization.CultureInfo.InvariantCulture);

            return 0;
        }
        catch { return 0; }
    }

    private static float GetFloatSafe(object? obj)
    {
        if (obj == null) return 0f;
        try
        {
            if (obj is byte[] bytes)
            {
                if (bytes.Length == 4) return BitConverter.ToSingle(bytes, 0);
                if (bytes.Length == 8) return (float)BitConverter.ToDouble(bytes, 0);
                return 0f;
            }

            if (obj is IConvertible)
                return Convert.ToSingle(obj, System.Globalization.CultureInfo.InvariantCulture);

            return 0f;
        }
        catch { return 0f; }
    }

    private static bool IsValidWorldPosition(float x, float y)
    {
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
        if (Math.Abs(x) >= 4000f || Math.Abs(y) >= 4000f) return false;
        return Math.Abs(x) > 0.1f || Math.Abs(y) > 0.1f;
    }

    private static bool TryGetPosFromArrayParam(Dictionary<byte, object> parameters, byte key, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!parameters.TryGetValue(key, out var obj) || obj == null) return false;

        if (obj is Array arr && arr.Length >= 2)
        {
            float ax = GetFloatSafe(arr.GetValue(0));
            float ay = GetFloatSafe(arr.GetValue(1));
            if (IsValidWorldPosition(ax, ay)) { x = ax; y = ay; return true; }
        }
        else if (obj is IList list && list.Count >= 2)
        {
            float lx = GetFloatSafe(list[0]);
            float ly = GetFloatSafe(list[1]);
            if (IsValidWorldPosition(lx, ly)) { x = lx; y = ly; return true; }
        }

        return false;
    }

    private static bool TryGetMovePositionFromRaw(Dictionary<byte, object> parameters, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!parameters.TryGetValue(1, out var p1)) return false;

        byte[]? bytes = null;
        if (p1 is byte[] b)
        {
            bytes = b;
        }
        else if (p1 is IList list)
        {
            var tmp = new byte[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                int v = GetIntSafe(list[i]);
                if (v < 0 || v > 255) return false;
                tmp[i] = (byte)v;
            }
            bytes = tmp;
        }

        if (bytes == null || bytes.Length < 17) return false;

        float rx = BitConverter.ToSingle(bytes, 9);
        float ry = BitConverter.ToSingle(bytes, 13);
        if (!IsValidWorldPosition(rx, ry)) return false;

        x = rx;
        y = ry;
        return true;
    }

    private void HandleWispCage(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = GetIntSafe(idObj);
            if (id == 0) return;

            string name = EventHandlerUtils.ExtractValue<string>(parameters, 4) ?? "Caged Object";
            float[] pos = null;
            if (parameters.TryGetValue(2, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }

            if (pos != null && pos.Length >= 2)
            {
                var mob = new Mob
                {
                    Id = id,
                    TypeId = 53000,
                    Experience = 0,
                    Name = name,
                    EnchantmentLevel = 0,
                    NetworkTier = 0,
                    Rarity = 0,
                    PositionX = pos[0],
                    PositionY = pos[1]
                };

                lock (_lockObject)
                {
                    var existingMob = _mobs.FirstOrDefault(m => m.Id == mob.Id);
                    if (existingMob != null) _mobs.Remove(existingMob);
                    _mobs.Add(mob);
                    Mobs.OnNext(_mobs);
                }
            }
        }
        catch { }
    }

    private void HandleHuntTrack(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = GetIntSafe(idObj);
            if (id == 0) return;

            string name = EventHandlerUtils.ExtractValue<string>(parameters, 3) ?? "Track";
            float[] pos = null;
            if (parameters.TryGetValue(1, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }

            if (pos != null && pos.Length >= 2)
            {
                var mob = new Mob
                {
                    Id = id,
                    TypeId = 55600,
                    Experience = 0,
                    Name = name,
                    EnchantmentLevel = 0,
                    NetworkTier = EventHandlerUtils.ExtractValue<int>(parameters, 6, 0),
                    Rarity = EventHandlerUtils.ExtractValue<int>(parameters, 7, 0),
                    PositionX = pos[0],
                    PositionY = pos[1]
                };

                lock (_lockObject)
                {
                    var existingMob = _mobs.FirstOrDefault(m => m.Id == mob.Id);
                    if (existingMob != null) _mobs.Remove(existingMob);
                    _mobs.Add(mob);
                    Mobs.OnNext(_mobs);
                }
            }
        }
        catch { }
    }

    private void HandleChestSpawn(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = GetIntSafe(idObj);
            if (id == 0) return;

            float[] pos = null;
            if (parameters.TryGetValue(1, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }

            long unlockTicks = 0;
            if (parameters.TryGetValue(3, out var ticksObj))
            {
                if (ticksObj is long l) unlockTicks = l;
                else if (ticksObj is IConvertible ic) unlockTicks = Convert.ToInt64(ic);
            }

            if (pos != null && pos.Length >= 2)
            {
                lock (_lockObject)
                {
                    var existingMob = _mobs.FirstOrDefault(m => m.Id == id);
                    if (existingMob != null)
                    {
                        existingMob.PositionX = pos[0];
                        existingMob.PositionY = pos[1];
                        existingMob.CurrentLerpedX = pos[0];
                        existingMob.CurrentLerpedY = pos[1];
                        existingMob.TypeId = 51800;
                        if (unlockTicks > 0)
                        {
                            existingMob.UnlockTicks = unlockTicks;
                        }
                    }
                    else
                    {
                        var mob = new Mob
                        {
                            Id = id,
                            TypeId = 51800,
                            Name = "Mists Portal",
                            PositionX = pos[0],
                            PositionY = pos[1],
                            CurrentLerpedX = pos[0],
                            CurrentLerpedY = pos[1],
                            EnchantmentLevel = 0,
                            NetworkTier = 0,
                            Rarity = 0,
                            UnlockTicks = unlockTicks
                        };
                        _mobs.Add(mob);
                    }
                    Mobs.OnNext(_mobs);
                }
            }
        }
        catch { }
    }

    private void HandleChestRarity(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = GetIntSafe(idObj);
            if (id == 0) return;

            int rarity = EventHandlerUtils.ExtractValue<int>(parameters, 1, 0);
            long unlockTicks = 0;
            if (parameters.TryGetValue(2, out var ticksObj))
            {
                if (ticksObj is long l) unlockTicks = l;
                else if (ticksObj is IConvertible ic) unlockTicks = Convert.ToInt64(ic);
            }

            lock (_lockObject)
            {
                var existingMob = _mobs.FirstOrDefault(m => m.Id == id);
                if (existingMob != null)
                {
                    existingMob.Rarity = rarity;
                    existingMob.TypeId = 51800;
                    if (unlockTicks > 0)
                    {
                        existingMob.UnlockTicks = unlockTicks;
                    }
                }
                else
                {
                    var mob = new Mob
                    {
                        Id = id,
                        TypeId = 51800,
                        Name = "Mists Portal",
                        Rarity = rarity,
                        PositionX = 0,
                        PositionY = 0,
                        EnchantmentLevel = 0,
                        NetworkTier = 0,
                        UnlockTicks = unlockTicks
                    };
                    _mobs.Add(mob);
                }
                Mobs.OnNext(_mobs);
            }
        }
        catch { }
    }

    private void HandleLootChestSpawn(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = GetIntSafe(idObj);
            if (id == 0) return;

            float[] pos = null;
            if (parameters.TryGetValue(1, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }

            if (pos == null || pos.Length < 2) return;

            string chestName = "LOOTCHEST";
            if (parameters.TryGetValue(3, out var nameObj) && nameObj != null)
            {
                chestName = nameObj.ToString();
            }

            if (chestName != null && chestName.ToLowerInvariant().Contains("mist"))
            {
                if (parameters.TryGetValue(4, out var nameObj2) && nameObj2 != null)
                {
                    chestName = nameObj2.ToString();
                }
            }

            int rarity = 1;
            if (chestName != null)
            {
                string lower = chestName.ToLowerInvariant();
                if (lower.Contains("legendary") || lower.Contains("yellow")) rarity = 4;
                else if (lower.Contains("rare") || lower.Contains("purple")) rarity = 3;
                else if (lower.Contains("uncommon") || lower.Contains("blue")) rarity = 2;
                else if (lower.Contains("standard") || lower.Contains("green")) rarity = 1;
                else rarity = 0;
            }

            lock (_lockObject)
            {
                var existingMob = _mobs.FirstOrDefault(m => m.Id == id);
                if (existingMob != null)
                {
                    existingMob.PositionX = pos[0];
                    existingMob.PositionY = pos[1];
                    existingMob.CurrentLerpedX = pos[0];
                    existingMob.CurrentLerpedY = pos[1];
                    existingMob.TypeId = 51900;
                    existingMob.Name = chestName;
                    existingMob.Rarity = rarity;
                }
                else
                {
                    var mob = new Mob
                    {
                        Id = id,
                        TypeId = 51900,
                        Name = chestName,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1],
                        EnchantmentLevel = 0,
                        NetworkTier = 0,
                        Rarity = rarity,
                        UnlockTicks = 0
                    };
                    _mobs.Add(mob);
                }
                Mobs.OnNext(_mobs);
            }
        }
        catch { }
    }
}


