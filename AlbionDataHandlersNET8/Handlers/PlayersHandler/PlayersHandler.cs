using System;
using System.Collections.Generic;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Utils;
using System.Collections;
using System.Linq;
using System.Diagnostics;

namespace AlbionDataHandlers.Handlers
{
    public class PlayersHandler : IEventHandler
    {
        public static int LocalPlayerId { get; private set; } = 0;

        private static readonly bool SelfOnlyTestMode = false;
        private static readonly bool AllowEventBasedLocalFallback = false;

        // --- PLAYER MOVE DECODE TEST SWITCHES (runtime) ---
        // DevTools > Player Decode sekmesinden aç/kapat yapýlýr.
        public static bool DecodePath01_Int1e7_1_9 { get; set; } = false;
        public static bool DecodePath02_Int1e6_1_9 { get; set; } = false;
        public static bool DecodePath03_Int1e5_1_9 { get; set; } = false;
        public static bool DecodePath04_Int100_1_9 { get; set; } = false;
        public static bool DecodePath05_Float_1_9 { get; set; } = false;
        public static bool DecodePath06_Int1e7_9_13 { get; set; } = false;
        public static bool DecodePath07_Int1e6_9_13 { get; set; } = false;
        public static bool DecodePath08_Int1e5_9_13 { get; set; } = false;
        public static bool DecodePath09_Int100_9_13 { get; set; } = false;
        public static bool DecodePath10_Float_9_13 { get; set; } = false;
        public static bool DecodePath11_XInt100YFloat_9_13 { get; set; } = false;
        public static bool DecodePath12_XFloatYInt100_9_13 { get; set; } = false;
        public static bool DecodePath13_Param4_5 { get; set; } = false;
        public static bool DecodePath14_Param19_25 { get; set; } = false;
        public static bool DecodePath15_List0_1 { get; set; } = true;
        public static bool DecodePath16_Float_9_17 { get; set; } = true;

        public event Action<Player>? LocalPlayerPosition;
        public event Action<IEnumerable<Player>>? OtherPlayersDetected;
        public event Action<int>? PlayerLeft;

        private readonly List<Player> _otherPlayersList = new List<Player>();
        private readonly Dictionary<int, (float x, float y)> _pendingPlayerMoves = new Dictionary<int, (float x, float y)>();
        private readonly Dictionary<int, DateTime> _pendingLeaveById = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, DateTime> _lastHealthSeenById = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, int> _localIdScores = new Dictionary<int, int>();
        private int _localEntityId = 0;
        private static readonly TimeSpan LeaveGracePeriod = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan HealthGracePeriod = TimeSpan.FromMilliseconds(1500);

        // EFSANE GERÝ DÖNDÜ: KUSURSUZ ORIGIN VE SPAWN MERKEZLERÝ
        private readonly Dictionary<int, (float x, float y)> _spawnWorldById = new Dictionary<int, (float x, float y)>();
        private readonly Dictionary<int, (float ox, float oy)> _originById = new Dictionary<int, (float ox, float oy)>();
        private readonly object _syncLock = new object();

        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            PlayerParserTraceStore.CaptureEvent(eventCode, parameters);
            TryLearnLocalEntityId(eventCode, parameters);
            SweepExpiredPendingLeaves();

            if (eventCode == EventCodes.NewCharacter) HandleNewCharacter(parameters);
            else if (eventCode == EventCodes.Move) HandleMove(parameters);
            else if (eventCode == EventCodes.Leave) HandleLeave(parameters);
            else if (eventCode == EventCodes.HealthUpdate) HandleHealthUpdate(parameters);
            else if (eventCode == EventCodes.HealthUpdates) HandleHealthUpdates(parameters);
        }

        private void HandleNewCharacter(Dictionary<byte, object> parameters)
        {
            if (SelfOnlyTestMode) return;

            try
            {
                if (!parameters.TryGetValue(0, out var idObj)) return;
                int id = Convert.ToInt32(idObj);

                string name = parameters.TryGetValue(1, out var nameObj) ? (nameObj?.ToString() ?? "Unknown") : "Unknown";
                string guild = parameters.TryGetValue(8, out var gObj) ? (gObj?.ToString() ?? "") : "";
                string alliance = parameters.TryGetValue(51, out var aObj) ? (aObj?.ToString() ?? "") : "";
                int faction = parameters.TryGetValue(53, out var fObj) ? Convert.ToInt32(fObj) : 0;

                int[] equipIds = new int[0];
                if (parameters.TryGetValue(40, out var equipObj) || parameters.TryGetValue(38, out equipObj))
                {
                    try { equipIds = ParseEquipmentIds(equipObj); } catch { }
                }

                float spawnX = 0f; float spawnY = 0f;
                if (parameters.TryGetValue(19, out var p19) && parameters.TryGetValue(25, out var p25))
                {
                    spawnX = GetFloatSafe(p19);
                    spawnY = GetFloatSafe(p25);
                    if (Math.Abs(spawnX) > 0.1f && Math.Abs(spawnY) > 0.1f)
                    {
                        lock (_syncLock)
                        {
                            _spawnWorldById[id] = (spawnX, spawnY);
                            _originById.Remove(id);
                        }
                    }
                }

                float initCurrentHealth = 0f; float initMaxHealth = 0f;
                if (parameters.TryGetValue(22, out var ncCurrObj) && parameters.TryGetValue(23, out var ncMaxObj))
                {
                    float hp22 = GetFloatSafe(ncCurrObj); float hp23 = GetFloatSafe(ncMaxObj);
                    if (hp22 > 0f && hp23 > 0f) { initCurrentHealth = hp22; initMaxHealth = hp23; }
                }
                if (initMaxHealth <= 0f && parameters.TryGetValue(2, out var initCurrObj) && parameters.TryGetValue(3, out var initMaxObj))
                {
                    float rawInitCurrent = GetFloatSafe(initCurrObj); float rawInitMax = GetFloatSafe(initMaxObj);
                    if (rawInitMax >= 200f && rawInitCurrent >= 0f) { initCurrentHealth = rawInitCurrent; initMaxHealth = rawInitMax; }
                }

                float[] pos = FindPosition(parameters);
                float initialX = spawnX > 0.1f ? spawnX : (pos != null ? pos[0] : 0f);
                float initialY = spawnY > 0.1f ? spawnY : (pos != null ? pos[1] : 0f);

                lock (_syncLock)
                {
                    _pendingLeaveById.Remove(id);
                    _lastHealthSeenById.Remove(id);

                    var newPlayer = new Player
                    {
                        Id = id,
                        Name = name,
                        Guild = guild,
                        Alliance = alliance,
                        Faction = faction,
                        PositionX = initialX,
                        PositionY = initialY,
                        CurrentLerpedX = initialX,
                        CurrentLerpedY = initialY,
                        CurrentHealth = initCurrentHealth,
                        MaxHealth = initMaxHealth,
                        Equipment = equipIds
                    };

                    if (_pendingPlayerMoves.TryGetValue(id, out var pendingPos))
                    {
                        newPlayer.PositionX = pendingPos.x; newPlayer.PositionY = pendingPos.y;
                        newPlayer.CurrentLerpedX = pendingPos.x; newPlayer.CurrentLerpedY = pendingPos.y;
                        _pendingPlayerMoves.Remove(id);
                    }

                    _otherPlayersList.RemoveAll(p => p.Id == id);
                    _otherPlayersList.Add(newPlayer);
                    OtherPlayersDetected?.Invoke(_otherPlayersList.ToList());
                }
            }
            catch (System.Exception ex) { System.Console.WriteLine($"Error Code : 8 | {ex.Message}"); }
        }

        private void HandleMove(Dictionary<byte, object> parameters)
        {
            if (SelfOnlyTestMode) return;

            if (AllowEventBasedLocalFallback)
                TryHydrateLocalFromEventMove(parameters);

            try
            {
                if (!parameters.TryGetValue(0, out var idObj)) return;
                int id = Convert.ToInt32(idObj);

                // Radar ekranýnda kendi ID'ni buradan bul
                Debug.WriteLine($"[MOVE EVENT] ID={id}");

                bool hasRaw = TryGetMoveRaw(parameters, 1, out var rawX, out var rawY)
                    || TryGetMoveRaw(parameters, 3, out rawX, out rawY);
                if (!hasRaw || !IsValidWorldPosition(rawX, rawY)) return;

                int localId = LocalPlayerId > 0 ? LocalPlayerId : _localEntityId;
                if (localId > 0 && id == localId)
                {
                    LocalPlayerPosition?.Invoke(new Player
                    {
                        Id = id,
                        PositionX = rawX,
                        PositionY = rawY,
                        CurrentLerpedX = rawX,
                        CurrentLerpedY = rawY
                    });
                    return;
                }

                lock (_syncLock)
                {
                    _pendingLeaveById.Remove(id);

                    var existingPlayer = _otherPlayersList.FirstOrDefault(p => p.Id == id);
                    if (existingPlayer == null)
                    {
                        _pendingPlayerMoves[id] = (rawX, rawY);
                        return;
                    }

                    existingPlayer.PositionX = rawX;
                    existingPlayer.PositionY = rawY;
                    existingPlayer.CurrentLerpedX = rawX;
                    existingPlayer.CurrentLerpedY = rawY;
                    OtherPlayersDetected?.Invoke(_otherPlayersList.ToList());
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error Code : 9 | {ex.Message}"); }
        }

        private static bool IsValidWorldPosition(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                return false;

            if (Math.Abs(x) >= 4000f || Math.Abs(y) >= 4000f)
                return false;

            return Math.Abs(x) > 0.1f || Math.Abs(y) > 0.1f;
        }

        private static float DistanceSquared(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (dx * dx) + (dy * dy);
        }

        private void HandleLeave(Dictionary<byte, object> parameters)
        {
            if (SelfOnlyTestMode) return;

            if (parameters.TryGetValue(0, out var idObj))
            {
                int id = Convert.ToInt32(idObj);
                lock (_syncLock)
                {
                    _pendingPlayerMoves.Remove(id);
                    _spawnWorldById.Remove(id);
                    _originById.Remove(id);

                    // Open world Leave paketleri dalgalý gelebiliyor. Anlýk silme yerine grace period uygula.
                    _pendingLeaveById[id] = DateTime.UtcNow;
                }
            }
        }

        private void SweepExpiredPendingLeaves()
        {
            lock (_syncLock)
            {
                if (_pendingLeaveById.Count == 0) return;

                DateTime now = DateTime.UtcNow;
                var expired = _pendingLeaveById
                    .Where(x => now - x.Value >= LeaveGracePeriod)
                    .Select(x => x.Key)
                    .ToList();

                if (expired.Count == 0) return;

                bool changed = false;
                foreach (var id in expired)
                {
                    if (_lastHealthSeenById.TryGetValue(id, out var hpSeenAt) && (now - hpSeenAt) < HealthGracePeriod)
                        continue;

                    _pendingLeaveById.Remove(id);
                    if (_otherPlayersList.RemoveAll(p => p.Id == id) > 0)
                    {
                        changed = true;
                        _lastHealthSeenById.Remove(id);
                        PlayerLeft?.Invoke(id);
                    }
                }

                if (changed)
                    OtherPlayersDetected?.Invoke(_otherPlayersList.ToList());
            }
        }

        private readonly HashSet<int> _loggedIds = new HashSet<int>();









        private bool TryGetMoveRaw(Dictionary<byte, object> parameters, out float rawX, out float rawY)
        {
            return TryGetMoveRaw(parameters, 1, out rawX, out rawY);
        }

        private bool TryGetMoveRaw(Dictionary<byte, object> parameters, byte sourceKey, out float rawX, out float rawY)
        {
            rawX = 0f;
            rawY = 0f;

            if (!parameters.TryGetValue(sourceKey, out var p1))
                return false;

            byte[]? bytes = null;
            if (p1 is byte[] bArr)
            {
                bytes = bArr;
            }
            else if (p1 is IList list && list.Count >= 13)
            {
                var tmp = new byte[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    int v = GetIntSafe(list[i]);
                    if (v < 0 || v > 255)
                        return false;

                    tmp[i] = (byte)v;
                }

                bytes = tmp;
            }

            if (bytes == null || bytes.Length < 13)
                return false;

            byte subtype = bytes[0];
            if (subtype != 1 && subtype != 3)
                return false;

            if (DecodePath01_Int1e7_1_9)
            {
                float x = BitConverter.ToInt32(bytes, 1) / 10_000_000f;
                float y = BitConverter.ToInt32(bytes, 9) / 10_000_000f;
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath02_Int1e6_1_9)
            {
                float x = BitConverter.ToInt32(bytes, 1) / 1_000_000f;
                float y = BitConverter.ToInt32(bytes, 9) / 1_000_000f;
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath03_Int1e5_1_9)
            {
                float x = BitConverter.ToInt32(bytes, 1) / 100_000f;
                float y = BitConverter.ToInt32(bytes, 9) / 100_000f;
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath04_Int100_1_9)
            {
                float x = BitConverter.ToInt32(bytes, 1) / 100f;
                float y = BitConverter.ToInt32(bytes, 9) / 100f;
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath05_Float_1_9)
            {
                float x = BitConverter.ToSingle(bytes, 1);
                float y = BitConverter.ToSingle(bytes, 9);
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (bytes.Length >= 17)
            {
                if (DecodePath06_Int1e7_9_13)
                {
                    float x = BitConverter.ToInt32(bytes, 9) / 10_000_000f;
                    float y = BitConverter.ToInt32(bytes, 13) / 10_000_000f;
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath07_Int1e6_9_13)
                {
                    float x = BitConverter.ToInt32(bytes, 9) / 1_000_000f;
                    float y = BitConverter.ToInt32(bytes, 13) / 1_000_000f;
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath08_Int1e5_9_13)
                {
                    float x = BitConverter.ToInt32(bytes, 9) / 100_000f;
                    float y = BitConverter.ToInt32(bytes, 13) / 100_000f;
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath09_Int100_9_13)
                {
                    float x = BitConverter.ToInt32(bytes, 9) / 100f;
                    float y = BitConverter.ToInt32(bytes, 13) / 100f;
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath10_Float_9_13)
                {
                    float x = BitConverter.ToSingle(bytes, 9);
                    float y = BitConverter.ToSingle(bytes, 13);
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (bytes.Length >= 21 && DecodePath16_Float_9_17)
                {
                    float x = BitConverter.ToSingle(bytes, 9);
                    float y = BitConverter.ToSingle(bytes, 17);
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath11_XInt100YFloat_9_13)
                {
                    float x = BitConverter.ToInt32(bytes, 9) / 100f;
                    float y = BitConverter.ToSingle(bytes, 13);
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }

                if (DecodePath12_XFloatYInt100_9_13)
                {
                    float x = BitConverter.ToSingle(bytes, 9);
                    float y = BitConverter.ToInt32(bytes, 13) / 100f;
                    if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
                }
            }

            if (DecodePath13_Param4_5 && parameters.TryGetValue(4, out var p4) && parameters.TryGetValue(5, out var p5))
            {
                float x = GetFloatSafe(p4);
                float y = GetFloatSafe(p5);
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath14_Param19_25 && parameters.TryGetValue(19, out var p19) && parameters.TryGetValue(25, out var p25))
            {
                float x = GetFloatSafe(p19);
                float y = GetFloatSafe(p25);
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            if (DecodePath15_List0_1 && p1 is IList listRaw && listRaw.Count >= 2)
            {
                float x = GetFloatSafe(listRaw[0]);
                float y = GetFloatSafe(listRaw[1]);
                if (IsValidWorldPosition(x, y)) { rawX = x; rawY = y; return true; }
            }

            return false;
        }

        private static bool IsLikelyPlayerId(int id)
        {
            // Tutorial/þehirde oyuncu idleri genelde küçük, mob idleri daha yüksek kümelerde.
            // Çok sert filtrelemiyoruz; sadece bariz negatif/0 ve aþýrý büyük deðerleri eliyoruz.
            return id > 0 && id < 1_000_000;
        }






        private float[]? FindPosition(Dictionary<byte, object> parameters, bool preferMovePayload = false)
        {
            if (parameters.TryGetValue(19, out var p19) && parameters.TryGetValue(25, out var p25))
            {
                float x = GetFloatSafe(p19); float y = GetFloatSafe(p25);
                if (Math.Abs(x) > 0.1f && Math.Abs(y) > 0.1f) return new float[] { x, y };
            }
            if (!preferMovePayload && parameters.TryGetValue(4, out var p4) && parameters.TryGetValue(5, out var p5))
            {
                float x = GetFloatSafe(p4); float y = GetFloatSafe(p5);
                if (Math.Abs(x) > 0.1f && Math.Abs(x) < 4000f && Math.Abs(y) > 0.1f && Math.Abs(y) < 4000f) return new float[] { x, y };
            }
            return null;
        }

        public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters)
        {
            int reqCode = (int)requestCode;
            if (reqCode != (int)RequestCodes.Move && reqCode != (int)RequestCodes.MoveAlt) return;

            try
            {
                int localId = LocalPlayerId > 0 ? LocalPlayerId : _localEntityId;

                // Öncelik: ham payload decode (en güncel decode path'leri kullanýr)
                if (TryGetMoveRaw(parameters, 1, out var rx, out var ry) ||
                    TryGetMoveRaw(parameters, 3, out rx, out ry))
                {
                    LocalPlayerPosition?.Invoke(new Player { Id = localId, PositionX = rx, PositionY = ry, CurrentLerpedX = rx, CurrentLerpedY = ry });
                    return;
                }

                // Fallback: düz float/list payload (bazý paket varyantlarý)
                if (TryGetLocalXYFromRequest(parameters, 1, out float x1, out float y1))
                {
                    LocalPlayerPosition?.Invoke(new Player { Id = localId, PositionX = x1, PositionY = y1, CurrentLerpedX = x1, CurrentLerpedY = y1 });
                    return;
                }

                if (TryGetLocalXYFromRequest(parameters, 3, out float x3, out float y3))
                {
                    LocalPlayerPosition?.Invoke(new Player { Id = localId, PositionX = x3, PositionY = y3, CurrentLerpedX = x3, CurrentLerpedY = y3 });
                    return;
                }
            }
            catch
            {
            }
        }

        private bool TryGetLocalXYFromRequest(Dictionary<byte, object> parameters, byte key, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (!parameters.TryGetValue(key, out var obj) || obj == null)
                return false;

            if (obj is float[] fa && fa.Length >= 2)
            {
                x = fa[0];
                y = fa[1];
                return IsValidWorldPosition(x, y);
            }

            if (obj is IList list && list.Count >= 2)
            {
                x = GetFloatSafe(list[0]);
                y = GetFloatSafe(list[1]);
                return IsValidWorldPosition(x, y);
            }

            return false;
        }

        private bool IsLocalPacketById(Dictionary<byte, object> parameters)
        {
            int localId = LocalPlayerId > 0 ? LocalPlayerId : _localEntityId;
            if (localId <= 0)
                return true;

            // Move request paketlerinin çoðunda id (param[0]) bulunmayabilir.
            // Request zaten local client tarafýndan üretildiði için id yoksa local kabul ediyoruz.
            if (!parameters.TryGetValue(0, out var idObj))
                return true;

            int packetId = GetIntSafe(idObj);
            return packetId > 0 && packetId == localId;
        }

        private void TryHydrateLocalFromEventMove(Dictionary<byte, object> parameters)
        {
            try
            {
                if (!parameters.TryGetValue(1, out var p1)) return;

                // Parser'dan direkt ham move payload (AOSniffer mantýðý)
                if (p1 is byte[] raw && raw.Length >= 17)
                {
                    float fx = BitConverter.ToSingle(raw, 9);
                    float fy = BitConverter.ToSingle(raw, 13);
                    if (IsValidWorldPosition(fx, fy))
                    {
                        LocalPlayerPosition?.Invoke(new Player { PositionX = fx, PositionY = fy });
                        return;
                    }
                }

                if (p1 is IList rawList && rawList.Count >= 17)
                {
                    var tmp = new byte[rawList.Count];
                    for (int i = 0; i < rawList.Count; i++)
                    {
                        int v = GetIntSafe(rawList[i]);
                        if (v < 0 || v > 255) { tmp = Array.Empty<byte>(); break; }
                        tmp[i] = (byte)v;
                    }

                    if (tmp.Length >= 17)
                    {
                        float fx = BitConverter.ToSingle(tmp, 9);
                        float fy = BitConverter.ToSingle(tmp, 13);
                        if (IsValidWorldPosition(fx, fy))
                        {
                            LocalPlayerPosition?.Invoke(new Player { PositionX = fx, PositionY = fy });
                            return;
                        }
                    }
                }

                if (p1 is float[] fArr && fArr.Length >= 2)
                {
                    float x = fArr[0];
                    float y = fArr[1];
                    if (IsValidWorldPosition(x, y))
                    {
                        LocalPlayerPosition?.Invoke(new Player { PositionX = x, PositionY = y });
                        return;
                    }
                }

                if (p1 is IList list && list.Count >= 2)
                {
                    float x = GetFloatSafe(list[0]);
                    float y = GetFloatSafe(list[1]);
                    if (IsValidWorldPosition(x, y))
                    {
                        LocalPlayerPosition?.Invoke(new Player { PositionX = x, PositionY = y });
                        return;
                    }
                }

                if (parameters.TryGetValue(4, out var p4) && parameters.TryGetValue(5, out var p5))
                {
                    float x = GetFloatSafe(p4);
                    float y = GetFloatSafe(p5);
                    if (IsValidWorldPosition(x, y))
                    {
                        LocalPlayerPosition?.Invoke(new Player { PositionX = x, PositionY = y });
                    }
                }
            }
            catch
            {
            }
        }

        public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters)
        {
            if ((int)responseCode == 2)
            {
                if (parameters.TryGetValue(0, out var idObj))
                {
                    int localId = GetIntSafe(idObj);
                    if (localId > 0)
                    {
                        LocalPlayerId = localId;
                        _localEntityId = localId;
                    }
                }
            }

            if ((int)responseCode == 2 || (int)responseCode == 35)
            {
                lock (_syncLock)
                {
                    _otherPlayersList.Clear(); _pendingPlayerMoves.Clear(); _spawnWorldById.Clear(); _originById.Clear();
                    _pendingLeaveById.Clear();
                    _localIdScores.Clear();
                    _localEntityId = LocalPlayerId;
                    if ((int)responseCode == 35)
                    {
                        LocalPlayerId = 0;
                        _localEntityId = 0;
                    }
                    OtherPlayersDetected?.Invoke(new List<Player>());
                }
            }
        }

        private void TryLearnLocalEntityId(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            if (!parameters.TryGetValue(0, out var idObj))
                return;

            int id = GetIntSafe(idObj);
            if (!IsLikelyPlayerId(id))
                return;

            int weight = 0;
            int code = (int)eventCode;

            // Local oyuncuda sýk görülen aksiyon/event kodlarý
            if (code == 19 || code == 211 || code == 353 || code == 357 || code == 358 || code == 359)
                weight += 4;

            // 0 ve 1 ayný id ise (örn: CastSpell) güçlü self sinyali
            if (parameters.TryGetValue(1, out var p1))
            {
                int id1 = GetIntSafe(p1);
                if (id1 > 0 && id1 == id)
                    weight += 4;
            }

            if (weight <= 0)
                return;

            int newScore = (_localIdScores.TryGetValue(id, out var oldScore) ? oldScore : 0) + weight;
            _localIdScores[id] = newScore;

            if (_localEntityId <= 0 || !_localIdScores.TryGetValue(_localEntityId, out var currentScore) || newScore >= currentScore)
                _localEntityId = id;
        }

        private void HandleHealthUpdate(Dictionary<byte, object> parameters)
        {
            if (SelfOnlyTestMode) return;

            if (!parameters.TryGetValue(0, out var idObj)) return;
            int id = Convert.ToInt32(idObj);

            lock (_syncLock)
            {
                var player = _otherPlayersList.FirstOrDefault(p => p.Id == id);
                if (player != null)
                {
                        _lastHealthSeenById[id] = DateTime.UtcNow;

                    float previousMax = player.MaxHealth;
                    float previousCurrent = player.CurrentHealth;
                    bool hasParam2 = parameters.TryGetValue(2, out var p2Obj);
                    bool hasParam3 = parameters.TryGetValue(3, out var p3Obj);

                    if (hasParam2 && hasParam3)
                    {
                        float v2 = GetFloatSafe(p2Obj);
                        float v3 = GetFloatSafe(p3Obj);

                        if (v2 <= 0f && v3 <= 0f && parameters.TryGetValue(22, out var altCurr) && parameters.TryGetValue(23, out var altMax))
                        {
                            float altC = GetFloatSafe(altCurr);
                            float altM = GetFloatSafe(altMax);
                            if (altC > 0f && altM > 0f)
                            {
                                v2 = altC;
                                v3 = altM;
                            }
                        }

                        if (v2 <= 0f && v3 > 0f)
                        {
                            player.CurrentHealth = v3;
                            if (player.MaxHealth <= 0f) player.MaxHealth = previousMax > 0f ? previousMax : v3;
                            if (player.MaxHealth < player.CurrentHealth) player.MaxHealth = player.CurrentHealth;
                        }
                        else
                        {
                            player.CurrentHealth = v2;
                            player.MaxHealth = v3;
                        }
                    }
                    else if (hasParam2) { player.CurrentHealth = GetFloatSafe(p2Obj); }
                    else if (hasParam3) { player.CurrentHealth = GetFloatSafe(p3Obj); }

                    float c = player.CurrentHealth;
                    float m = player.MaxHealth;
                    NormalizeHealth(ref c, ref m, previousMax);

                    if (m <= 0f && c <= 0f && previousMax > 0f) { c = previousCurrent; m = previousMax; }

                    player.CurrentHealth = c;
                    player.MaxHealth = m;
                }
            }
        }

        private void HandleHealthUpdates(Dictionary<byte, object> parameters)
        {
            if (SelfOnlyTestMode) return;

            try
            {
                if (!parameters.TryGetValue(0, out var idObj) || idObj is not IList idList) return;

                var currentList = parameters.TryGetValue(2, out var currObj) ? currObj as IList : null;
                var maxList = parameters.TryGetValue(3, out var maxObj) ? maxObj as IList : null;
                bool looseStyleBatch = currentList == null && maxList != null;

                lock (_syncLock)
                {
                    for (int i = 0; i < idList.Count; i++)
                    {
                        int id = GetIntSafe(idList[i]);
                        var player = _otherPlayersList.FirstOrDefault(p => p.Id == id);
                        if (player == null) continue;

                        _lastHealthSeenById[id] = DateTime.UtcNow;

                        float previousMax = player.MaxHealth;
                        float previousCurrent = player.CurrentHealth;

                        if (!looseStyleBatch && currentList != null && i < currentList.Count) player.CurrentHealth = GetFloatSafe(currentList[i]);
                        if (!looseStyleBatch && maxList != null && i < maxList.Count) player.MaxHealth = GetFloatSafe(maxList[i]);
                        if (looseStyleBatch && i < maxList.Count) player.CurrentHealth = GetFloatSafe(maxList[i]);

                        float c = player.CurrentHealth;
                        float m = player.MaxHealth;
                        NormalizeHealth(ref c, ref m, previousMax);

                        if (m <= 0f && c <= 0f && previousMax > 0f) { c = previousCurrent; m = previousMax; }

                        player.CurrentHealth = c;
                        player.MaxHealth = m;
                    }
                }
            }
            catch (System.Exception ex) { System.Console.WriteLine($"Error Code : 80 | {ex.Message}"); }
        }

        private float GetFloatSafe(object? obj)
        {
            if (obj == null) return 0f;
            try
            {
                if (obj is byte[] bytes)
                {
                    if (bytes.Length == 4) return BitConverter.ToSingle(bytes, 0);
                    if (bytes.Length == 8) return (float)BitConverter.ToDouble(bytes, 0);
                }
                return Convert.ToSingle(obj);
            }
            catch (System.Exception ex) { return 0f; }
        }

        private int GetIntSafe(object? obj)
        {
            if (obj == null) return 0;
            try
            {
                if (obj is byte[] bytes)
                {
                    if (bytes.Length == 4) return BitConverter.ToInt32(bytes, 0);
                    if (bytes.Length == 2) return BitConverter.ToInt16(bytes, 0);
                    if (bytes.Length == 1) return bytes[0];
                }
                return Convert.ToInt32(obj);
            }
            catch (System.Exception ex) { return 0; }
        }

        private int[] ParseEquipmentIds(object equipObj)
        {
            if (equipObj == null) return new int[0];
            if (equipObj is int[] intArr) return intArr;
            if (equipObj is IEnumerable enumerable)
            {
                var result = new List<int>();
                foreach (var item in enumerable) result.Add(ExtractItemId(item));
                return result.ToArray();
            }
            return new[] { ExtractItemId(equipObj) };
        }

        private int ExtractItemId(object item)
        {
            int direct = GetIntSafe(item);
            if (direct > 0) return direct;
            if (item is IDictionary dict)
            {
                string[] commonKeys = { "ItemId", "itemId", "Id", "id", "Type", "type" };
                foreach (var key in commonKeys) { if (dict.Contains(key)) { int v = GetIntSafe(dict[key]); if (v > 0) return v; } }
                foreach (DictionaryEntry kv in dict) { int v = ExtractItemId(kv.Value); if (v > 0) return v; }
            }
            if (item is IList list) { foreach (var element in list) { int v = ExtractItemId(element); if (v > 0) return v; } }
            return 0;
        }

        private void NormalizeHealth(ref float current, ref float max, float previousMax)
        {
            if (current < 0f) current = 0f;
            if (max < 0f) max = 0f;
            if (previousMax > 200f && max > 0f && max <= 100f && current >= 0f && current <= max) { float ratio = max > 0f ? (current / max) : 0f; max = previousMax; current = ratio * max; }
            if (previousMax > 200f && max > 200f && current >= 0f && current <= 1.5f) { current *= max; }
            if (max <= 0f && current > 0f) { max = previousMax > 0f ? Math.Max(previousMax, current) : current; return; }
            if (current > max && max > 0f)
            {
                float normalDiff = previousMax > 0f ? Math.Abs(max - previousMax) : float.MaxValue;
                float swappedDiff = previousMax > 0f ? Math.Abs(current - previousMax) : 0f;
                if (swappedDiff < normalDiff || previousMax <= 0f) (current, max) = (max, current);
            }
            if (current > max && max > 0f) current = max;
        }
    }
}


