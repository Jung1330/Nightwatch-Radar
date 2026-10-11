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
            catch (Exception ex)
            {
                // Catch FormatException, InvalidCastException, etc.
                // Log commented out to reduce spam, but it handles cases where conversion fails.
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// long alanlar icin guvenli okuyucu.
    /// NEDEN: Photon bazen 8 baytlik long yerine byte[8] gonderebiliyor
    /// (eski handler'lar bunu elle kontrol ediyordu). Bu metot ikisini de okur,
    /// boylece uretilen paket modelleri eski davranisi kaybetmez.
    /// </summary>
    public static long ExtractLong(Dictionary<byte, object> parameters, byte key, long defaultValue = 0)
    {
        if (parameters != null && parameters.TryGetValue(key, out var value) && value != null)
        {
            try
            {
                if (value is long l) return l;
                if (value is byte[] b && b.Length == 8) return BitConverter.ToInt64(b, 0);
                if (value is int i) return i;
                return Convert.ToInt64(value);
            }
            catch (Exception)
            {
                // donusmuyorsa varsayilana dus
            }
        }
        return defaultValue;
    }

    // ==================================================================
    // DIZI OKUYUCULAR (EK)
    // ------------------------------------------------------------------
    // NEDEN: Photon bir dizi alanini, dump'taki tipten FARKLI gonderebiliyor.
    // Canli kayit (kod 39 = NewSimpleHarvestableObjectList):
    //   [0] modelde long[]  -> telde short[] (133 kez) / byte[] (3 kez)
    //   [3] modelde int     -> telde float[] (konum!)
    //   [4] modelde short[] -> telde byte[]
    // ExtractValue<T> bu durumda donusum yapamayip varsayilana dusuyordu; bu
    // metotlar diziyi eleman eleman hedef tipe cevirir.
    //
    // KURALLAR:
    //  - Deger zaten hedef tipte ise AYNEN dondurulur (kopya uretilmez).
    //  - Anahtar yok / deger dizi degil / eleman cevrilemiyor ise null doner
    //    (uretilen modeller "alan var mi" kontrolunu p.ContainsKey ile yapar).
    //  - ExtractValue ve ExtractLong DAVRANISI DEGISTIRILMEDI (sadece ekleme).
    // ==================================================================

    public static long[] ExtractLongArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is long[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new long[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (!TryConvertInt64(items[i], out long v)) return null;
            result[i] = v;
        }
        return result;
    }

    public static int[] ExtractIntArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is int[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            long v;
            if (!TryConvertInt64(items[i], out v)) return null;
            if (v < int.MinValue || v > int.MaxValue) return null;
            result[i] = (int)v;
        }
        return result;
    }

    public static short[] ExtractShortArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is short[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new short[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            long v;
            if (!TryConvertInt64(items[i], out v)) return null;
            if (v < short.MinValue || v > short.MaxValue) return null;
            result[i] = (short)v;
        }
        return result;
    }

    public static byte[] ExtractByteArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is byte[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new byte[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            long v;
            if (!TryConvertInt64(items[i], out v)) return null;
            if (v < byte.MinValue || v > byte.MaxValue) return null;
            result[i] = (byte)v;
        }
        return result;
    }

    public static float[] ExtractFloatArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is float[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new float[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            object item = items[i];
            if (item == null) return null;
            try
            {
                result[i] = System.Convert.ToSingle(item, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
        return result;
    }

    public static string[] ExtractStringArray(Dictionary<byte, object> parameters, byte key)
    {
        object value = GetArrayValue(parameters, key);
        if (value == null) return null;
        if (value is string[] exact) return exact;

        System.Collections.IList items = AsList(value);
        if (items == null) return null;

        var result = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            object item = items[i];
            if (item == null) { result[i] = null; continue; }
            result[i] = item as string ?? System.Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture);
        }
        return result;
    }

    /// <summary>Anahtar varsa ham degeri dondurur (yok/bos ise null).</summary>
    private static object GetArrayValue(Dictionary<byte, object> parameters, byte key)
    {
        if (parameters == null) return null;
        if (!parameters.TryGetValue(key, out object value)) return null;
        return value;
    }

    /// <summary>Degeri indekslenebilir liste olarak dondurur (dizi veya IList); degilse null.</summary>
    private static System.Collections.IList AsList(object value)
    {
        if (value is System.Collections.IList list) return list;   // byte[]/short[]/float[]/List<object>...
        if (value is Array arr) return arr;                          // tek boyutlu dizi (IList'i acik uygular)
        return null;
    }

    /// <summary>Elemani long'a cevirir (kulture bagimsiz); cevrilemiyorsa false.</summary>
    private static bool TryConvertInt64(object item, out long value)
    {
        value = 0;
        if (item == null) return false;
        try
        {
            if (item is long l) { value = l; return true; }
            if (item is int i) { value = i; return true; }
            if (item is short s) { value = s; return true; }
            if (item is byte b) { value = b; return true; }
            if (item is sbyte sb) { value = sb; return true; }
            if (item is ushort us) { value = us; return true; }
            if (item is uint ui) { value = ui; return true; }
            value = System.Convert.ToInt64(item, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}


