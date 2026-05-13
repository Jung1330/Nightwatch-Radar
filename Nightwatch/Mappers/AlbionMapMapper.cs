namespace Nightwatch.Mappers;

public static class AlbionMapMapper
{
    /// <summary>  
    /// Rotates a point (x, y) around a specified center point (centerX, centerY) by a given angle.  
    /// </summary>  
    /// <param name="x">The x-coordinate of the point to rotate.</param>  
    /// <param name="y">The y-coordinate of the point to rotate.</param>  
    /// <param name="centerX">The x-coordinate of the center point.</param>  
    /// <param name="centerY">The y-coordinate of the center point.</param>  
    /// <param name="angle">The rotation angle in degrees.</param>  
    /// <returns>A tuple containing the rotated x and y coordinates.</returns>  
    public static Tuple<float, float> RotateWithCenter(float x, float y, float centerX, float centerY, float angle)
    {
        // Convert the angle from degrees to radians.  
        float radians = angle * (MathF.PI / 180f);

        // Precompute cosine and sine of the angle for efficiency.  
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);

        // Translate the point to the origin relative to the center.  
        float translatedX = x - centerX;
        float translatedY = y - centerY;

        // Apply the rotation formula.  
        float rotatedX = translatedX * cos - translatedY * sin + centerX;
        float rotatedY = translatedX * sin + translatedY * cos + centerY;

        // Return the rotated coordinates as a tuple.  
        return new Tuple<float, float>(rotatedX, rotatedY);
    }
}


