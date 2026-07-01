using System;
using System.Text.RegularExpressions;
using AlbionDataHandlers.Entities;

namespace AlbionDataHandlers.Utils
{
    public static class DungeonDatabase
    {
        public static (int Tier, string Name) ParseDungeon(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return (0, "Unknown");

            int tier = 0;
            var tierMatch = Regex.Match(prefab, @"^T(\d)");
            if (tierMatch.Success)
            {
                tier = int.Parse(tierMatch.Groups[1].Value);
            }

            string name = "Dungeon";
            if (prefab.Contains("SOLO")) name = "Solo Dungeon";
            else if (prefab.Contains("GROUP")) name = "Group Dungeon";
            else if (prefab.Contains("CORRUPT")) name = "Corrupted Dungeon";
            else if (prefab.Contains("HELLGATE")) name = "Hellgate";
            else if (prefab.Contains("ELITE") || prefab.Contains("AVALON")) name = "Avalonian Dungeon";
            else if (prefab.Contains("MIST") || prefab.Contains("ABBEY")) name = "Mist Portal";

            if (prefab.Contains("BOSS")) name += " (Boss Lair)";

            return (tier, name);
        }
    }
}
