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

        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            int code = (int)eventCode;
            if (eventCode == EventCodes.NewRandomDungeonExit || eventCode == EventCodes.NewMistsDungeonExit || eventCode == EventCodes.RandomDungeonPositionInfo || code == 323 || code == 325 || code == 515 || code == 520 || code == 525 || code == 535)
            {
                HandleDungeon(parameters, eventCode);
            }
            else if (code == 521 || code == 522)
            {
                HandleExitPortal(parameters, code);
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
                long id = parameters.ContainsKey(0) ? Convert.ToInt64(parameters[0]) : 0;
                float[] pos = null;
                if (parameters.TryGetValue(1, out var posObj))
                {
                    if (posObj is float[] fArr) pos = fArr;
                    else if (posObj is IList<float> list) pos = new float[] { list[0], list[1] };
                    else if (posObj is System.Collections.IList list2) pos = new float[] { Convert.ToSingle(list2[0]), Convert.ToSingle(list2[1]) };
                }
                string rawType = parameters.ContainsKey(3) ? parameters[3].ToString().ToUpper() : "";
                string prefab = parameters.ContainsKey(5) ? parameters[5].ToString() : "";
                string tag15 = parameters.ContainsKey(15) ? parameters[15].ToString().ToUpper() : "";
                byte enchant = parameters.ContainsKey(8) ? Convert.ToByte(parameters[8]) : (byte)0;
                int groupSize = parameters.ContainsKey(17) ? Convert.ToInt32(parameters[17]) : 1;

                string prefabUpper = prefab.ToUpper();
                string type = "";

                if (rawType.Contains("MIST_DUNGEON_ENTRANCE") || rawType.Contains("ABBEY") || prefabUpper.Contains("ABBEY") || prefabUpper.Contains("MIST") || prefabUpper.Contains("WISP") || tag15.Contains("MIST") || tag15.Contains("ABBEY"))
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
                else if (rawType.Contains("SOLO") || prefabUpper.Contains("SOLO"))
                {
                    type = "1";
                }
                else if (rawType.Contains("GROUP") || prefabUpper.Contains("GROUP") || rawType.Contains("EXPEDITION") || (rawType.Contains("DUNGEON") && !rawType.Contains("MIST")))
                {
                    type = "2";
                }
                else
                {
                    // Ağaç, taş ve çevre objelerini kesinlikle zindan sanıp çizme!
                    return;
                }

                if (id != 0 && pos != null && pos.Length >= 2)
                {
                    var dbInfo = AlbionDataHandlers.Utils.DungeonDatabase.ParseDungeon(prefab);

                    var d = new Dungeon
                    {
                        Id = id,
                        PositionX = pos[0],
                        PositionY = pos[1],
                        Type = type,
                        Prefab = prefab,
                        Tier = dbInfo.Tier,
                        Name = dbInfo.Name,
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
                long id = parameters.ContainsKey(0) ? Convert.ToInt64(parameters[0]) : 0;
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
    }
}
