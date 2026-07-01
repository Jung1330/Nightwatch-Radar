using System.Numerics;

namespace AlbionDataHandlers.Entities
{
    public class Dungeon : InterpolatableEntity
    {
        public long Id { get; set; }
        public string Type { get; set; }
        public string Prefab { get; set; }
        public int Tier { get; set; }
        public string Name { get; set; }
        public byte EnchantmentLevel { get; set; }
    }
}
