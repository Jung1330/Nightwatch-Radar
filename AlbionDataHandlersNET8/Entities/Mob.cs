using AlbionDataHandlers.Enums;
using System.Text.Json.Nodes;
using System.IO;
using System;
using System.Collections.Generic;

namespace AlbionDataHandlers.Entities;

public class Mob : InterpolatableEntity
{
    public int TypeId { get; set; }
    public float Experience { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EnchantmentLevel { get; set; }
    public int NetworkTier { get; set; }
    public int Rarity { get; set; }

    // Properties for Nightwatch compatibility
    public TierLevels Tier { get; set; }
    public MobTypes Type { get; set; }
}