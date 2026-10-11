using System;

namespace AlbionDataHandlers.Utils
{
    /// <summary>
    /// Dosyaya yazan TUM tani loglarini tek anahtardan kontrol eder.
    /// VARSAYILAN: KAPALI -> uygulama hicbir log dosyasi olusturmaz.
    /// DevTools sekmesindeki "AllLogInfo" kutusu acilinca true olur.
    /// </summary>
    public static class PacketLogSettings
    {
        /// <summary>true ise tanı logları diske yazılır.</summary>
        public static bool AllLogInfo { get; set; } = false;
    }
}
