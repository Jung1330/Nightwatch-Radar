#region Using Directives
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AlbionDataHandlers.Entities;
using AlbionDataHandlers.Handlers;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;
#endregion

namespace Nightwatch
{
    public partial class AlbionOverlay
    {
        #region Database Loaders
        private void LoadItemDatabaseTXT()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "items.txt");
            if (File.Exists(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        // Format: "ID: InternalName : DisplayName : IP"
                        // DisplayName içinde ':' olabilir ("Delivery: Blueprints" gibi)
                        // Bu yüzden sabit sütun sayýsýna güvenmek yerine son sütunu IP olarak okuyoruz.
                        var parts = line.Split(':');
                        // Dumper çýktýsý genellikle 4 parça olur (Örn: "1: NAME : Display : ")
                        if (parts.Length >= 3 && int.TryParse(parts[0].Trim(), out int id))
                        {
                            string internalName = parts[1].Trim();

                            // Son parça genelde IP veya boþluktur
                            string lastPart = parts[parts.Length - 1].Trim();
                            int exactIp = 0;

                            // Dumper'ýn attýðý boþluklarý ve gereksiz iki noktalarý yok say
                            if (!string.IsNullOrWhiteSpace(lastPart)) int.TryParse(lastPart, out exactIp);

                            // Display Name: 2. indeksten baþlayýp sondan bir öncekine kadar (IP olsun veya olmasýn dumper her zaman sona ':' koyar)
                            string displayName = "";
                            if (parts.Length >= 4)
                            {
                                displayName = string.Join(":", parts, 2, parts.Length - 3).Trim();
                            }
                            else
                            {
                                displayName = parts[2].Trim();
                            }

                            if (string.IsNullOrEmpty(displayName)) displayName = internalName;


                            displayName = displayName.Replace("Beginner's ", "").Replace("Novice's ", "").Replace("Journeyman's ", "")
                                .Replace("Adept's ", "").Replace("Expert's ", "").Replace("Master's ", "").Replace("Grandmaster's ", "").Replace("Elder's ", "");

                            int tier = 0, enchant = 0;
                            var tMatch = _tierRegex.Match(internalName);
                            if (tMatch.Success) int.TryParse(tMatch.Groups[1].Value, out tier);
                            if (internalName.Contains("@"))
                            {
                                var split = internalName.Split('@');
                                if (split.Length > 1) int.TryParse(split[1], out enchant);
                            }

                            // IP dosyada yoksa (0 geldi) tier+enchant formülüyle hesapla
                            if (exactIp == 0)
                            {
                                int baseIp = (tier < 4 ? tier * 100 : 700 + (tier - 4) * 100);
                                exactIp = baseIp + (enchant * 100);
                            }

                            _itemDatabase[id] = new ItemInfo { InternalName = internalName, DisplayName = displayName, Power = exactIp };
                        }

                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 63 | {ex.Message}");
                    Log($"[HATA] {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void CheckAndLoadDatabase()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possiblePaths = new string[]
            {
                System.IO.Path.Combine(baseDir, "Assets", "Helper", "mobs.min.json"),
                System.IO.Path.Combine(baseDir, "Assets", "mobs.min.json"),
                System.IO.Path.Combine(baseDir, "mobs.min.json")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Log(string.Format(Lang.Get("MobDbLoading"), path), LogLevel.Success);
                    LoadMobDatabase(path);
                    return;
                }
            }

            Log(Lang.Get("MobDbNotFound"), LogLevel.Error);
        }

        private void LoadMobDatabase(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                JArray mobArray = JArray.Parse(json);
                _mobDatabase.Clear();
                for (int i = 0; i < mobArray.Count; i++)
                {
                    JToken item = mobArray[i];
                    if (item == null) continue;
                    var info = new MobInfo();

                    int typeId = i + 15;

                    string rawName = "";
                    if (item.Type == JTokenType.Object)
                    {
                        var obj = (JObject)item;
                        rawName = obj["n"]?.ToString() ?? obj["u"]?.ToString() ?? "";
                        if (int.TryParse(obj["t"]?.ToString(), out int t)) info.Tier = t;
                        if (obj.ContainsKey("l")) { info.IsHarvestable = true; info.HarvestType = obj["l"]?.ToString(); }
                    }
                    else { rawName = item.ToString(); }

                    if (info.Tier == 0 && !string.IsNullOrEmpty(rawName)) info.Tier = ParseTier(rawName);

                    if (!string.IsNullOrEmpty(rawName))
                    {
                        info.Name = CleanName(rawName);
                        if (!_mobDatabase.ContainsKey(typeId))
                        {
                            _mobDatabase[typeId] = info;
                        }
                    }
                }

                BuildLivingResourceTypeMap();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error Code : 64 | {ex.Message}");
                Log(string.Format(Lang.Get("MobDbJsonError"), ex.Message), LogLevel.Error);
            }
        }

        private void BuildLivingResourceTypeMap()
        {
            _livingResourceTypeMap.Clear();

            foreach (var kv in _mobDatabase)
            {
                var info = kv.Value;
                if (info == null || !info.IsHarvestable) continue;

                var category = ParseCategoryFromString(info.HarvestType);
                if (category == HarvestableCategory.None)
                    category = ParseCategoryFromString(info.Name);

                if (category == HarvestableCategory.None) continue;

                int tier = info.Tier;
                if (tier <= 0 && !string.IsNullOrEmpty(info.Name))
                    tier = ParseTier(info.Name);

                _livingResourceTypeMap[kv.Key] = (category, tier);
            }
        }

        private void LoadZonesDatabase()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Helper", "zones.json");
            if (File.Exists(path))
            {
                try
                {
                    _mapSizes.Clear();
                    string json = File.ReadAllText(path);
                    // Dosya Array [...] deÄŸil Object {...} olduÄŸu iÃ§in JObject kullanÄ±yoruz
                    JObject zonesObj = JObject.Parse(json);

                    // JObject iÃ§indeki her bir Ã¶zelliÄŸi (Key-Value) dÃ¶nÃ¼yoruz
                    foreach (var property in zonesObj.Properties())
                    {
                        string id = property.Name; // Anahtar (Key) harita ID'sidir (Ã–rn: "1000")
                        if (string.IsNullOrEmpty(id)) continue;

                        JToken item = property.Value;
                        // JSON iÃ§indeki "type" ve "name" alanlarÄ±nÄ± kÃ¼Ã§Ã¼k harfle okumamÄ±z gerekiyor
                        string type = item["type"]?.ToString().ToUpperInvariant() ?? "";
                        string name = item["name"]?.ToString().ToUpperInvariant() ?? "";

                        float size = 825.0f; // VarsayÄ±lan AÃ§Ä±k DÃ¼nya boyutu

                        // Oyunun kendi veritabanÄ±ndaki (zones.json) Type/Name deÄŸerine gÃ¶re sÄ±nÄ±rlarÄ± belirliyoruz
                        if (type.Contains("CITY") || name.Contains("CITY")) size = 800.0f;
                        else if (type.Contains("PORTAL") || name.Contains("PORTAL")) size = 800.0f;
                        else if (type.Contains("ISLAND") || name.Contains("ISLAND")) size = 500.0f;
                        else if (type.Contains("HIDEOUT") || name.Contains("HIDEOUT")) size = 400.0f;
                        else if (type.Contains("DUNGEON") || name.Contains("DNG") || name.Contains("TUNNEL") || name.Contains("TNL") || name.Contains("PASSAGE") || name.Contains("PSG") || name.Contains("HALL")) size = 350.0f;

                        _mapSizes[id] = size;
                    }
                    Log(string.Format(Lang.Get("ZonesLoaded"), _mapSizes.Count), LogLevel.Success);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error Code : 65 | {ex.Message}");
                    Log(string.Format(Lang.Get("ZonesError"), ex.Message), LogLevel.Error);
                }
            }
        }
        #endregion

    }
}


