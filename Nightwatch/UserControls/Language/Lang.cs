using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nightwatch.UserControls.Language
{
    public static class Lang
    {
        private static Dictionary<string, string> _texts = new Dictionary<string, string>();
        public static string CurrentLang { get; private set; } = "TR";
        public static string CurrentLanguage { get; private set; } = "TR";

        public static void LoadLanguage(string langCode)
        {
            CurrentLang = langCode;
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Language");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{langCode}.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    _texts = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

                    /*     Console.ForegroundColor = ConsoleColor.DarkGray;
                         Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                         Console.ForegroundColor = ConsoleColor.Green;
                         Console.WriteLine($"[+] Dil Paketi Yuklendi: {langCode}");
                         Console.ResetColor();*/
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 66 | {ex.Message}");
                    Console.WriteLine($"[!] Dil dosyasi okunamadi: {ex.Message}");
                }
            }
            else
            {
                // Dosya yoksa Ýngilizce (EN) veya Türkçe (TR) için örnek bir dosya oluþtur (Geliþtirici dostu!)
                /*CreateTemplate(path);*/
            }
        }

        // KODUN ÝÇÝNDE METÝNLERÝ ÇAÐIRACAÐIMIZ SÝHÝRLÝ FONKSÝYON
        public static string Get(string key)
        {
            if (_texts.TryGetValue(key, out string value)) return value;
            return key; // Eðer json içinde çeviriyi bulamazsa, key'in kendisini yazsýn ki eksiði görelim
        }

        /* private static void CreateTemplate(string path)
         {
             var template = new Dictionary<string, string>
             {
                 { "Console_AdminError", "[!] Yönetici olarak çalýþtýrýn." },
                 { "Menu_Settings", "Ayarlar" },
                 { "Menu_Resources", "Kaynaklar" },
                 { "Menu_Language", "Dil / Language" }
             };
             var options = new JsonSerializerOptions { WriteIndented = true };
             File.WriteAllText(path, JsonSerializer.Serialize(template, options));
         }*/
    }
}


