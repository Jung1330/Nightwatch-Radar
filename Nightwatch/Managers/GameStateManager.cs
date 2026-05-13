using AlbionDataHandlers.Entities;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Nightwatch.Managers
{
    public class GameStateManager
    {
        private Player _localPlayer = new Player();
        private List<Mob> _mobs = new List<Mob>();
        private List<Harvestable> _harvestables = new List<Harvestable>();
        private List<Player> _otherPlayers = new List<Player>();

        private readonly List<Mob> _debugMobs = new List<Mob>();
        private readonly List<Harvestable> _debugHarvestables = new List<Harvestable>();

        public string CurrentMapId { get; private set; } = "";
        private readonly object _stateLock = new object();

        public void SetCurrentMap(string mapId)
        {
            if (CurrentMapId != mapId || mapId == "LEAVING_ZONE")
            {
                CurrentMapId = mapId;
                ClearAllData();
            }
        }

        // --- SÝMÜLATÖR METOTLARI ---

        // 1. Fake Mob Ekle
        public void AddDebugMob(int typeId, float x, float y, string name)
        {
            lock (_stateLock)
            {
                var m = new Mob { Id = -Math.Abs(Guid.NewGuid().GetHashCode()), TypeId = typeId, PositionX = x, PositionY = y, CurrentLerpedX = x, CurrentLerpedY = y, Name = name };
                _debugMobs.Add(m); // Gerçek listeye deðil, VIP listeye ekle
            }
        }

        // 2. Fake Resource Ekle
        public void AddDebugHarvestable(int typeId, int tier, int count, int capacity, int enchant, float x, float y)
        {
            lock (_stateLock)
            {
                var h = new Harvestable { Id = -Math.Abs(Guid.NewGuid().GetHashCode()), Type = typeId, Tier = tier, Count = count, Capacity = capacity, PositionX = x, PositionY = y, CurrentLerpedX = x, CurrentLerpedY = y, EnchantmentLevel = enchant };
                _debugHarvestables.Add(h); // Gerçek listeye deðil, VIP listeye ekle
            }
        }

        // 3. Ekrana Çizdirirken Ýkisini Birleþtir
        public void GetMobs(List<Mob> buffer)
        {
            lock (_stateLock)
            {
                buffer.AddRange(_mobs);      // Gerçek moblar
                buffer.AddRange(_debugMobs); // Simülatör moblarý (Ezilmez)
            }
        }
        public void GetHarvestables(List<Harvestable> buffer)
        {
            lock (_stateLock)
            {
                buffer.AddRange(_harvestables);      // Gerçek kaynaklar
                buffer.AddRange(_debugHarvestables); // Simülatör kaynaklarý (Ezilmez)
            }
        }

        // 4. Tablodan Silme
        public void RemoveDebugEntity(int id)
        {
            lock (_stateLock)
            {
                _debugMobs.RemoveAll(x => x.Id == id);
                _debugHarvestables.RemoveAll(x => x.Id == id);
            }
        }

        // 5. Tüm Simülasyonu Temizle
        public void ClearAllData()
        {
            lock (_stateLock)
            {
                _mobs.Clear();
                _harvestables.Clear();
                _otherPlayers.Clear();
                _debugMobs.Clear();
                _debugHarvestables.Clear();
            }
        }
        /* Eski yöntem
        public void UpdateLocalPlayer(Player p)
        {
            _localPlayer.PositionX = p.PositionX;
            _localPlayer.PositionY = p.PositionY;
            _localPlayer.CurrentLerpedX = p.PositionX;
            _localPlayer.CurrentLerpedY = p.PositionY;
        }*/

        public void UpdateLocalPlayer(Player p)
        {
            lock (_stateLock) // YENÝ: Çakýþmalarý ve ýþýnlanmalarý engeller!
            {
                _localPlayer.Id = p.Id;
                _localPlayer.Name = p.Name;
                _localPlayer.Guild = p.Guild;
                _localPlayer.Alliance = p.Alliance;
                _localPlayer.Faction = p.Faction;
                _localPlayer.CurrentHealth = p.CurrentHealth;
                _localPlayer.MaxHealth = p.MaxHealth;
                _localPlayer.Equipment = p.Equipment?.ToArray() ?? Array.Empty<int>();
                _localPlayer.PositionX = p.PositionX;
                _localPlayer.PositionY = p.PositionY;
                _localPlayer.CurrentLerpedX = p.PositionX;
                _localPlayer.CurrentLerpedY = p.PositionY;
            }
        }
        public void UpdateOtherPlayers(IEnumerable<Player> players)
        {
            lock (_stateLock)
            {
                // Listeyi tamamen yenile (Snapshot mantýðý)
                _otherPlayers.Clear();
                _otherPlayers.AddRange(players);
            }
        }

        public void RemovePlayer(int id)
        {
            lock (_stateLock)
            {
                _otherPlayers.RemoveAll(x => x.Id == id);
            }
        }

        public void UpdateMobsState(IEnumerable<Mob> newMobs)
        {
            lock (_stateLock)
            {
                _mobs.Clear();
                foreach (var mob in newMobs)
                {
                    mob.CurrentLerpedX = mob.PositionX;
                    mob.CurrentLerpedY = mob.PositionY;
                    _mobs.Add(mob);
                }
            }
        }

        public void UpdateHarvestablesState(IEnumerable<Harvestable> newHarvestables)
        {
            lock (_stateLock)
            {
                _harvestables.Clear();
                _harvestables.AddRange(newHarvestables);
            }
        }

        public void RemoveHarvestables(IEnumerable<int> idsToRemove)
        {
            lock (_stateLock)
            {
                var removeSet = idsToRemove as HashSet<int> ?? new HashSet<int>(idsToRemove);
                _harvestables.RemoveAll(x => removeSet.Contains(x.Id));
            }
        }

        // --- FPS DOSTU HIZLI MESAFE HESAPLAMA (KAREKÖK ÝPTAL EDÝLDÝ) ---
        private float GetDistanceSquared(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (dx * dx) + (dy * dy);
        }

        public void Update()
        {
            lock (_stateLock)
            {
                if (_localPlayer == null) return;

                float px = _localPlayer.PositionX;
                float py = _localPlayer.PositionY;

                // maxDist 400 ise karesi tam 160.000 yapar! Artýk karekök almadan direkt karesiyle karþýlaþtýracaðýz.
                float maxDistSquared = 160000.0f;

                // Gerçek listelerden uzak objeleri sil
                PruneMobsByDistance(_mobs, px, py, maxDistSquared);
                PruneHarvestablesByDistance(_harvestables, px, py, maxDistSquared);
                PrunePlayersByDistance(_otherPlayers, px, py, maxDistSquared);

                // Simülatör (Sahte) objeleri de çok uzaklaþýrsa sil ki test yaparken ekran þiþmesin
                PruneMobsByDistance(_debugMobs, px, py, maxDistSquared);
                PruneHarvestablesByDistance(_debugHarvestables, px, py, maxDistSquared);
            }
        }

        private static void PruneMobsByDistance(List<Mob> mobs, float px, float py, float maxDistSquared)
        {
            for (int i = mobs.Count - 1; i >= 0; i--)
            {
                var m = mobs[i];
                if (((px - m.PositionX) * (px - m.PositionX)) + ((py - m.PositionY) * (py - m.PositionY)) > maxDistSquared)
                    mobs.RemoveAt(i);
            }
        }

        private static void PruneHarvestablesByDistance(List<Harvestable> harvestables, float px, float py, float maxDistSquared)
        {
            for (int i = harvestables.Count - 1; i >= 0; i--)
            {
                var h = harvestables[i];
                if (((px - h.PositionX) * (px - h.PositionX)) + ((py - h.PositionY) * (py - h.PositionY)) > maxDistSquared)
                    harvestables.RemoveAt(i);
            }
        }

        private static void PrunePlayersByDistance(List<Player> players, float px, float py, float maxDistSquared)
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                var p = players[i];
                if (((px - p.PositionX) * (px - p.PositionX)) + ((py - p.PositionY) * (py - p.PositionY)) > maxDistSquared)
                    players.RemoveAt(i);
            }
        }
        public Player GetPlayer()
        {
            lock (_stateLock)
            {
                return new Player
                {
                    Id = _localPlayer.Id,
                    Name = _localPlayer.Name,
                    Guild = _localPlayer.Guild,
                    Alliance = _localPlayer.Alliance,
                    Faction = _localPlayer.Faction,
                    PositionX = _localPlayer.PositionX,
                    PositionY = _localPlayer.PositionY,
                    CurrentLerpedX = _localPlayer.CurrentLerpedX,
                    CurrentLerpedY = _localPlayer.CurrentLerpedY,
                    CurrentHealth = _localPlayer.CurrentHealth,
                    MaxHealth = _localPlayer.MaxHealth,
                    Equipment = _localPlayer.Equipment?.ToArray() ?? Array.Empty<int>()
                };
            }
        }

        public void GetOtherPlayers(List<Player> buffer)
        {
            lock (_stateLock)
            {
                buffer.Clear();
                buffer.AddRange(_otherPlayers);
            }
        }
    }
}


