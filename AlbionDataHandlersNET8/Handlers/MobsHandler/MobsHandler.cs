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
        switch (eventCode)
        {
            case EventCodes.Leave:
                HandleLeave(parameters);
                break;

            case EventCodes.Move:
                HandleMove(parameters);
                break;

            case EventCodes.NewMob:
                HandleNewMob(parameters);
                break;
            case EventCodes.MobChangeState:
                HandleMobChangeState(parameters);
                break;
        }
    }

    private void HandleMobChangeState(Dictionary<byte, object> parameters)
    {
        var id = EventHandlerUtils.ExtractValue<int>(parameters, 0);
        var enchantmentLevel = EventHandlerUtils.ExtractValue<int>(parameters, 1);

        lock (_lockObject)
        {
            var existingMob = _mobs.FirstOrDefault(m => m.Id == id);
            if (existingMob != null)
            {
                existingMob.EnchantmentLevel = enchantmentLevel;
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
}


