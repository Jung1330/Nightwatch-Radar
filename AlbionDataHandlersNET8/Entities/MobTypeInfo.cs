using AlbionDataHandlers.Enums;

namespace AlbionDataHandlers.Entities;

public class MobTypeInfo
{
    public int TypeId { get; set; }
    public MobTypes Type { get; set; }
    public TierLevels Tier { get; set; }
    public string Name { get; set; } = string.Empty;
}


