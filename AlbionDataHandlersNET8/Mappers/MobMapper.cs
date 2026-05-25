using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using System.Text.Json.Nodes;

namespace AlbionDataHandlers.Mappers;

public class MobMapper
{
    private static MobMapper? _instance;
    public static MobMapper Instance => _instance ??= new MobMapper("Assets/Helper/mobs.min.json");

    private Dictionary<int, MobTypeInfo> TypeMap { get; set; } = new Dictionary<int, MobTypeInfo>();

    public MobMapper(string filePath)
    {
        Reload(filePath);
    }

    public void Reload(string filePath)
    {
        // Dosya yolunu mutlak yol yap, hata riskini sıfırla
        string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"[MobMapper] HATA: Dosya bulunamadı: {fullPath}");
            return;
        }

        try
        {
            // Dosyayı UTF8 ile oku
            string json = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
            JsonNode? jsonNode = JsonNode.Parse(json);

            if (jsonNode is JsonArray mobArray)
            {
                lock (TypeMap) // Kilitlemeyi TypeMap üzerinden yap
                {
                    TypeMap.Clear(); // ÖNEMLİ: Eski veriyi tamamen sil!
                    for (int i = 0; i < mobArray.Count; i++)
                    {
                        var item = mobArray[i];
                        if (item is JsonObject obj)
                        {
                            int typeId = obj.ContainsKey("id") ? obj["id"]!.GetValue<int>() : (i + 15);
                            int mobTier = obj.ContainsKey("t") ? obj["t"]!.GetValue<int>() : 1;

                            var mobTypeInfo = new MobTypeInfo
                            {
                                TypeId = typeId,
                                Name = obj["n"]?.GetValue<string>() ?? "",
                                UniqueName = obj["u"]?.GetValue<string>() ?? ""
                            };
                            TypeMap[typeId] = mobTypeInfo;
                        }
                    }
                }
                Console.WriteLine($"[MobMapper] Başarılı: {filePath} yüklendi.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MobMapper] HATA: {ex.Message}");
        }
    }

    public MobTypeInfo? GetMobInfo(int typeId)
    {
        return TypeMap.TryGetValue(typeId, out var mobInfo) ? mobInfo : null;
    }
}