using System;
using AlbionDataHandlers;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Handlers.MapHandler;

namespace Nightwatch.Managers
{
    /// <summary>
    /// Parser + handler + GameStateManager baglantisini TEK YERDE kurar.
    ///
    /// NEDEN: Normal akis (Program.cs) ve test harness (--replay) AYNI kurulumu
    /// kullanmali. Yoksa "testte calisiyor ama oyunda calismiyor" durumu olusur.
    /// </summary>
    public sealed class PacketPipeline
    {
        public GameStateManager Manager { get; }
        public AlbionDataParser Parser { get; }
        public MobsHandler Mobs { get; }
        public PlayersHandler Players { get; }
        public HarvestableHandler Harvestables { get; }
        public AlbionDataHandlers.Handlers.DungeonHandler.DungeonHandler Dungeons { get; }
        public AlbionDataHandlers.Handlers.UnknownPacketHandler.UnknownPacketHandler Unknown { get; }
        public MapChangeHandler Map { get; }

        public PacketPipeline(GameStateManager manager,
                              Action<string> onMapChanged = null,
                              Action<string> onDungeonDebug = null)
        {
            Manager = manager;

            Parser = new AlbionDataParser();
            Mobs = new MobsHandler();
            Players = new PlayersHandler();
            Harvestables = new HarvestableHandler();
            Dungeons = new AlbionDataHandlers.Handlers.DungeonHandler.DungeonHandler();
            Unknown = new AlbionDataHandlers.Handlers.UnknownPacketHandler.UnknownPacketHandler();

            if (onDungeonDebug != null)
            {
                AlbionDataHandlers.Handlers.DungeonHandler.DungeonHandler.DebugLog += onDungeonDebug;
                // EK: harita/mists teshis izi de ayni konsola dussun
                AlbionDataHandlers.Handlers.MapHandler.MapChangeHandler.DebugLog += onDungeonDebug;
            }

            Map = new MapChangeHandler(id =>
            {
                onMapChanged?.Invoke(id);
                manager.SetCurrentMap(id);
            });

            Parser.RegisterEventHandler(Mobs);
            Parser.RegisterEventHandler(Players);
            Parser.RegisterEventHandler(Harvestables);
            Parser.RegisterEventHandler(Dungeons);
            Parser.RegisterEventHandler(Unknown);
            Parser.RegisterEventHandler(Map);

            Mobs.Mobs.Subscribe(manager.UpdateMobsState);
            Harvestables.Harvestables += manager.UpdateHarvestablesState;
            Players.LocalPlayerPosition += manager.UpdateLocalPlayer;
            Players.OtherPlayersDetected += manager.UpdateOtherPlayers;
            Players.PlayerLeft += manager.RemovePlayer;
            Dungeons.DungeonDetected += manager.UpdateDungeonsState;
            Dungeons.DungeonLeft += manager.RemoveDungeon;
        }
    }
}
