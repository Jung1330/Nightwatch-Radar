using Nightwatch.ViewModels;

namespace Nightwatch.Entities;

/// <summary>  
/// Represents a player entity with position properties.  
/// Inherits from MVVMBase to support property change notifications.  
/// </summary>  
public class PlayerEntity : MVVMBase
{
    private float _positionX;
    private float _positionY;

    /// <summary>  
    /// Gets or sets the X-coordinate of the player's position.  
    /// Notifies listeners when the value changes.  
    /// </summary>  
    public float PositionX
    {
        get => _positionX;
        set => SetField(ref _positionX, value);
    }

    /// <summary>  
    /// Gets or sets the Y-coordinate of the player's position.  
    /// Notifies listeners when the value changes.  
    /// </summary>  
    public float PositionY
    {
        get => _positionY;
        set => SetField(ref _positionY, value);
    }
}


