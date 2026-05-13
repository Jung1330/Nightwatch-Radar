using AlbionDataHandlers.Enums;
using Nightwatch.ViewModels;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Nightwatch.Entities
{
    /// <summary>  
    /// Represents an entity on the radar. Implements INotifyPropertyChanged for data binding  
    /// and IEquatable for comparison based on its unique ID.  
    /// </summary>  
    public class RadarEntity : MVVMBase, IEquatable<RadarEntity>
    {
        // Backing fields  
        private int _id;
        private string _name;
        private float _positionX;
        private float _positionY;
        private int _typeId;
        private string? _imageUrl;
        private int _enchantmentLevel;
        private EntityTypes _type;

        public int Id { get => _id; set => SetField(ref _id, value); }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public float PositionX { get => _positionX; set => SetField(ref _positionX, value); }
        public float PositionY { get => _positionY; set => SetField(ref _positionY, value); }
        public int TypeId { get => _typeId; set => SetField(ref _typeId, value); }
        public string? ImageUrl { get => _imageUrl; set => SetField(ref _imageUrl, value); }
        public int EnchantmentLevel { get => _enchantmentLevel; set => SetField(ref _enchantmentLevel, value); }
        public EntityTypes Type { get => _type; set => SetField(ref _type, value); }

        #region Equality Members  

        /// <summary>  
        /// Compares two RadarEntity objects for equality based on their unique ID.  
        /// </summary>  
        public bool Equals(RadarEntity? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.Id == other.Id;
        }

        /// <summary>  
        /// Overrides the base Equals method to use the type-safe Equals implementation.  
        /// </summary>  
        public override bool Equals(object? obj)
        {
            return Equals(obj as RadarEntity);
        }

        /// <summary>  
        /// Overrides GetHashCode to ensure consistency with the Equals method.  
        /// </summary>  
        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(RadarEntity? left, RadarEntity? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RadarEntity? left, RadarEntity? right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}


