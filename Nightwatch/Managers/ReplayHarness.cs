using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Packets;
using Newtonsoft.Json.Linq;

namespace Nightwatch.Managers
{
    /// <summary>
    /// TEST HARNESS (oyunsuz test)
    /// ---------------------------------------------------------------
    /// Kaydedilmis paketleri (log\packets_*.jsonl) parser + handler'lara
    /// besler ve GameStateManager'in son halini raporlar.
    ///
    /// KULLANIM:
    ///     Nightwatch.exe --replay <dosya.jsonl>
    ///
    /// Kayit almak icin: uygulamada DevTools -> Packet Inspector -> "Diske kaydet"
    ///
    /// Boylece "oyunda su paket geldiginde uygulama ne yapiyor?" sorusunu
    /// oyuna girmeden tekrar tekrar test edebiliriz.
    /// </summary>
    public static class ReplayHarness
    {
        /// <summary>WinExe'nin konsolu yoktur; ciktiyi hem ekrana hem rapor dosyasina yazar.</summary>
        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _screen;
            private readonly TextWriter _file;
            public TeeWriter(TextWriter screen, TextWriter file) { _screen = screen; _file = file; }
            public override System.Text.Encoding Encoding => _screen.Encoding;
            public override void Write(char c) { try { _screen.Write(c); } catch { } _file.Write(c); }
            public override void Write(string s) { try { _screen.Write(s); } catch { } _file.Write(s); }
            public override void WriteLine(string s) { try { _screen.WriteLine(s); } catch { } _file.WriteLine(s); }
        }

        public static int Run(string file)
        {
            var screen = Console.Out;
            var buffer = new StringWriter();
            try { Console.SetOut(new TeeWriter(screen, buffer)); } catch { }

            int result = RunCore(file);

            try
            {
                Console.SetOut(screen);
                string reportPath = file + ".report.txt";
                File.WriteAllText(reportPath, buffer.ToString());
                Console.WriteLine();
                Console.WriteLine("Rapor yazildi: " + reportPath);
            }
            catch { }

            return result;
        }

        private static int RunCore(string file)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("NIGHTWATCH REPLAY (oyunsuz test)");
            Console.WriteLine("=====================================================");

            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                Console.WriteLine("[HATA] Dosya bulunamadi: " + file);
                Console.WriteLine("Kullanim: Nightwatch.exe --replay log\\packets_YYYYMMDD_HHMMSS.jsonl");
                return 2;
            }

            Console.WriteLine("Kaynak: " + file);
            Console.WriteLine();

            var manager = new GameStateManager();
            int mapChanges = 0;

            var pipeline = new PacketPipeline(
                manager,
                onMapChanged: id => { mapChanges++; Console.WriteLine("  [MAP] " + id); },
                onDungeonDebug: msg => Console.WriteLine("  " + msg));

            var byKind = new Dictionary<string, int>();
            var byCode = new Dictionary<int, int>();
            int lines = 0, parseErrors = 0, handled = 0;

            foreach (string raw in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                lines++;

                try
                {
                    JObject o = JObject.Parse(raw);
                    int code = (int)o["code"];
                    string kind = (string)o["kind"] ?? "EVENT";

                    var p = new Dictionary<byte, object>();
                    if (o["p"] is JObject po)
                    {
                        foreach (JProperty prop in po.Properties())
                        {
                            if (byte.TryParse(prop.Name, out byte key))
                                p[key] = PacketRecorder.Decode(prop.Value);
                        }
                    }

                    byKind[kind] = byKind.TryGetValue(kind, out int kc) ? kc + 1 : 1;
                    byCode[code] = byCode.TryGetValue(code, out int cc) ? cc + 1 : 1;

                    if (kind == "EVENT")
                    {
                        p[252] = (short)code;                      // parser kod alanini buradan okur
                        pipeline.Parser.InjectParsedEvent(1, p);
                    }
                    else if (kind == "OP-ISTEK")
                    {
                        p[253] = (short)code;
                        pipeline.Parser.InjectParsedRequest(0, p);
                    }
                    else
                    {
                        p[253] = (short)code;
                        pipeline.Parser.InjectParsedResponse(0, p);
                    }
                    handled++;
                }
                catch (Exception ex)
                {
                    parseErrors++;
                    if (parseErrors <= 5) Console.WriteLine("  [HATA] " + ex.Message);
                }
            }

            // ---- durum raporu ----
            // ONEMLI: Bazi handler'lar listeyi ZAMANLAYICI ile yayinlar
            // (orn. MobsHandler -> 25 ms'lik PublishTick). Paketler saniyeler icinde
            // islendigi icin son durumun olusmasi adina kisa bir bekleme sart;
            // yoksa "mob 0" gibi YANILTICI sonuclar cikar (uygulama hatasi degil!).
            try { System.Threading.Thread.Sleep(600); } catch { }
            try { manager.Update(0.016f); } catch { }

            var players = new List<Player>();
            var mobs = new List<Mob>();
            var harvestables = new List<Harvestable>();
            var dungeons = new List<Dungeon>();
            try { manager.GetOtherPlayers(players); } catch { }
            try { manager.GetMobs(mobs); } catch { }
            try { manager.GetHarvestables(harvestables); } catch { }
            try { manager.GetDungeons(dungeons); } catch { }
            Player me = null;
            try { me = manager.GetPlayer(); } catch { }

            Console.WriteLine();
            Console.WriteLine("==================== GIRDI OZETI ====================");
            Console.WriteLine("Satir          : " + lines);
            Console.WriteLine("Islenen paket  : " + handled);
            Console.WriteLine("Parse hatasi   : " + parseErrors);
            foreach (var kv in byKind) Console.WriteLine("Tur            : " + kv.Key + " = " + kv.Value);
            Console.WriteLine();
            Console.WriteLine("En cok gelen 12 kod:");
            foreach (var kv in byCode.OrderByDescending(x => x.Value).Take(12))
                Console.WriteLine(string.Format("   [{0,4}] {1,5} kez", kv.Key, kv.Value));

            Console.WriteLine();
            Console.WriteLine("==================== SONUC DURUMU ===================");
            Console.WriteLine("Aktif harita   : " + (string.IsNullOrEmpty(manager.CurrentMapId) ? "(yok)" : manager.CurrentMapId)
                              + "   (harita degisimi: " + mapChanges + ")");
            Console.WriteLine("Kendi oyuncu   : " + (me == null ? "(yok)" : string.Format("{0} @ ({1:0.#}, {2:0.#}) HP {3:0}/{4:0}",
                              me.Name ?? "?", me.PositionX, me.PositionY, me.CurrentHealth, me.MaxHealth)));

            Console.WriteLine("Diger oyuncu   : " + players.Count);
            foreach (Player pl in players.Take(8))
                Console.WriteLine(string.Format("   - {0,-22} [{1}] guild={2} alliance={3} pos=({4:0.#},{5:0.#})",
                    pl.Name ?? "?", pl.Id, pl.Guild ?? "-", pl.Alliance ?? "-", pl.PositionX, pl.PositionY));

            Console.WriteLine("Mob            : " + mobs.Count);
            Dictionary<int, string> mobNames = LoadMobNames();
            foreach (Mob m in mobs.Take(8))
                Console.WriteLine(string.Format("   - {0,-28} tip={1} T{2} rar={3} pos=({4:0.#},{5:0.#})",
                    MobLabel(m, mobNames), m.TypeId, m.NetworkTier, m.Rarity, m.PositionX, m.PositionY));

            // Sandiklar da mob listesine duser (TypeId=51900). Ilk 8'de gorunmeyebilirler;
            // bu yuzden nadirlik tasiyanlari AYRICA yazdiriyoruz (rarity testi icin kritik).
            var flagged = mobs.Where(m => m.Rarity > 0 || m.TypeId == 51900).Take(10).ToList();
            if (flagged.Count > 0)
            {
                Console.WriteLine("   -- SANDIK / ozel (rarity>0 veya tip=51900): " + flagged.Count);
                foreach (Mob m in flagged)
                    Console.WriteLine(string.Format("   * {0,-34} tip={1} rar={2} pos=({3:0.#},{4:0.#}) unlock={5}",
                        MobLabel(m, mobNames), m.TypeId, m.Rarity, m.PositionX, m.PositionY, m.UnlockTicks));
            }

            Console.WriteLine("Kaynak         : " + harvestables.Count);
            foreach (Harvestable h in harvestables.Take(8))
                Console.WriteLine(string.Format("   - tip={0} T{1} e{2} pos=({3:0.#},{4:0.#})",
                    h.Type, h.Tier, h.EnchantmentLevel, h.PositionX, h.PositionY));

            Console.WriteLine("Dungeon/Portal : " + dungeons.Count);
            foreach (Dungeon d in dungeons.Take(8))
                Console.WriteLine(string.Format("   - {0,-24} tip={1} T{2} pos=({3:0.#},{4:0.#})",
                    d.Name ?? "?", d.Type, d.Tier, d.PositionX, d.PositionY));

            Console.WriteLine("=====================================================");
            return parseErrors == 0 ? 0 : 3;
        }

        /// <summary>
        /// Mob adlarini oyunun kendi DB'sinden yukler.
        /// Esleme (uygulamayla AYNI): TypeId = index + 16, isim = "n" (varsa) yoksa "u".
        /// </summary>
        private static Dictionary<int, string> LoadMobNames()
        {
            var map = new Dictionary<int, string>();
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "mobs_EN_min.json");
                if (!File.Exists(path)) return map;

                JArray arr = JArray.Parse(File.ReadAllText(path));
                for (int i = 0; i < arr.Count; i++)
                {
                    if (!(arr[i] is JObject o)) continue;
                    string n = o["n"]?.ToString();
                    if (string.IsNullOrEmpty(n)) n = o["u"]?.ToString();
                    if (!string.IsNullOrEmpty(n)) map[i + 16] = n;
                }
            }
            catch { }
            return map;
        }

        /// <summary>Gosterilecek isim: DB'de varsa onu, yoksa paketteki ismi kullanir.</summary>
        private static string MobLabel(Mob m, Dictionary<int, string> names)
        {
            if (names != null && names.TryGetValue(m.TypeId, out string n) && !string.IsNullOrEmpty(n))
                return n;
            return string.IsNullOrEmpty(m.Name) ? "?" : m.Name;
        }
    }
}
