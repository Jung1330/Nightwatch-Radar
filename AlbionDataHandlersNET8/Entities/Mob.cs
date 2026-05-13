using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Mappers;

namespace AlbionDataHandlers.Entities
{
    /// <summary>  
    /// Represents a Mob entity. Inherits interpolation logic from InterpolatableEntity  
    /// and implements equality checks based on its unique ID.  
    /// </summary>  
    public class Mob : InterpolatableEntity, IEqualityComparer<Mob>
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public float Experience { get; set; }
        public int EnchantmentLevel { get; set; }
        public int NetworkTier { get; set; }
        public int Rarity { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get
            {
                // Retrieve the name from the mapper if not set directly.  
                return string.IsNullOrEmpty(_name)
                    ? MobMapper.Instance.GetMobInfo(TypeId)?.Name ?? string.Empty
                    : _name;
            }
            set
            {
                _name = value ?? string.Empty;
            }
        }

        /// <summary>  
        /// Carries position data from the network parser to the GameStateManager.  
        /// Not used for rendering.  
        /// </summary>  
        public float PositionX { get; set; }

        /// <summary>  
        /// Carries position data from the network parser to the GameStateManager.  
        /// Not used for rendering.  
        /// </summary>  
        public float PositionY { get; set; }

        #region Mapped Properties  
        public MobTypes Type => MobMapper.Instance.GetMobInfo(TypeId)?.Type ?? MobTypes.Enemy;
        public TierLevels Tier => MobMapper.Instance.GetMobInfo(TypeId)?.Tier ?? TierLevels.Tier1;
        #endregion

        #region IEqualityComparer<Mob> Implementation  

        /// <summary>  
        /// Determines equality based on the unique ID.  
        /// </summary>  
        public bool Equals(Mob? x, Mob? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Id == y.Id;
        }

        /// <summary>  
        /// Generates a hash code based on the unique ID.  
        /// </summary>  
        public int GetHashCode(Mob obj)
        {
            return obj.Id;
        }

        #endregion
    }
}


