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
    public long UnlockTicks { get; set; }

    private string _cleanDisplayName = string.Empty;
    public string CleanDisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(_cleanDisplayName))
            {
                if (!string.IsNullOrEmpty(Name))
                {
                    _cleanDisplayName = Name.Replace("Mob ", "").Replace("Enemy ", "").Trim();
                    if (string.IsNullOrEmpty(_cleanDisplayName)) _cleanDisplayName = "Unknown";
                }
                else
                {
                    _cleanDisplayName = $"TypeId:{TypeId}";
                }
            }
            return _cleanDisplayName;
        }
        set => _cleanDisplayName = value;
    }
}