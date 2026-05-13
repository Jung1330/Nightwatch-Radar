using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nightwatch.ViewModels
{
    /// <summary>  
    /// Base class for implementing the MVVM pattern.  
    /// Provides property change notification and helper methods for property binding.  
    /// </summary>  
    public class MVVMBase : INotifyPropertyChanged
    {
        /// <summary>  
        /// Event triggered when a property value changes.  
        /// </summary>  
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>  
        /// Notifies listeners that a property value has changed.  
        /// </summary>  
        /// <param name="propertyName">The name of the property that changed. Defaults to the caller's name.</param>  
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>  
        /// Sets the field to the specified value and raises the PropertyChanged event if the value changes.  
        /// </summary>  
        /// <typeparam name="T">The type of the field.</typeparam>  
        /// <param name="field">The reference to the field being updated.</param>  
        /// <param name="value">The new value to set.</param>  
        /// <param name="propertyName">The name of the property. Defaults to the caller's name.</param>  
        /// <returns>True if the field value was changed; otherwise, false.</returns>  
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            // Check if the new value is equal to the current value.  
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            // Update the field value.  
            field = value;

            // Notify listeners about the property change.  
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}


