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

    // Throttle kaldırıldı

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
            case (int)EventCodes.NewHuntTrack:
            case (int)EventCodes.HuntMissionStepStateUpdate:
            case (int)EventCodes.HuntStarted:
            case (int)EventCodes.HuntFinished:
                HandleHuntTrack(parameters);
                break;
            case (int)EventCodes.NewMistsStaticEntrance:
                HandleChestSpawn(parameters);
                break;
            case (int)EventCodes.MistsEntranceDataChanged:
                HandleChestRarity(parameters);
                break;
            case (int)EventCodes.NewLootChest:
            case (int)EventCodes.UpdateLootChest:
            case (int)EventCodes.UpdateLootProtectedByMobsWithMinimapDisplay: // 396
                HandleLootChestSpawn(parameters);
                break;
            case (int)EventCodes.LootChestOpened:
                HandleLeave(parameters);
                break;
            case (int)EventCodes.NewMobSoul:
            case (int)EventCodes.NewHellgateShrine:
            case (int)EventCodes.UpdateHellgateShrine:
                HandleAbbeyStatueSpawn(parameters);
                break;
            // 397/398 (NewShrine / UpdateShrine): KULLANICI KARARI -> koda eklenir ama
            // SU AN RADARA CIZILMEZ; yalnizca konsola iz dusurulur (ileride istenirse isaret uretilir).
            case (int)EventCodes.NewShrine:
            case (int)EventCodes.UpdateShrine:
                HandleShrine(parameters);
                break;
        }
    }

    /// <summary>
    /// 397 (NewShrine) / 398 (UpdateShrine): sunak nesneleri.
    /// Su an SADECE teshis izi (konsol) - radar/isaret YOK, state DEGISMEZ.
    /// </summary>
    private void HandleShrine(Dictionary<byte, object> parameters)
    {
        try
        {
            var shrine = AlbionDataHandlers.Packets.Events.NewShrine.From(parameters);
            if (shrine == null || shrine.F0 == 0) return;

            var upd = AlbionDataHandlers.Packets.Events.UpdateShrine.From(parameters);
            string name = !string.IsNullOrEmpty(shrine.F3) ? shrine.F3 : (shrine.F4 ?? "");
            string posTxt = shrine.F1 != null && shrine.F1.Length >= 2
                ? string.Format("({0:F1}, {1:F1})", shrine.F1[0], shrine.F1[1])
                : "(konum yok)";

            if (ShrineTraceEnabled)
            {
                DebugShrine(string.Format("[Shrine] id={0} pos={1} isim='{2}' tip={3} damga={4}{5}",
                    shrine.F0, posTxt, name, shrine.F5, shrine.F6,
                    (upd != null && upd.F0 != 0 ? $" | guncelleme: durum={upd.F1}" : "")));
            }
        }
        catch { }
    }

    /// <summary>
    /// Sunak izi ANAHTARI (varsayilan: KAPALI).
    /// KULLANICI NOTU: "boyle seyler icin bir checkbox acmak lazim" -> ileride DevTools'a
    /// checkbox olarak baglanacak; simdilik elle true yapilmadikca konsola HICBIR SEY yazilmaz.
    /// </summary>
    public static bool ShrineTraceEnabled = false;

    /// <summary>Sunak izi: uygulama ici Console'a yazar (DebugLog kanali MobsHandler'da yok).</summary>
    private static void DebugShrine(string msg)
    {
        try { System.Console.WriteLine(msg); } catch { }
    }
    // --- T2: Yayın sınırlayıcı ---
    // Eskiden HER mob hareket paketinde tüm mob listesi abonelere yeniden inşa ettiriliyordu
    // (GroupBy + sözlük + listeyi sil/yeniden ekle) ve bu, kilit TUTULURKEN yapılıyordu.
    // Artık değişiklik sadece işaretlenir; yayın en fazla ~25 kez/sn yapılır ve kilit dışında tetiklenir.
    private int _publishPending;              // 0/1
    private int _publishGate;                 // aynı anda tek yayıncı
    private long _lastPublishTicks;
    private int _lastPublishedCount = -1;     // yapısal değişim tespiti (mob eklendi/silindi mi?)
    private const int PublishTickMs = 25;         // kontrol sıklığı
    private const int MinPublishIntervalMs = 40;  // iki yayın arası en az süre
    private readonly System.Threading.Timer _publishTimer;

    public MobsHandler()
    {
        _publishTimer = new System.Threading.Timer(_ => PublishTick(), null, PublishTickMs, PublishTickMs);
    }

    /// <summary>Liste değişti. Yapısal değişim (ekle/sil) en geç ~25 ms'de, konum değişimi ~40 ms'de yayınlanır.</summary>
    private void RequestPublish() => System.Threading.Interlocked.Exchange(ref _publishPending, 1);

    private void PublishTick()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _publishGate, 1, 0) != 0) return;

        try
        {
            if (System.Threading.Interlocked.Exchange(ref _publishPending, 0) == 0) return;

            // Kilit ALTINDA yalnızca KOPYA alınır; event/OnNext kilit DIŞINDA tetiklenir.
            List<Mob> snapshot;
            lock (_lockObject) { snapshot = new List<Mob>(_mobs); }

            // YAPISAL değişim (mob eklendi/silindi) asla bekletilmez -> yeni mob en geç 25 ms'de radarda.
            // Yalnızca konum güncellemeleri birleştirilir (en fazla ~25 yayın/sn).
            bool structural = snapshot.Count != _lastPublishedCount;

            long now = DateTime.UtcNow.Ticks;
            if (!structural && now - System.Threading.Interlocked.Read(ref _lastPublishTicks) < MinPublishIntervalMs * TimeSpan.TicksPerMillisecond)
            {
                System.Threading.Interlocked.Exchange(ref _publishPending, 1); // çok erken, sonraki tike bırak
                return;
            }

            _lastPublishedCount = snapshot.Count;
            System.Threading.Interlocked.Exchange(ref _lastPublishTicks, now);

            try { Mobs.OnNext(snapshot); }
            catch { /* abone hatası paket akışını durdurmasın */ }
        }
        finally
        {
            System.Threading.Volatile.Write(ref _publishGate, 0);
        }
    }

    private void HandleMobChangeState(Dictionary<byte, object> parameters)
    {
        // URETILEN PAKET MODELI (okunabilirlik): [0] = EntityId, [1] = Enchant
        var state = AlbionDataHandlers.Packets.Events.MobChangeState.From(parameters);
        if (state == null || state.EntityId == 0) return;
        int entityId = (int)state.EntityId;
        int newEnchantLevel = Convert.ToInt32(state.Enchant);   // [1] Enchantment Level

        lock (_lockObject)
        {
            var mobToUpdate = _mobs.FirstOrDefault(m => m.Id == entityId);
            if (mobToUpdate != null)
            {
                // Mobun enchant'ını güncelle (Code: 47 paketinden gelen bilgi)
                mobToUpdate.EnchantmentLevel = newEnchantLevel;

                // Mobs listesini UI'a gönder (Radar güncellensin)
                RequestPublish();
            }
        }
    }
    private void HandleNewMob(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(0, out var idObj)) return;

        // URETILEN PAKET MODELI (kod 123 = NewMob) - Packet Inspector panelinin
        // gosterdigi alan adlariyla ayni:
        //   [0] EntityId · [1] MobTypeId · [2] FlaggingStatus · [7] Position
        //   [8] SecondPosition · [13] CurrentHealth · [21] F21 (network tier)
        //   [33] F33 (isim) · [34] Rarity
        var pkt = AlbionDataHandlers.Packets.Events.NewMob.From(parameters);
        if (pkt == null) return;

        // Kimlik modelden; tel formatinda kisa bicim (byte[4]/byte[2]) gelirse
        // eski guvenli okuma yedek olarak korunur (davranis degismez).
        int id = (int)pkt.EntityId;
        if (id == 0) id = GetIntSafe(idObj);
        if (id == 0) return;

        // [1] MobTypeId (model: short). Canli kayitta 120-2593 araliginda.
        int typeId = pkt.MobTypeId;
        // DUZELTME (canli kayitla dogrulandi):
        //   [21] = network tier (gercek paketlerde COGUNLUKLA YOK)
        //   [2]  = FlaggingStatus (0=Passive, 255=Hostile) -> TIER DEGIL!
        // Eski kod [21] yoksa tier'i [2]'den aliyordu; bu yuzden moblar radarda
        // "T255" olarak gorunuyordu. Artik gecersiz deger 0'a cekilir; tier
        // bilgisi mob DB'sinden/isimden gelir.
        int networkTier = (int)pkt.F21;
        if (networkTier < 1 || networkTier > 8)
            networkTier = 0;

        // DIKKAT: konum yolu BILEREK degistirilmedi (TryGetPosFromArrayParam +
        // IsValidWorldPosition). Model alanlari: [7] Position, [8] SecondPosition.
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
        
        // ALBION UPDATE FALLBACK: Check keys 10 and 11 for new position floats
        if (!IsValidWorldPosition(posX, posY))
        {
            if (parameters.TryGetValue(10, out var p10) && parameters.TryGetValue(11, out var p11))
            {
                posX = GetFloatSafe(p10);
                posY = GetFloatSafe(p11);
            }
        }

        float experience = pkt.CurrentHealth;   // [13] (model alani adi: CurrentHealth)
        // NOT (kod denetimi + canli kayit): [32] tabloya gore BOOL, [31] ise FlaggingStatus'tur
        // -> ikisi de isim DEGIL. Eski kod bunlari isim sanip okuyordu; Convert.ChangeType
        // (bool -> string) yuzunden isim "False" olabiliyordu. Gercek isim [33]'ten (string)
        // veya mob DB'sinden gelir.
        string name = string.Empty;

        int enchantmentLevel = 0;
        if (parameters.TryGetValue(33, out var p33))
        {
            // [33] tabloya gore string -> isim. Baska tipte gelirse enchant kabul edilir (eski davranis).
            // (TIP AYRIMI BILEREK KORUNDU: model alani string oldugu icin "ham tip" bilgisi
            //  yalnizca burada gerekli.)
            if (p33 is string s33) name = s33;
            else enchantmentLevel = EventHandlerUtils.ExtractValue<int>(parameters, 33, 0);
        }
        int rarity = pkt.Rarity;   // [34]
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
            RequestPublish(); // Anlık güncelleme - mob hemen radara düşsün
        }
    }

    private void HandleMove(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(0, out var idObj)) return;
        int id = GetIntSafe(idObj);
        if (id == 0) return;

        // NOT: asagidaki [4]/[5] okumalari canli tel formatinda YOK (Move paketi sadece
        // [0] id + [1] blob tasiyor) -> olu okuma. Gercek konum TryGetMovePositionFromRaw'dan
        // gelir. DAVRANIS DEGISTIRILMEDI (bilerek dokunulmadi).
        float posX = EventHandlerUtils.ExtractValue<float>(parameters, 4);
        float posY = EventHandlerUtils.ExtractValue<float>(parameters, 5);
        if (TryGetMovePositionFromRaw(parameters, out var rx, out var ry))
        {
            posX = rx;
            posY = ry;
        }

        bool updated = false;
        lock (_lockObject)
        {
            var mobToUpdate = _mobs.FirstOrDefault(m => m.Id == id);
            if (mobToUpdate != null)
            {
                // Eğer pozisyon aynıysa gereksiz yayın yapma
                float dx = mobToUpdate.PositionX - posX;
                float dy = mobToUpdate.PositionY - posY;
                if (dx * dx + dy * dy > 0.0001f)
                {
                    mobToUpdate.PositionX = posX;
                    mobToUpdate.PositionY = posY;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            lock (_lockObject)
            {
                RequestPublish();
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
                RequestPublish();
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
            RequestPublish();
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

    private void HandleHuntTrack(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;

            // URETILEN PAKET MODELI (kod 559 = NewHuntTrack):
            //   [0] id · [1] konum · [3] isim · [6] tier · [7] nadirlik
            // NOT: Bu metot 555/556/558/559 icin de cagrilir; onlarin [0] alani bir GUID'dir
            // (model 0 doner) -> o durumda asagidaki eski ham okuma gecerli kalir.
            var track = AlbionDataHandlers.Packets.Events.NewHuntTrack.From(parameters);

            int id = track != null ? unchecked((int)track.F0) : 0;
            if (id == 0) id = GetIntSafe(idObj);
            if (id == 0) return;

            string name = track != null && !string.IsNullOrEmpty(track.F3) ? track.F3 : null;
            if (name == null) name = EventHandlerUtils.ExtractValue<string>(parameters, 3) ?? "Track";

            float[] pos = null;
            if (track != null && track.F1 != null) pos = track.F1;
            else if (parameters.TryGetValue(1, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }

            int trackTier = track != null ? track.F6 : EventHandlerUtils.ExtractValue<int>(parameters, 6, 0);
            int trackRarity = track != null ? track.F7 : EventHandlerUtils.ExtractValue<int>(parameters, 7, 0);

            if (pos != null && pos.Length >= 2)
            {
                var mob = new Mob
                {
                    Id = id,
                    TypeId = 55600,
                    Experience = 0,
                    Name = name,
                    EnchantmentLevel = 0,
                    NetworkTier = trackTier,
                    Rarity = trackRarity,
                    PositionX = pos[0],
                    PositionY = pos[1]
                };

                lock (_lockObject)
                {
                    var existingMob = _mobs.FirstOrDefault(m => m.Id == mob.Id);
                    if (existingMob != null) _mobs.Remove(existingMob);
                    _mobs.Add(mob);
                    RequestPublish();
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

            // Bu metot yalnizca 523 (NewMistsStaticEntrance) icin cagrilir.
            // URETILEN PAKET MODELI (kod 523): [0] id · [1] GUID (16 bayt) · [2] KONUM
            // DUZELTME: eskiden konum [1]'de araniyordu; [1] bir GUID oldugu icin ilk iki bayti
            // koordinat saniliyordu ("Mists Portal" isareti yanlis yere dusuyordu).
            var p523 = AlbionDataHandlers.Packets.Events.NewMistsStaticEntrance.From(parameters);
            if (p523 != null && p523.F0 != 0) id = unchecked((int)p523.F0);
            if (id == 0) return;

            float[] pos = null;
            if (p523 != null && p523.F2 != null)
            {
                pos = p523.F2;   // [2] Position (model)
            }
            else if (parameters.TryGetValue(1, out var posObj))
            {
                // yedek: model bos ise eski ham okuma (davranis degismez)
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
                    RequestPublish();
                }
            }
        }
        catch { }
    }

    private void HandleChestRarity(Dictionary<byte, object> parameters)
    {
        try
        {
            // Bu metot yalnizca 532 (MistsEntranceDataChanged) icin cagrilir.
            // URETILEN PAKET MODELI (kod 532): [0] id · [1] nadirlik · [2] zaman damgasi
            var pkt = AlbionDataHandlers.Packets.Events.MistsEntranceDataChanged.From(parameters);
            if (pkt == null) return;

            int id = unchecked((int)pkt.F0);
            if (id == 0 && parameters.TryGetValue(0, out var idObj)) id = GetIntSafe(idObj);   // yedek (eski davranis)
            if (id == 0) return;

            int rarity = pkt.F1;
            // ESKI DAVRANIS KORUNUR: [2] long ya da IConvertible ise okunur, yoksa 0.
            long unlockTicks = pkt.F2;
            if (unlockTicks == 0 && parameters.TryGetValue(2, out var ticksObj) && ticksObj != null)
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
                RequestPublish();
            }
        }
        catch { }
    }

    private void HandleLootChestSpawn(Dictionary<byte, object> parameters)
    {
        try
        {
            // URETILEN PAKET MODELI (okunabilirlik): parameters[5] gibi sihirli indeksler
            // yerine isimli alanlar -> ChestId, Position, ShortName, UniqueName, Rarity, UnlockTicks
            var chest = AlbionDataHandlers.Packets.Events.NewLootChest.From(parameters);
            if (chest == null || chest.ChestId == 0) return;

            int id = (int)chest.ChestId;
            float[] pos = chest.Position;

            if (pos == null || pos.Length < 2) return;

            string chestName = string.IsNullOrEmpty(chest.ShortName) ? "LOOTCHEST" : chest.ShortName;

            // MIST portalleri DungeonHandler tarafından işleniyor, burada atlıyoruz
            // (MISTS_PERSONAL_SMALL_LC, TREASURE_COFFER_MISTS_SOLO vb.)
            if (!string.IsNullOrEmpty(chestName))
            {
                string upperCheck = chestName.ToUpperInvariant();
                if (upperCheck.Contains("MIST") || upperCheck.Contains("MISTS"))
                    return;
            }

            if (chestName != null && chestName.ToLowerInvariant().Contains("mist")
                && !string.IsNullOrEmpty(chest.UniqueName))
            {
                chestName = chest.UniqueName;
            }

            long unlockTicks = chest.UnlockTicks;   // [7]

            // NOT: Eski kod [19] alanini nadirlik sanip 1-4 arasi okuyordu. Tabloya gore
            // [19] = bool (nadirlik DEGIL) -> bu OLU YOL kaldirildi. Gercek nadirlik [5]'tedir.
            int rarity = 0;

            if (chestName != null)
            {
                string lower = chestName.ToLowerInvariant();
                int parsedRarity = -1;

                // Sadece GERÇEK nadirlik kelimelerine bak (01/02 camp numaraları veya yeşil harita isimleri aldanmasın!)
                if (lower.Contains("legendary")) parsedRarity = 4;
                else if (lower.Contains("epic")) parsedRarity = 3;
                else if (lower.Contains("rare") && !lower.Contains("treasure")) parsedRarity = 2;
                else if (lower.Contains("uncommon")) parsedRarity = 1;
                else if (lower.Contains("standard") || lower.Contains("common")) parsedRarity = 0;

                if (parsedRarity != -1)
                {
                    rarity = parsedRarity;
                }
                else if (rarity == 0)
                {
                    // NOT: [31] okuması kaldırıldı — tabloya göre [31] = FlaggingStatus
                    // (canlıda hep 0), yani nadirlik değil; sonuçsuz bir okumaydı.
                    // [5] = sandik nadirligi (model alani: Rarity). Canlida 1, 2, 4, 6 goruldu.
                    // Eskiden 1-4 kabul ediliyordu -> 5+ degerler sessizce dusuyordu.
                    if (chest.Rarity >= 1 && chest.Rarity <= 8) rarity = chest.Rarity;
                }
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
                        TypeId = 51900,
                        Name = chestName,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1],
                        EnchantmentLevel = 0,
                        NetworkTier = 0,
                        Rarity = rarity,
                        UnlockTicks = unlockTicks
                    };
                    _mobs.Add(mob);
                }
                RequestPublish();
            }
        }
        catch { }
    }

    private void HandleAbbeyStatueSpawn(Dictionary<byte, object> parameters)
    {
        try
        {
            if (!parameters.TryGetValue(0, out var idObj)) return;

            // URETILEN PAKET MODELLERI (alanlar artik ISIMLI, kod basina dogru kutu):
            //   400 NewMobSoul          : [0] id · [2] konum · [4] tipId · [5] isim (orn. "mdrewardstatue")
            //   401 NewHellgateShrine   : [0] id · [1] konum · [4] tipId · [3] isim
            //   402 UpdateHellgateShrine: [0] id (konum YOK -> konum guncellenmez)
            var soul = AlbionDataHandlers.Packets.Events.NewMobSoul.From(parameters);
            var shrine = AlbionDataHandlers.Packets.Events.NewHellgateShrine.From(parameters);
            var shrineUpd = AlbionDataHandlers.Packets.Events.UpdateHellgateShrine.From(parameters);

            // DUZELTME: eskiden [1] "tipId" saniliyordu; 400'de [1] bir ZAMAN DAMGASI oldugu icin
            // isim "Statue = 639269..." gibi sacma cikiyordu. Artik dogru alanlar kullaniliyor.
            int id = 0;
            if (soul != null && soul.F0 != 0) id = unchecked((int)soul.F0);
            else if (shrine != null && shrine.F0 != 0) id = unchecked((int)shrine.F0);
            else if (shrineUpd != null && shrineUpd.F0 != 0) id = unchecked((int)shrineUpd.F0);
            if (id == 0) id = GetIntSafe(idObj);
            if (id == 0) return;

            int statueTypeId = 0;
            if (soul != null && soul.F4 != 0) statueTypeId = unchecked((int)soul.F4);
            else if (shrine != null && shrine.F4 != 0) statueTypeId = shrine.F4;
            if (statueTypeId == 0) statueTypeId = EventHandlerUtils.ExtractValue<int>(parameters, 4, 0);   // yedek

            float[] pos = null;
            if (soul != null && soul.F2 != null) pos = soul.F2;                 // 400: konum [2]
            else if (shrine != null && shrine.F1 != null) pos = shrine.F1;      // 401: konum [1]
            if (pos == null && parameters.TryGetValue(2, out var posObj))
            {
                if (posObj is float[] fArr) pos = fArr;
                else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
            }
            else if (pos == null && parameters.TryGetValue(3, out var posObj3))
            {
                if (posObj3 is float[] fArr3) pos = fArr3;
                else if (posObj3 is IList<float> list3) pos = new float[] { list3[0], list3[1] };
                else if (posObj3 is System.Collections.IList list23) pos = new float[] { Convert.ToSingle(list23[0]), Convert.ToSingle(list23[1]) };
            }

            if (pos == null || pos.Length < 2) return;

            // ISIM: pakette gelen gercek isim (400 -> [5], 401 -> [3]) once gelir; yoksa eski desen.
            string packetName = null;
            if (soul != null && !string.IsNullOrEmpty(soul.F5)) packetName = soul.F5;
            else if (shrine != null && !string.IsNullOrEmpty(shrine.F3)) packetName = shrine.F3;

            // ETIKET: ham prefab adi ("mdrewardstatue") yerine "Statue <id>" yazilir.
            // 400 NewMobSoul: [4] = ruhun ait oldugu mob tipId'si (orn. 1622) -> paketle konum dogrulanabiliyor.
            bool hamPrefabAdi = !string.IsNullOrEmpty(packetName)
                                && packetName.IndexOf("mdrewardstatue", StringComparison.OrdinalIgnoreCase) >= 0;
            string statueName;
            if (hamPrefabAdi || string.IsNullOrEmpty(packetName))
                statueName = statueTypeId > 0 ? $"Statue {statueTypeId}" : "Statue";
            else
                statueName = packetName.Replace("_", " ");

            lock (_lockObject)
            {
                var existingMob = _mobs.FirstOrDefault(m => m.Id == id);
                if (existingMob != null)
                {
                    existingMob.PositionX = pos[0];
                    existingMob.PositionY = pos[1];
                    existingMob.CurrentLerpedX = pos[0];
                    existingMob.CurrentLerpedY = pos[1];
                    existingMob.TypeId = 52000;
                    existingMob.Name = statueName;
                }
                else
                {
                    var mob = new Mob
                    {
                        Id = id,
                        TypeId = 52000,
                        Name = statueName,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        CurrentLerpedX = pos[0],
                        CurrentLerpedY = pos[1],
                        EnchantmentLevel = 0,
                        NetworkTier = 0,
                        Rarity = 0,
                        UnlockTicks = 0
                    };
                    _mobs.Add(mob);
                }
                RequestPublish();
            }
        }
        catch { }
    }
}


