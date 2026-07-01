using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using ImGuiNET;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    // Bunlar sadece BURADA tan�ml� olmal�!
    public enum LogLevel { Info, Warning, Error, Success, Logo }

    public struct LogEntry
    {
        public string Message;
        public LogLevel Level;
    }

    public static class UIConsole
    {
        private static List<LogEntry> logs = new List<LogEntry>();
        private static bool autoScroll = true;
        private static bool _showInfo = true;
        private static bool _showWarning = true;
        private static bool _showError = true;
        private static bool _showSuccess = true;

        private static string _lastSaveStatus = "";
        private static LogLevel _lastSaveStatusLevel = LogLevel.Info;

        // AlbionOverlay'in AddUIConsoleLog'una köprü
        public static Action<string, LogLevel> ExternalLogger;

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            // Eğer mesaj zaten [HH:mm:ss] ile başlıyorsa tekrar ekleme
            string timedMessage = message.StartsWith("[") ? message : $"[{DateTime.Now:HH:mm:ss}] {message}";
            logs.Add(new LogEntry { Message = timedMessage, Level = level });

            // AlbionOverlay konsoluna da ilet
            ExternalLogger?.Invoke(timedMessage, level);
        }

        private static void SaveLogsToFile()
        {
            try
            {
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");
                Directory.CreateDirectory(logsDir);

                string filePath = Path.Combine(logsDir, $"UIConsole_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                var sb = new StringBuilder();
                foreach (var log in logs)
                    sb.AppendLine($"[{log.Level}] {log.Message}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                _lastSaveStatus = string.Format(Lang.Get("DevTools_Console_SaveSuccess") ?? "Saved: {0}", filePath);
                _lastSaveStatusLevel = LogLevel.Success;
            }
            catch (Exception ex)
            {
                _lastSaveStatus = string.Format(Lang.Get("DevTools_Console_SaveFailed") ?? "Save failed: {0}", ex.Message);
                _lastSaveStatusLevel = Nightwatch.LogLevel.Error;
            }
        }

        public static void DrawConsoleWindow()
        {
            // Buton ve Checkbox artık JSON'dan geliyor
            if (ImGui.Button(Lang.Get("DevTools_Console_Clear")))
            {
                logs.Clear();
            }

            ImGui.SameLine();
            ImGui.Checkbox(Lang.Get("DevTools_Console_Auto_Scroll"), ref autoScroll);

            ImGui.SameLine();
            if (ImGui.Button(Lang.Get("DevTools_Console_SaveLog") ?? "Save Log"))
            {
                SaveLogsToFile();
            }

            // Log Filtreleri
            ImGui.Checkbox("Info", ref _showInfo); ImGui.SameLine();
            ImGui.Checkbox("Warning", ref _showWarning); ImGui.SameLine();
            ImGui.Checkbox("Error", ref _showError); ImGui.SameLine();
            ImGui.Checkbox("Success", ref _showSuccess);

            if (!string.IsNullOrWhiteSpace(_lastSaveStatus))
            {
                Vector4 statusCol = _lastSaveStatusLevel switch
                {
                    LogLevel.Success => new Vector4(0.2f, 1f, 0.2f, 1f),
                    Nightwatch.LogLevel.Error => new Vector4(1f, 0.3f, 0.3f, 1f),
                    _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
                };
                ImGui.TextColored(statusCol, _lastSaveStatus);
            }

            ImGui.Separator();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.02f, 0.02f, 0.02f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));

            if (ImGui.BeginChild("LogScrollRegion", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
            {
                foreach (var log in logs)
                {
                    if (log.Level == LogLevel.Info && !_showInfo) continue;
                    if (log.Level == LogLevel.Warning && !_showWarning) continue;
                    if (log.Level == Nightwatch.LogLevel.Error && !_showError) continue;
                    if (log.Level == LogLevel.Success && !_showSuccess) continue;
                    if (log.Level == LogLevel.Logo && !_showInfo) continue; // Logo info gibi davranır

                    Vector4 color = new Vector4(0.8f, 0.8f, 0.8f, 1);
                    if (log.Level == LogLevel.Warning) color = new Vector4(1, 1, 0, 1);
                    else if (log.Level == Nightwatch.LogLevel.Error) color = new Vector4(1, 0.3f, 0.3f, 1);
                    else if (log.Level == LogLevel.Success) color = new Vector4(0.2f, 1, 0.2f, 1);
                    else if (log.Level == LogLevel.Logo) color = new Vector4(1.0f, 0.0f, 1.0f, 1.0f);

                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    ImGui.TextUnformatted($"{log.Message}");
                    ImGui.PopStyleColor();
                }

                if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                {
                    ImGui.SetScrollHereY(1.0f);
                }
                ImGui.EndChild();
            }
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }
}




