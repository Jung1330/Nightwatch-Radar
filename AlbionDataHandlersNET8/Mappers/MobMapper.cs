using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using System.Text.Json.Nodes;

namespace AlbionDataHandlers.Mappers;

public class MobMapper
{
    public static MobMapper Instance { get; } = new MobMapper("Assets/Helper/mobs.min.json");
    private static Dictionary<int, MobTypeInfo> TypeMap;

    public MobMapper(string filePath)
    {
        TypeMap = new Dictionary<int, MobTypeInfo>();
        using (FileStream file = File.OpenRead(filePath))
        using (StreamReader reader = new StreamReader(file))
        {
            string json = reader.ReadToEnd();
            JsonNode? jsonNode = JsonNode.Parse(json);
            if (jsonNode is JsonArray mobArray)
            {
                for (int i = 0; i < mobArray.Count; i++)
                {
                    var item = mobArray[i];
                    if (item is JsonObject obj)
                    {
                        int typeId = i + 15;
                        var mobTypeInfo = new MobTypeInfo
                        {
                            TypeId = typeId,
                            Tier = obj.ContainsKey("t") ? (TierLevels)obj["t"]!.GetValue<int>() : TierLevels.Tier1,
                            Type = obj.ContainsKey("l") ? MobTypes.LivingHarvestable : MobTypes.LivingSkinnable,
                            Name = obj["n"]?.GetValue<string>() ?? obj["u"]?.GetValue<string>() ?? ""
                        };
                        TypeMap[typeId] = mobTypeInfo;
                    }
                }
            }
        }
    }

    public MobTypeInfo? GetMobInfo(int typeId)
    {
        return TypeMap.TryGetValue(typeId, out var mobInfo) ? mobInfo : null;
    }
}


