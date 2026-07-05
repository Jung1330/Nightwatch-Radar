using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Enums;

namespace Nightwatch.Tests
{
    public class NightwatchTests
    {
        // --------------------------------------------------------------------------------
        // TEST 1: DÜZ SANDIKLARIN (LOOT CHESTS) DOĞRU PARSE EDİLMESİ VE NADİRLİK DERECELERİ
        // --------------------------------------------------------------------------------
        // Bu test, oyundan gelen 391 (NewLootChest) paketlerinin koordinat, isim ve
        // nadirlik seviyelerinin (Rarity) doğru bir şekilde çözümlenip çözümlenmediğini doğrular.
        [Fact]
        public void Test_LootChestParsing_And_RarityDetermination()
        {
            // MobsHandler sınıfını sıfırdan oluşturuyoruz
            var mobsHandler = new MobsHandler();

            // UI'a gidecek listenin durumunu takip etmek için Mobs observer'ına abone oluyoruz
            IEnumerable<Mob> currentMobs = null;
            mobsHandler.Mobs.Subscribe(mobs => currentMobs = mobs);

            // SENARYO A: Yeşil (Common/Standard) Sandık Paketi Taklidi
            var greenChestParams = new Dictionary<byte, object>
            {
                { 0, 10001 }, // ID
                { 1, new float[] { 50.5f, -80.2f } }, // Konum [X, Y]
                { 3, "TREASURE_STANDARD_01" } // İsim
            };

            // SENARYO B: Efsanevi (Legendary) Sandık Paketi Taklidi
            var legendaryChestParams = new Dictionary<byte, object>
            {
                { 0, 10002 }, // ID
                { 1, new float[] { -150.0f, 300.0f } }, // Konum [X, Y]
                { 3, "TREASURE_LEGENDARY_04" } // İsim
            };

            // Paketleri handler'a gönderiyoruz (Event 391)
            mobsHandler.OnEvent((EventCodes)391, greenChestParams);
            mobsHandler.OnEvent((EventCodes)391, legendaryChestParams);

            // Kontroller (Doğrulamalar)
            Assert.NotNull(currentMobs);
            var mobsList = currentMobs.ToList();
            Assert.Equal(2, mobsList.Count);

            // Yeşil sandık doğrulaması
            var greenChest = mobsList.FirstOrDefault(m => m.Id == 10001);
            Assert.NotNull(greenChest);
            Assert.Equal(51900, greenChest.TypeId); // Custom Loot Chest TypeId'si
            Assert.Equal("TREASURE_STANDARD_01", greenChest.Name);
            Assert.Equal(1, greenChest.Rarity); // standard -> Rarity 1 (Yeşil) olmalı
            Assert.Equal(50.5f, greenChest.PositionX);

            // Efsanevi sandık doğrulaması
            var legendaryChest = mobsList.FirstOrDefault(m => m.Id == 10002);
            Assert.NotNull(legendaryChest);
            Assert.Equal(4, legendaryChest.Rarity); // legendary -> Rarity 4 (Efsanevi/Sarı) olmalı
            Assert.Equal(-150.0f, legendaryChest.PositionX);
        }

        // --------------------------------------------------------------------------------
        // TEST 2: MOB SPAWN VE HAREKET (MOVE) PAKETLERİNİN TEST EDİLMESİ
        // --------------------------------------------------------------------------------
        // Bu test, haritada yeni bir canavar spawn olduğunda (NewMob - 27) ve bu canavar
        // haritada hareket ettiğinde (Move - 23) koordinatlarının doğru güncellendiğini doğrular.
        [Fact]
        public void Test_MobSpawning_And_Movement()
        {
            var mobsHandler = new MobsHandler();
            IEnumerable<Mob> currentMobs = null;
            mobsHandler.Mobs.Subscribe(mobs => currentMobs = mobs);

            // 1. Yeni canavar spawn paketi hazırlıyoruz (NewMob)
            var spawnParams = new Dictionary<byte, object>
            {
                { 0, 20001 }, // Mob ID
                { 1, 150 }, // TypeID (Canavar türü)
                { 7, new float[] { 10.0f, 20.0f } }, // Başlangıç konumu [X, Y]
                { 32, "HERETIC_ARCHER" } // Canavar adı
            };

            mobsHandler.OnEvent(EventCodes.NewMob, spawnParams);

            // Canavarın eklendiğini kontrol ediyoruz
            Assert.NotNull(currentMobs);
            var mob = currentMobs.FirstOrDefault(m => m.Id == 20001);
            Assert.NotNull(mob);
            Assert.Equal(10.0f, mob.PositionX);
            Assert.Equal(20.0f, mob.PositionY);

            // 2. Canavarın hareket paketi (Move)
            var moveParams = new Dictionary<byte, object>
            {
                { 0, 20001 }, // Mob ID
                { 4, 15.5f }, // Yeni X
                { 5, 25.5f }  // Yeni Y
            };

            mobsHandler.OnEvent(EventCodes.Move, moveParams);

            // Canavar koordinatlarının güncellendiğini doğruluyoruz
            Assert.Equal(15.5f, mob.PositionX);
            Assert.Equal(25.5f, mob.PositionY);
        }

        // --------------------------------------------------------------------------------
        // TEST 3: MOB LEAVE (AYRILMA/SİLİNME) PAKETİNİN TEST EDİLMESİ
        // --------------------------------------------------------------------------------
        // Canavar öldüğünde veya oyuncunun görüş alanından çıktığında gelen Leave (1)
        // paketinin, canavarı aktif listeden düzgünce temizlediğini doğrular.
        [Fact]
        public void Test_MobLeave_RemovesFromList()
        {
            var mobsHandler = new MobsHandler();
            IEnumerable<Mob> currentMobs = null;
            mobsHandler.Mobs.Subscribe(mobs => currentMobs = mobs);

            // İlk canavarı spawn ediyoruz
            var spawnParams = new Dictionary<byte, object>
            {
                { 0, 30001 },
                { 1, 200 },
                { 7, new float[] { 0f, 0f } }
            };
            mobsHandler.OnEvent(EventCodes.NewMob, spawnParams);
            Assert.Single(currentMobs);

            // Canavarın görüş alanından çıkış (Leave) paketini tetikliyoruz
            var leaveParams = new Dictionary<byte, object>
            {
                { 0, 30001 } // Silinecek nesne ID'si
            };
            mobsHandler.OnEvent(EventCodes.Leave, leaveParams);

            // Listenin tamamen temizlendiğini doğruluyoruz
            Assert.Empty(currentMobs);
        }

        // --------------------------------------------------------------------------------
        // TEST 4: TÜM DİL DOSYALARINDAKİ (JSON) ANAHTARLARIN TUTARLILIK KONTROLÜ
        // --------------------------------------------------------------------------------
        // Bu test, TR.json, EN.json, RU.json ve ZH.json dosyalarını okur, geçerli bir JSON
        // formatında olduklarını onaylar ve tüm dosyalardaki çeviri anahtarlarının (Keys)
        // birbirleriyle tam uyumlu olduğunu (eksik anahtar olmadığını) garanti eder.
        [Fact]
        public void Test_LanguageFiles_KeysConsistency()
        {
            // Projedeki dil klasörü yolunu belirliyoruz
            // (Test projesinin çalıştığı yer debug klasörü olduğu için yukarı doğru çıkarak ana klasöre ulaşıyoruz)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string langDir = Path.Combine(solutionDir, "Nightwatch", "Assets", "Language");

            string[] langFiles = { "TR.json", "EN.json", "RU.json", "ZH.json" };
            var allKeys = new Dictionary<string, HashSet<string>>();

            // 1. Her bir dosyayı okuyup içindeki anahtarları kümelere aktarıyoruz
            foreach (var langFile in langFiles)
            {
                string filePath = Path.Combine(langDir, langFile);
                Assert.True(File.Exists(filePath), $"Dil dosyası bulunamadı: {langFile}");

                string jsonContent = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                
                Assert.NotNull(dict);
                allKeys[langFile] = new HashSet<string>(dict.Keys);
            }

            // 2. TR.json dosyasını referans alarak diğer tüm dosyalarla karşılaştırıyoruz
            var referenceFile = "TR.json";
            var referenceKeys = allKeys[referenceFile];

            foreach (var langFile in langFiles)
            {
                if (langFile == referenceFile) continue;

                var currentKeys = allKeys[langFile];

                // Referans dosyada olup hedef dosyada olmayan eksik anahtarlar
                var missingInTarget = referenceKeys.Except(currentKeys).ToList();
                Assert.Empty(missingInTarget);

                // Hedef dosyada olup referans dosyada olmayan fazla anahtarlar
                var extraInTarget = currentKeys.Except(referenceKeys).ToList();
                Assert.Empty(extraInTarget);
            }
        }
    }
}
