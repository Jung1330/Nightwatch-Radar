using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public class EquipmentData
    {
        public string Weapon { get; set; }
        public string Head { get; set; }
        public string Armor { get; set; }
        public string Shoes { get; set; }
        public string Cape { get; set; }
    }
    public class PlayerData
    {
        public string Time { get; set; }
        public string Map { get; set; }
        public string Name { get; set; }
        public int IP { get; set; }
        public EquipmentData Equipment { get; set; }
    }
    public class ResourceData
    {
        public string Time { get; set; }
        public string Map { get; set; }
        public string Type { get; set; }
        public string Tier { get; set; }
        public string Amount { get; set; }
        public string Location { get; set; }
    }
    public class LogRoot
    {
        public List<PlayerData> Players { get; set; }
        public List<ResourceData> Resources { get; set; }
    }

    public static class RadarLogger
    {
        private static LogRoot _currentLog = new LogRoot() { Players = new List<PlayerData>(), Resources = new List<ResourceData>() };
        private static HashSet<string> _loggedKeys = new HashSet<string>();
        private static string _logFilePath;
        private static readonly object _lock = new object();
        private static DateTime _lastSaveTime = DateTime.Now;

        static RadarLogger()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Log");

            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                UIConsole.Log($"Error Code : 67 | {ex.Message}", LogLevel.Error);
                UIConsole.Log(string.Format(Lang.Get("Log_DirError"), ex.Message), LogLevel.Error);
            }

            string fileName = $"{DateTime.Now:dd-MM-yyyy_HH-mm}-Log.json";
            _logFilePath = Path.Combine(dir, fileName);

            UIConsole.Log(string.Format(Lang.Get("Log_PathInfo"), _logFilePath), LogLevel.Warning);

            AppDomain.CurrentDomain.ProcessExit += (s, e) => SaveFile();
            Console.CancelKeyPress += (s, e) => SaveFile();
        }

        public static void LogPlayer(string mapId, string name, int ip, string weapon, string head, string armor, string shoes, string cape)
        {
            string key = $"P_{name}_{mapId}";
            lock (_lock)
            {
                if (_loggedKeys.Contains(key)) return;
                _loggedKeys.Add(key);
                _currentLog.Players.Add(new PlayerData
                {
                    Time = DateTime.Now.ToString("HH:mm"),
                    Map = mapId,
                    Name = name,
                    IP = ip,
                    Equipment = new EquipmentData { Weapon = weapon, Head = head, Armor = armor, Shoes = shoes, Cape = cape }
                });

                UIConsole.Log(string.Format(Lang.Get("Log_PlayerSaved"), name), LogLevel.Info);

                CheckAndAutoSave();
            }
        }

        public static void LogResource(string mapId, string type, string tier, string amount, float posX, float posY)
        {
            string location = $"X: {posX:F0}, Y: {posY:F0}";
            string key = $"R_{location}_{type}_{tier}";
            lock (_lock)
            {
                if (_loggedKeys.Contains(key)) return;
                _loggedKeys.Add(key);
                _currentLog.Resources.Add(new ResourceData
                {
                    Time = DateTime.Now.ToString("HH:mm"),
                    Map = mapId,
                    Type = type,
                    Tier = tier,
                    Amount = amount,
                    Location = location
                });

                UIConsole.Log(string.Format(Lang.Get("Log_ResourceSaved"), type, tier), LogLevel.Info);

                CheckAndAutoSave();
            }
        }

        private static void CheckAndAutoSave()
        {
            if ((DateTime.Now - _lastSaveTime).TotalSeconds >= 15)
            {
                UIConsole.Log(Lang.Get("Log_AutoSave"), LogLevel.Warning);
                SaveFile();
                _lastSaveTime = DateTime.Now;
            }
        }

        public static void SaveFile()
        {
            try
            {
                lock (_lock)
                {
                    if (_currentLog.Players.Count == 0 && _currentLog.Resources.Count == 0) return;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_currentLog, options);

                    // FileShare.ReadWrite › baþka process ayný anda eriþse bile kilitlenmiyor
                    using var fs = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    using var sw = new StreamWriter(fs);
                    sw.Write(json);

                    UIConsole.Log(string.Format(Lang.Get("Log_SaveSuccess"), _currentLog.Players.Count, _currentLog.Resources.Count), LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                UIConsole.Log($"Error Code : 68 | {ex.Message}", LogLevel.Error);
                UIConsole.Log(string.Format(Lang.Get("Log_SaveError"), ex.Message), LogLevel.Error);
            }
        }
    }
}


