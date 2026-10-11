namespace AlbionDataHandlers.Enums
{
    // NOT: Oyunun gerçek enum'u "OperationCodes"tur ve istek/yanıt AYNI kodu paylaşır.
    // Bu enum yalnızca radarın İHTİYAÇ DUYDUĞU istekleri içerir.
    // İsimler oyunun gerçek isimleriyle eşitlendi (kaynak: Extra\dump.cs -> public enum OperationCodes).
    public enum RequestCodes : short
    {
        Unused = 0,

        Move = 22,               // oyun: Move
        AttackStart = 23,        // (eski adı: Attack)
        CastStart = 24,          // (eski adı: Cast)
        HarvestStart = 52,       // (eski adı: Harvest)
        SendChatMessage = 189,   // (eski adı: OpenChat)
        Mount = 199              // oyun: Mount
    }
}
