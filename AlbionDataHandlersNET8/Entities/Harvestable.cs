namespace AlbionDataHandlers.Entities
{
    /// <summary>  
    /// Represents a harvestable resource node (e.g., ore, fiber, stone).  
    /// </summary>  
    public class Harvestable : InterpolatableEntity, IEqualityComparer<Harvestable>
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public int Tier { get; set; }
        public int EnchantmentLevel { get; set; }
        public int Size { get; set; }
        public int Count { get; set; }      // Þu an içinde kaç tane var (Örn: 2)
        public int Capacity { get; set; }   // Maksimum kapasite (Örn: 8)
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

        #region IEqualityComparer<Harvestable> Implementation  

        /// <summary>  
        /// Compares two Harvestable objects for equality based on their IDs.  
        /// </summary>  
        public bool Equals(Harvestable? x, Harvestable? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Id == y.Id;
        }

        /// <summary>  
        /// Generates a hash code for the Harvestable based on its ID.  
        /// </summary>  
        public int GetHashCode(Harvestable obj)
        {
            return obj.Id;
        }

        #endregion
    }
}


