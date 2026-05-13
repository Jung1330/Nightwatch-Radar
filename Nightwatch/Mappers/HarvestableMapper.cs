using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;
using Nightwatch.Entities;

namespace Nightwatch.Mappers;

/// <summary>  
/// Provides mapping functionality to convert Harvestable objects into RadarEntity objects.  
/// </summary>  
public static class HarvestableMapper
{
    /// <summary>  
    /// Converts a Harvestable object to a RadarEntity object.  
    /// </summary>  
    /// <param name="harvestable">The Harvestable object to convert.</param>  
    /// <returns>A RadarEntity object or null if the input is null.</returns>  
    public static RadarEntity? ToRadarEntity(this Harvestable harvestable)
    {
        if (harvestable == null) return null;

        // Create and populate a RadarEntity object based on the Harvestable object.  
        var entity = new RadarEntity
        {
            Id = harvestable.Id,
            TypeId = harvestable.Size,
            Name = string.Empty, // Name is intentionally left empty.  
            PositionX = harvestable.CurrentLerpedX,
            PositionY = harvestable.CurrentLerpedY,
            ImageUrl = GetImageUrl(harvestable),
            EnchantmentLevel = harvestable.EnchantmentLevel,
            Type = EntityTypes.Harvestable,
        };

        return entity;
    }

    /// <summary>  
    /// Generates the image URL for a given Harvestable object based on its type, tier, and enchantment level.  
    /// </summary>  
    /// <param name="harvestable">The Harvestable object for which to generate the image URL.</param>  
    /// <returns>A string representing the image URL, or null if the type is invalid.</returns>  
    private static string? GetImageUrl(Harvestable harvestable)
    {
        // Albion'da kaynak tipleri her tier için ayrý bir ID alýr. 
        // Sonradan eklenen T7 ve T8'ler sona (28-37) eklenmiþtir.
        string? prefix = harvestable.Type switch
        {
            >= 0 and <= 5 or 28 or 29 => "Logs_",
            >= 6 and <= 11 or 30 or 31 => "rock_",
            >= 12 and <= 16 or 32 or 33 => "fiber_",
            >= 17 and <= 22 or 34 or 35 => "hide_",
            >= 23 and <= 27 or 36 or 37 => "ore_",
            _ => null
        };

        // If no valid prefix is found, return null.  
        if (prefix is null) return null;

        // Construct and return the image URL.  
        return $"Resources/{prefix}{harvestable.Tier}_{harvestable.EnchantmentLevel}.png";
    }
}


