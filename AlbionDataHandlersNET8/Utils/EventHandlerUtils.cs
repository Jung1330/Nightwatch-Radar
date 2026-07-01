namespace AlbionDataHandlers.Utils;

public static class EventHandlerUtils
{
    public static T ExtractValue<T>(Dictionary<byte, object> parameters, byte key, T defaultValue = default)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                // Attempt to convert if types are compatible  
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (InvalidCastException ex)
            {
                System.Console.WriteLine($"Error Code : 46 | {ex.Message}");
                // Handle cases where conversion fails  
            }
        }
        return defaultValue;
    }
}


