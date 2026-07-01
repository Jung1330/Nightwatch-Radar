using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AlbionDataHandlers;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Handlers;
using Nightwatch.Managers;

namespace Nightwatch.UserControls.AlbionOverlay.Map
{
    public class NetworkPacketDiagnostic
    {
        public static void RunDiagnostic(GameStateManager manager, Dictionary<int, Nightwatch.MobInfo> mobDatabase, string mode = "All")
        {
            Task.Run(() =>
            {
                try
                {
                    Nightwatch.UIConsole.Log("==================================================", Nightwatch.LogLevel.Info);
                    Nightwatch.UIConsole.Log("    ALBION RADAR - RAW PACKET DIAGNOSTIC TEST     ", Nightwatch.LogLevel.Info);
                    Nightwatch.UIConsole.Log("==================================================", Nightwatch.LogLevel.Info);

                    // Handlers oluşturuluyor (Gerçek parser mekanizması)
                    var parser = new AlbionDataParser();
                    var mobsHandler = new MobsHandler();
                    var harvestableHandler = new HarvestableHandler();

                    parser.RegisterEventHandler(mobsHandler);
                    parser.RegisterEventHandler(harvestableHandler);

                    mobsHandler.Mobs.Subscribe(manager.UpdateMobsState);
                    harvestableHandler.Harvestables += manager.UpdateHarvestablesState;

                    Nightwatch.UIConsole.Log("[INFO] Handlers and Subscriptions ready. Generating massive test...", Nightwatch.LogLevel.Info);
                    Thread.Sleep(1000);

                    manager.ClearAllData();
                    Nightwatch.UIConsole.Log("[INFO] GameState cleared.", Nightwatch.LogLevel.Info);
                    Thread.Sleep(500);

                    Random rnd = new Random();
                    int fakeIdCounter = -5000;

                    float baseX = 0f;
                    float baseY = 0f;
                    var p = manager.GetPlayer();
                    if (p != null)
                    {
                        baseX = p.PositionX;
                        baseY = p.PositionY;
                    }

                    float startX = baseX - 50f;
                    float currentX = startX;
                    float currentY = baseY - 50f;

                    if (mode == "All")
                    {
                        // 1. TEST: GİZLİ SANDIKLAR (HIDDEN CHESTS) BURADAN KALDIRILDI. MOB DÖNGÜSÜNE TAŞINDI.

                        // TÜM DİĞER KAYNAKLAR (T1 - T8, 0-3 Enchant, Types: Wood=0, Rock=6, Fiber=11, Hide=16, Ore=23)
                        int[] resourceTypes = { 0, 6, 11, 16, 23 };
                        foreach (int type in resourceTypes)
                        {
                            for (int tier = 1; tier <= 8; tier++)
                            {
                                for (int ench = 0; ench <= 3; ench++)
                                {
                                    var packet = new Dictionary<byte, object>
                                    {
                                        { 0, fakeIdCounter-- },
                                        { 5, (byte)type },
                                        { 7, (byte)tier },
                                        { 8, new float[] { currentX, currentY } },
                                        { 10, (byte)5 },
                                        { 11, (byte)ench },
                                    };

                                    currentX += 3f;
                                    if (currentX > baseX + 50f) { currentX = startX; currentY += 4f; }

                                    harvestableHandler.OnEvent(EventCodes.NewHarvestableObject, packet);
                                    Thread.Sleep(1);
                                }
                            }
                            Nightwatch.UIConsole.Log($"[SUCCESS] Spawned All Harvestables for Type {type}", Nightwatch.LogLevel.Info);
                        }
                    }

                    if (mode == "All" || mode == "Chests")
                    {
                        int[] hiddenChests = { 795, 798, 800, 2637 };
                        foreach (int hId in hiddenChests)
                        {
                            var packet = new Dictionary<byte, object>
                            {
                                { 0, fakeIdCounter-- },
                                { 1, hId },
                                { 2, 5 },
                                { 7, new float[] { currentX, currentY } }
                            };

                            currentX += 3f;
                            if (currentX > baseX + 50f) { currentX = startX; currentY += 4f; }

                            mobsHandler.OnEvent(EventCodes.NewMob, packet);
                            Thread.Sleep(5);
                        }
                    }

                    if (mode == "All" || mode == "Bosses" || mode == "Mists")
                    {
                        // 2. TEST: TÜM MOBLAR
                        Nightwatch.UIConsole.Log($"[PACKET INCOMING] Simulating Mobs ({mode} mode)...", Nightwatch.LogLevel.Warning);
                        
                        int spawnedCount = 0;
                        foreach (var kvp in mobDatabase)
                        {
                            int mId = kvp.Key;
                            var info = kvp.Value;
                            
                            if (info == null || string.IsNullOrEmpty(info.Name)) continue;
                            
                            bool isBoss = info.Name.Contains("BOSS", StringComparison.OrdinalIgnoreCase) || 
                                          info.Name.Contains("ASPECT", StringComparison.OrdinalIgnoreCase) || 
                                          info.Name.Contains("VETERAN", StringComparison.OrdinalIgnoreCase);
                                          
                            bool isMist = info.Name.Contains("WISP", StringComparison.OrdinalIgnoreCase) || 
                                          info.Name.Contains("MIST", StringComparison.OrdinalIgnoreCase) ||
                                          info.Name.Contains("CRYSTAL", StringComparison.OrdinalIgnoreCase);

                            if (mode == "Bosses" && !isBoss) continue;
                            if (mode == "Mists" && !isMist) continue;

                            var packet = new Dictionary<byte, object>
                            {
                                { 0, fakeIdCounter-- },
                                { 1, mId },
                                { 4, currentX },
                                { 5, currentY },
                                { 21, (info.Tier > 0) ? info.Tier : 6 }, // NetworkTier
                                { 33, rnd.Next(0, 4) }, // Enchant
                                { 34, rnd.Next(0, 2) }  // Rarity
                            };

                            currentX += 3f;
                            if (currentX > baseX + 50f) { currentX = startX; currentY += 4f; }

                            mobsHandler.OnEvent(EventCodes.NewMob, packet);
                            spawnedCount++;
                            
                            // Her 50 mobda bir log yazdırıp azıcık bekle
                            if (spawnedCount % 50 == 0)
                            {
                                Nightwatch.UIConsole.Log($"[SUCCESS] Spawned {spawnedCount} mobs...", Nightwatch.LogLevel.Info);
                                Thread.Sleep(10);
                            }
                        }
                        Nightwatch.UIConsole.Log($"[INFO] Total Mobs Spawned: {spawnedCount}", Nightwatch.LogLevel.Info);
                    }

                    Nightwatch.UIConsole.Log("==================================================", Nightwatch.LogLevel.Info);
                    Nightwatch.UIConsole.Log(" DIAGNOSTIC TEST COMPLETE! CHECK RADAR VISUALS.   ", Nightwatch.LogLevel.Info);
                    Nightwatch.UIConsole.Log("==================================================", Nightwatch.LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Nightwatch.UIConsole.Log($"[ERROR] Diagnostic failed: {ex.Message}", Nightwatch.LogLevel.Error);
                }
            });
        }
    }
}
