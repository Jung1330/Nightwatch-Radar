namespace AlbionDataHandlers.Enums;

public enum ResponseCodes
{
    // Oyunun OperationCodes.Join = 2 (küme girişi) — doğru.
    PlayerJoiningMap = 2,

    // DÜZELTME: Oyunun gerçek enum'unda 35 = InventoryStack, ChangeCluster = 41.
    // Eskiden 35 yazılıydı; yani envanterde eşya birleştirme işlemi "küme değişti" sanılıyordu.
    // Kaynak: Extra\dump.cs -> public enum OperationCodes (ChangeCluster = 41, InventoryStack = 35)
    // Canlı doğrulama: küme değiştirince 41, envanterde birleştirince 35 gelmeli.
    PlayerChangeCluster = 41,

    // DÜZELTME: oyunun enum'unda GetCharacterStats = 145 (137 eski/yanlıştı).
    // Kaynak: Extra\dump.cs -> public enum OperationCodes
    GetCharacterStats = 145
}


