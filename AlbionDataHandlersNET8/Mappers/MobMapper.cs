using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;

using System.Text.Json.Nodes;

namespace AlbionDataHandlers.Mappers;

public class MobMapper
{
    private static MobMapper? _instance;
    public static MobMapper Instance => _instance ??= new MobMapper(GetDefaultLanguagePath());

    private static string GetDefaultLanguagePath()
    {
        return "Assets/Helper/mobs_EN_min.json";
    }

    // typeId → MobTypeInfo
    private Dictionary<int, MobTypeInfo> TypeMap { get; set; } = new Dictionary<int, MobTypeInfo>();

    // "@MOB_..." ham lok. key → MobTypeInfo (parser'dan gelen @MOB_ key ile arama için)
    private Dictionary<string, MobTypeInfo> LocKeyMap { get; set; } = new Dictionary<string, MobTypeInfo>(StringComparer.OrdinalIgnoreCase);

    // uniquename → MobTypeInfo (opsiyonel yedek arama)
    private Dictionary<string, MobTypeInfo> UniqueNameMap { get; set; } = new Dictionary<string, MobTypeInfo>(StringComparer.OrdinalIgnoreCase);

    public MobMapper(string filePath)
    {
        Reload(filePath);
    }

    public void Reload(string filePath)
    {
        string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"[MobMapper] HATA: Dosya bulunamadı: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
            JsonNode? jsonNode = JsonNode.Parse(json);

            if (jsonNode is JsonArray mobArray)
            {
                var newTypeMap      = new Dictionary<int, MobTypeInfo>();
                var newLocKeyMap    = new Dictionary<string, MobTypeInfo>(StringComparer.OrdinalIgnoreCase);
                var newUniqueNameMap = new Dictionary<string, MobTypeInfo>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < mobArray.Count; i++)
                {
                    var item = mobArray[i];
                    if (item is not JsonObject obj) continue;

                    int typeId  = obj.ContainsKey("id") ? obj["id"]!.GetValue<int>() : (i + 16);
                    int mobTier = obj.ContainsKey("t")  ? obj["t"]!.GetValue<int>()  : 1;

                    if (!Enum.IsDefined(typeof(TierLevels), mobTier))
                        mobTier = 1;

                    string locKey     = obj["k"]?.GetValue<string>() ?? "";
                    string uniqueName = obj["u"]?.GetValue<string>() ?? "";
                    string name       = obj["n"]?.GetValue<string>() ?? "";

                    var mobTypeInfo = new MobTypeInfo
                    {
                        TypeId     = typeId,
                        Name       = name,
                        UniqueName = uniqueName,
                        LocKey     = locKey,
                        Tier       = (TierLevels)mobTier,
                        LootTier   = obj.ContainsKey("lt") ? obj["lt"]!.GetValue<int>() : 0
                    };

                    newTypeMap[typeId] = mobTypeInfo;

                    // LocKey haritası: "@MOB_..." ile ara
                    if (!string.IsNullOrEmpty(locKey))
                        newLocKeyMap[locKey] = mobTypeInfo;

                    // UniqueName haritası: "T5_MOB_..." ile ara (@ olmadan da çalışsın)
                    if (!string.IsNullOrEmpty(uniqueName))
                        newUniqueNameMap[uniqueName] = mobTypeInfo;
                }

                lock (TypeMap)
                {
                    TypeMap      = newTypeMap;
                    LocKeyMap    = newLocKeyMap;
                    UniqueNameMap = newUniqueNameMap;
                }

                Console.WriteLine($"[MobMapper] Yüklendi: {filePath} ({mobArray.Count} mob, {newLocKeyMap.Count} locKey)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MobMapper] HATA: {ex.Message}");
        }
    }

    /// <summary>TypeId ile arama (ana yöntem)</summary>
    public MobTypeInfo? GetMobInfo(int typeId)
    {
        return TypeMap.TryGetValue(typeId, out var info) ? info : null;
    }

    /// <summary>Ham lokalizasyon key ile arama: "@MOB_T5_MOB_..." veya "MOB_T5_MOB_..." her ikisi de çalışır</summary>
    public MobTypeInfo? GetMobInfoByLocKey(string locKey)
    {
        if (string.IsNullOrEmpty(locKey)) return null;

        // Direkt arama
        if (LocKeyMap.TryGetValue(locKey, out var info)) return info;

        // @ eklenerek dene
        if (!locKey.StartsWith("@") && LocKeyMap.TryGetValue("@" + locKey, out info)) return info;

        // @ çıkarılarak dene
        if (locKey.StartsWith("@") && LocKeyMap.TryGetValue(locKey.Substring(1), out info)) return info;

        // UniqueName üzerinden son çare
        // @MOB_T5_MOB_... → T5_MOB_... (başındaki @MOB_ kısmını at)
        string stripped = locKey.TrimStart('@');
        if (stripped.StartsWith("MOB_", StringComparison.OrdinalIgnoreCase))
            stripped = stripped.Substring(4); // "MOB_" at
        if (UniqueNameMap.TryGetValue(stripped, out info)) return info;

        return null;
    }

    /// <summary>UniqueName ile arama: "T5_MOB_ROAMING_..." </summary>
    public MobTypeInfo? GetMobInfoByUniqueName(string uniqueName)
    {
        if (string.IsNullOrEmpty(uniqueName)) return null;
        return UniqueNameMap.TryGetValue(uniqueName, out var info) ? info : null;
    }
}