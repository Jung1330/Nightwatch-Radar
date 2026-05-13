namespace AlbionDataHandlers.Enums
{
    public enum RequestCodes : short
    {
        Unused = 0,
        Attack = 1,
        Cast = 15,

        // Eksik olan kod buydu
        Move = 21,
        MoveAlt = 22,

        // Ýhtiyaç duyulabilecek diðer yaygýn kodlar
        Mount = 10,
        Harvest = 5,
        OpenChat = 30
    }
}


