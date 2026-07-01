using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Nightwatch.UserControls.Language;

public enum LogLevel { Info, Warning, Error }

public struct LogEntry
{
    public string Message;
    public LogLevel Level;
}

public static class Logger
{
    private static List<LogEntry> logs = new List<LogEntry>();
    private static bool autoScroll = true;

    // Log ekleme metodumuz
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        logs.Add(new LogEntry { Message = message, Level = level });
    }

    // Bu metodu ImGui Render döngünün (UI çizdiğin yerin) içinde çağıracaksın
    public static void DrawConsoleWindow()
    {
        ImGui.Begin("DevTools / Console");

        if (ImGui.Button(Lang.Get("DevTools_Console_Clear")))
            logs.Clear();
        ImGui.SameLine();
        ImGui.Checkbox((Lang.Get("DevTools_Console_Auto_Scroll")), ref autoScroll);

        ImGui.Separator();

        // Logları yazdıracağımız kaydırılabilir alan
        ImGui.BeginChild("LogScrollRegion", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);

        foreach (var log in logs)
        {
            // Log seviyesine göre renk belirleme
            Vector4 color = new Vector4(1, 1, 1, 1); // Varsayılan Beyaz (Info)
            if (log.Level == LogLevel.Warning) color = new Vector4(1, 1, 0, 1); // Sarı
            else if (log.Level == LogLevel.Error) color = new Vector4(1, 0, 0, 1); // Kırmızı

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"[{log.Level}] {log.Message}");
            ImGui.PopStyleColor();
        }

        // Otomatik aşağı kaydırma mantığı
        if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1.0f);
        }

        ImGui.EndChild();
        ImGui.End();
    }
}





