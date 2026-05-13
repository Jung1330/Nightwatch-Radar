using AlbionDataHandlers.Entities;
using Nightwatch.Entities;

namespace Nightwatch.Mappers;

/// <summary>  
/// Provides mapping functionality to convert Mob objects into RadarEntity objects.  
/// </summary>  
public static class MobsMapper
{
    /// <summary>  
    /// Converts a Mob object to a RadarEntity object.  
    /// </summary>  
    /// <param name="mob">The Mob object to convert.</param>  
    /// <returns>A RadarEntity object or null if the input is null.</returns>  
    public static RadarEntity? ToRadarEntity(this Mob mob)
    {
        if (mob == null) return null;

        // Create and populate a RadarEntity object based on the Mob properties.  
        var entity = new RadarEntity
        {
            Id = mob.Id,
            TypeId = mob.TypeId,
            Name = mob.Name,
            PositionX = mob.CurrentLerpedX,
            PositionY = mob.CurrentLerpedY,
            ImageUrl = GetImageUrl(mob),
            EnchantmentLevel = mob.EnchantmentLevel,
            Type = AlbionDataHandlers.Enums.EntityTypes.Mob,
        };

        return entity;
    }

    /// <summary>  
    /// Generates the image URL for a Mob based on its type, tier, and enchantment level.  
    /// </summary>  
    /// <param name="mob">The Mob object for which to generate the image URL.</param>  
    /// <returns>A string representing the image URL, or null if the Mob type is not skinnable or harvestable.</returns>  
    private static string? GetImageUrl(Mob mob)
    {
        if (mob.Name != null && mob.Name.Contains("MIST")) return null;
        // Check if the Mob is of a type that has an associated image.  
        if (mob.Type == AlbionDataHandlers.Enums.MobTypes.LivingSkinnable ||
            mob.Type == AlbionDataHandlers.Enums.MobTypes.LivingHarvestable)
        {
            // Construct the image URL using the Mob's name, tier, and enchantment level.  
            return $"Resources/{mob.Name}_{(int)mob.Tier}_{mob.EnchantmentLevel}.png";
        }

        // Return null if no image URL is applicable.  
        return null;
    }
}


