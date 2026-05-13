using SharpPcap;

namespace Nightwatch.Entities;

/// <summary>
/// Represents a capture device with a user-friendly description or name.
/// </summary>
public class DisplayableCaptureDevice
{
    /// <summary>
    /// Gets the underlying capture device.
    /// </summary>
    public ICaptureDevice Device { get; }

    /// <summary>
    /// Gets a user-friendly description or name for the capture device.
    /// </summary>
    public string DescriptionOrName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayableCaptureDevice"/> class.
    /// </summary>
    /// <param name="device">The capture device to wrap.</param>
    /// <param name="index">The index of the device in the list.</param>
    public DisplayableCaptureDevice(ICaptureDevice device, int index)
    {
        // Assign the device to the property.
        Device = device;

        // Generate a user-friendly description or name for the device.
        DescriptionOrName = $"{index + 1}. " +
                            (string.IsNullOrEmpty(device.Description)
                                ? "(No description available)"
                                : device.Description);
    }
}


