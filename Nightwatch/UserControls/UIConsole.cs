using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using ImGuiNET;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    // Bunlar sadece BURADA tanımlı olmalı!
    public enum LogLevel { Info, Warning, Error, Success, Logo }

    public struct LogEntry
    {
        public string Message;
        public LogLevel Level;
    }

    public static class UIConsole
    {
        // --- A1: Log listesi KASITLI OLARAK SINIRSIZ tutulur (kullanıcı tercihi).
        // 0 = sınırsız. İleride bir üst sınır istersen burayı değiştirmen yeterli (örn. 200000).
        private const int MaxLogEntries = 0;

        private static readonly List<LogEntry> _logs = new List<LogEntry>();

        // Yazma (paket/IPC thread'leri) ile okuma (render thread'i) arasında kilit.
        private static readonly object _logsLock = new object();

        // Çizim için önbellek: liste saniyede en fazla 10 kez kopyalanır, her karede değil.
        private static LogEntry[] _visibleSnapshot = Array.Empty<LogEntry>();
        private static DateTime _lastSnapshotUtc = DateTime.MinValue;
        private static bool _snapshotDirty = true;

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

            lock (_logsLock)
            {
                _logs.Add(new LogEntry { Message = timedMessage, Level = level });

                if (MaxLogEntries > 0 && _logs.Count > MaxLogEntries)
                    _logs.RemoveRange(0, _logs.Count - MaxLogEntries);

                _snapshotDirty = true;
            }

            // AlbionOverlay konsoluna da ilet
            ExternalLogger?.Invoke(timedMessage, level);
        }

        public static void Clear()
        {
            lock (_logsLock)
            {
                _logs.Clear();
                _snapshotDirty = true;
            }
        }

        /// <summary>
        /// Çizim için görünür kayıtların kopyasını döndürür.
        /// Kilit yalnızca kopyalama sırasında tutulur; çizim kilitsiz devam eder.
        /// Kopya en fazla 100 ms'de bir yenilenir (kare başına maliyet sabit kalır).
        /// </summary>
        private static LogEntry[] GetVisibleSnapshot()
        {
            if (!_snapshotDirty && (DateTime.UtcNow - _lastSnapshotUtc).TotalMilliseconds < 100)
                return _visibleSnapshot;

            lock (_logsLock)
            {
                var filtered = new List<LogEntry>(_logs.Count);
                foreach (var log in _logs)
                {
                    if (log.Level == LogLevel.Info && !_showInfo) continue;
                    if (log.Level == LogLevel.Warning && !_showWarning) continue;
                    if (log.Level == Nightwatch.LogLevel.Error && !_showError) continue;
                    if (log.Level == LogLevel.Success && !_showSuccess) continue;
                    if (log.Level == LogLevel.Logo && !_showInfo) continue; // Logo info gibi davranır

                    filtered.Add(log);
                }

                _visibleSnapshot = filtered.ToArray();
                _snapshotDirty = false;
                _lastSnapshotUtc = DateTime.UtcNow;
            }

            return _visibleSnapshot;
        }

        private static void SaveLogsToFile()
        {
            try
            {
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");
                Directory.CreateDirectory(logsDir);

                string filePath = Path.Combine(logsDir, $"UIConsole_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                LogEntry[] snapshot;
                lock (_logsLock)
                {
                    snapshot = _logs.ToArray();
                }

                var sb = new StringBuilder();
                foreach (var log in snapshot)
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
                Clear();
            }

            ImGui.SameLine();
            ImGui.Checkbox(Lang.Get("DevTools_Console_Auto_Scroll"), ref autoScroll);

            ImGui.SameLine();
            if (ImGui.Button(Lang.Get("DevTools_Console_SaveLog") ?? "Save Log"))
            {
                SaveLogsToFile();
            }

            // Log Filtreleri (değişince önbellek bir sonraki çizimde tazelenir)
            bool wasShowInfo = _showInfo, wasWarning = _showWarning, wasError = _showError, wasSuccess = _showSuccess;
            ImGui.Checkbox("Info", ref _showInfo); ImGui.SameLine();
            ImGui.Checkbox("Warning", ref _showWarning); ImGui.SameLine();
            ImGui.Checkbox("Error", ref _showError); ImGui.SameLine();
            ImGui.Checkbox("Success", ref _showSuccess);
            if (wasShowInfo != _showInfo || wasWarning != _showWarning || wasError != _showError || wasSuccess != _showSuccess)
                _snapshotDirty = true;

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
                LogEntry[] visible = GetVisibleSnapshot();

                if (visible.Length == 0)
                {
                    ImGui.TextDisabled("...");
                }
                else
                {
                    // --- Sanal çizim: yalnızca ekranda görünen satırlar çizilir.
                    // Liste sınırsız büyüse bile kare maliyeti sabit kalır.
                    //
                    // ÖNEMLİ (assert düzeltmesi): ImGui, imleci içeriğin ötesine taşıyıp
                    // HİÇBİR öğe çizmezsen hata verir:
                    //   "Code uses SetCursorPos() to extend window boundaries.
                    //    Please submit an item e.g. Dummy() to validate extent."
                    // Bu yüzden görünmeyen baş/son kısımlar SetCursorPosY yerine
                    // Dummy() ile temsil edilir.
                    float lineH = Math.Max(1f, ImGui.GetTextLineHeightWithSpacing());
                    float scrollY = ImGui.GetScrollY();
                    float viewH = Math.Max(lineH, ImGui.GetWindowHeight());

                    int first = Math.Max(0, (int)(scrollY / lineH) - 1);
                    int last = Math.Min(visible.Length, first + (int)(viewH / lineH) + 2);

                    // 1) Ekranın üstünde kalan (görünmeyen) satırların yerini tut
                    if (first > 0)
                        ImGui.Dummy(new Vector2(1f, first * lineH));

                    // 2) Görünen satırları çiz
                    for (int i = first; i < last; i++)
                    {
                        Vector4 color = new Vector4(0.8f, 0.8f, 0.8f, 1);
                        if (visible[i].Level == LogLevel.Warning) color = new Vector4(1, 1, 0, 1);
                        else if (visible[i].Level == Nightwatch.LogLevel.Error) color = new Vector4(1, 0.3f, 0.3f, 1);
                        else if (visible[i].Level == LogLevel.Success) color = new Vector4(0.2f, 1, 0.2f, 1);
                        else if (visible[i].Level == LogLevel.Logo) color = new Vector4(1.0f, 0.0f, 1.0f, 1.0f);

                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.TextUnformatted(visible[i].Message);
                        ImGui.PopStyleColor();
                    }

                    // 3) Ekranın altında kalan satırların yerini tut
                    //    (kaydırma çubuğu doğru toplam yüksekliği görsün)
                    float remaining = (visible.Length - last) * lineH;
                    if (remaining > 0f)
                        ImGui.Dummy(new Vector2(1f, remaining));
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
