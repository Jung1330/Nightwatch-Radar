using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using AlbionDataHandlers.Enums;

namespace AlbionDataHandlers.Utils;

public static class PlayerParserTraceStore
{
    private static readonly string[] _mapJobsKeywords =
    {
        "map", "zone", "cluster", "joinfinished", "join", "leave", "job", "jobs"
    };

    private static readonly Dictionary<EventCodes, Dictionary<byte, string>> _eventParamLabels = new()
    {
        [EventCodes.NewCharacter] = new Dictionary<byte, string>
        {
            [0] = "EntityId",
            [1] = "Name",
            [8] = "Guild",
            [19] = "SpawnX",
            [22] = "CurrentHealth",
            [23] = "MaxHealth",
            [25] = "SpawnY",
            [38] = "Equipment(Alt)",
            [40] = "Equipment",
            [51] = "Alliance",
            [53] = "Faction"
        },
        [EventCodes.Move] = new Dictionary<byte, string>
        {
            [0] = "EntityId",
            [1] = "MovePayload(Primary)",
            [3] = "MovePayload(Alt)",
            [4] = "X(Fallback)",
            [5] = "Y(Fallback)",
            [19] = "X(SpawnStyle)",
            [25] = "Y(SpawnStyle)"
        },
        [EventCodes.Leave] = new Dictionary<byte, string>
        {
            [0] = "EntityId"
        },
        [EventCodes.HealthUpdate] = new Dictionary<byte, string>
        {
            [0] = "EntityId",
            [2] = "CurrentHealth",
            [3] = "MaxHealth"
        },
        [EventCodes.HealthUpdates] = new Dictionary<byte, string>
        {
            [0] = "EntityIdList",
            [2] = "CurrentHealthList",
            [3] = "MaxHealthList"
        }
    };

    public static string DumpMapJobsTestToFile(string? filePath = null)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logsDir);

            string path = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(logsDir, "parser_map_jobs_test_dump.txt")
                : filePath;

            List<TraceEntry> snapshot;
            lock (_lock)
            {
                snapshot = _globalEntries.ToList();
            }

            var filtered = snapshot
                .Where(IsMapJobsEntry)
                .ToList();

            var sb = new StringBuilder(128 * 1024);
            sb.AppendLine($"# Parser Map/Jobs Test Dump | UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"TotalGlobalEntries={snapshot.Count} | FilteredEntries={filtered.Count}");
            sb.AppendLine();

            foreach (var e in filtered)
            {
                sb.Append('[').Append(e.Time.ToString("O")).Append("] ")
                  .Append("Code=").Append(e.EventCode).Append(' ')
                  .Append(e.EventName).Append(" :: ")
                  .AppendLine(e.Payload);
            }

            if (filtered.Count == 0)
            {
                sb.AppendLine("(No map/jobs related entries matched current filter.)");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class TraceEntry
    {
        public DateTime Time { get; set; }
        public int EventCode { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    private static bool IsMapJobsEntry(TraceEntry entry)
    {
        if (entry.EventCode == (int)ResponseCodes.PlayerJoiningMap ||
            entry.EventCode == (int)ResponseCodes.PlayerChangeCluster ||
            entry.EventCode == (int)EventCodes.JoinFinished ||
            entry.EventCode == (int)EventCodes.Leave)
        {
            return true;
        }

        string haystack = (entry.EventName + " " + entry.Payload).ToLowerInvariant();
        return _mapJobsKeywords.Any(k => haystack.Contains(k));
    }

    private static readonly Dictionary<RequestCodes, Dictionary<byte, string>> _requestParamLabels = new()
    {
        [RequestCodes.Move] = new Dictionary<byte, string>
        {
            [0] = "EntityId(Optional)",
            [1] = "CurrentPos",
            [3] = "TargetPos",
            [253] = "OperationCode(Expanded)"
        },
        [RequestCodes.MoveAlt] = new Dictionary<byte, string>
        {
            [0] = "EntityId(Optional)",
            [1] = "CurrentPos",
            [3] = "TargetPos",
            [253] = "OperationCode(Expanded)"
        }
    };

    private static readonly Dictionary<ResponseCodes, Dictionary<byte, string>> _responseParamLabels = new()
    {
        [ResponseCodes.PlayerJoiningMap] = new Dictionary<byte, string>
        {
            [0] = "LocalPlayerId",
            [253] = "OperationCode(Expanded)"
        },
        [ResponseCodes.PlayerChangeCluster] = new Dictionary<byte, string>
        {
            [0] = "LocalPlayerId(Optional)",
            [253] = "OperationCode(Expanded)"
        }
    };

    public static string DumpAllToFile(string? filePath = null)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logsDir);

            string path = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(logsDir, "parser_trace_dump.txt")
                : filePath;

            var sb = new StringBuilder(256 * 1024);
            sb.AppendLine($"# Parser Trace Dump | UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

            lock (_lock)
            {
                PruneUnsafe(DateTime.UtcNow);

                sb.AppendLine("## Global Timeline");
                sb.AppendLine($"Entries={_globalEntries.Count}");
                foreach (var e in _globalEntries)
                {
                    sb.Append('[').Append(e.Time.ToString("O")).Append("] ")
                      .Append("Code=").Append(e.EventCode).Append(' ')
                      .Append(e.EventName).Append(" :: ")
                      .AppendLine(e.Payload);
                }
                sb.AppendLine();

                foreach (var kv in _playerLogs.OrderByDescending(x => x.Value.LastSeen))
                {
                    int id = kv.Key;
                    var p = kv.Value;
                    string name = string.IsNullOrWhiteSpace(p.Name) ? $"ID:{id}" : p.Name;
                    sb.AppendLine($"## Player {name} | Id={id} | LastSeen={p.LastSeen:O} | Entries={p.Entries.Count}");

                    foreach (var e in p.Entries)
                    {
                        sb.Append('[').Append(e.Time.ToString("O")).Append("] ")
                          .Append("Code=").Append(e.EventCode).Append(' ')
                          .Append(e.EventName).Append(" :: ")
                          .AppendLine(e.Payload);
                    }

                    sb.AppendLine();
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class PlayerTrace
    {
        public string Name { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public Queue<TraceEntry> Entries { get; } = new();
    }

    private static readonly object _lock = new();
    private static readonly Dictionary<int, PlayerTrace> _playerLogs = new();
    private static readonly Queue<TraceEntry> _globalEntries = new();
    private const int MaxEntriesPerPlayer = 120;
    private const int MaxGlobalEntries = 4000;
    private const int MaxPlayersTracked = 400;
    private static readonly TimeSpan MaxIdleAge = TimeSpan.FromMinutes(15);

    public static void CaptureEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return;

        DateTime now = DateTime.UtcNow;
        string payload = SerializeParameters(eventCode, parameters);
        string eventName = eventCode.ToString();
        int numericCode = (int)eventCode;

        AppendGlobal(numericCode, eventName, payload, now);

        if (parameters.TryGetValue(0, out var idObj))
        {
            if (TryGetInt(idObj, out int singleId))
            {
                string name = parameters.TryGetValue(1, out var nObj) ? nObj?.ToString() ?? string.Empty : string.Empty;
                Append(singleId, name, numericCode, eventName, payload, now);
                return;
            }

            if (idObj is IList idList)
            {
                int captureCount = Math.Min(idList.Count, 40);
                for (int i = 0; i < captureCount; i++)
                {
                    if (TryGetInt(idList[i], out int batchId))
                        Append(batchId, string.Empty, numericCode, eventName + "[B]", payload, now);
                }
            }
        }
    }

    public static void CaptureRequest(RequestCodes requestCode, Dictionary<byte, object> parameters)
    {
        if (parameters == null) return;

        DateTime now = DateTime.UtcNow;
        string payload = SerializeParameters(requestCode, parameters);
        string eventName = "REQ:" + requestCode;
        int numericCode = (int)requestCode;

        AppendGlobal(numericCode, eventName, payload, now);

        if (parameters.TryGetValue(0, out var idObj))
        {
            if (TryGetInt(idObj, out int singleId))
            {
                string name = parameters.TryGetValue(1, out var nObj) ? nObj?.ToString() ?? string.Empty : string.Empty;
                Append(singleId, name, numericCode, eventName, payload, now);
                return;
            }

            if (idObj is IList idList)
            {
                int captureCount = Math.Min(idList.Count, 40);
                for (int i = 0; i < captureCount; i++)
                {
                    if (TryGetInt(idList[i], out int batchId))
                        Append(batchId, string.Empty, numericCode, eventName + "[B]", payload, now);
                }
            }
        }
    }

    public static void CaptureResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters)
    {
        if (parameters == null) return;

        DateTime now = DateTime.UtcNow;
        string payload = SerializeParameters(responseCode, parameters);
        string eventName = "RES:" + responseCode;
        int numericCode = (int)responseCode;

        AppendGlobal(numericCode, eventName, payload, now);

        if (parameters.TryGetValue(0, out var idObj) && TryGetInt(idObj, out int singleId))
        {
            Append(singleId, string.Empty, numericCode, eventName, payload, now);
        }
    }

    private static void AppendGlobal(int eventCode, string eventName, string payload, DateTime now)
    {
        lock (_lock)
        {
            _globalEntries.Enqueue(new TraceEntry
            {
                Time = now,
                EventCode = eventCode,
                EventName = eventName,
                Payload = payload
            });

            while (_globalEntries.Count > MaxGlobalEntries)
                _globalEntries.Dequeue();
        }
    }

    public static List<(int id, string name)> GetKnownPlayersSnapshot(IEnumerable<(int id, string name)> nearbyPlayers)
    {
        var nearby = nearbyPlayers?.ToDictionary(x => x.id, x => x.name ?? string.Empty) ?? new Dictionary<int, string>();

        lock (_lock)
        {
            PruneUnsafe(DateTime.UtcNow);

            foreach (var kv in nearby)
            {
                if (_playerLogs.TryGetValue(kv.Key, out var p))
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value)) p.Name = kv.Value;
                    p.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    _playerLogs[kv.Key] = new PlayerTrace
                    {
                        Name = kv.Value,
                        LastSeen = DateTime.UtcNow
                    };
                }
            }

            return _playerLogs
                .OrderByDescending(x => x.Value.LastSeen)
                .Select(x => (x.Key, string.IsNullOrWhiteSpace(x.Value.Name) ? $"ID:{x.Key}" : x.Value.Name))
                .ToList();
        }
    }

    public static List<(DateTime time, int eventCode, string eventName, string payload)> GetPlayerEntries(int playerId)
    {
        lock (_lock)
        {
            if (!_playerLogs.TryGetValue(playerId, out var player))
                return new List<(DateTime, int, string, string)>();

            return player.Entries
                .Select(e => (e.Time, e.EventCode, e.EventName, e.Payload))
                .ToList();
        }
    }

    // Geriye dönük uyumluluk: eski çaðrýlar (time, eventName, payload) bekliyorsa çalýþmaya devam etsin.
    public static List<(DateTime time, string eventName, string payload)> GetPlayerEntriesLegacy(int playerId)
    {
        return GetPlayerEntries(playerId)
            .Select(e => (e.time, e.eventName, e.payload))
            .ToList();
    }

    private static void Append(int playerId, string name, int eventCode, string eventName, string payload, DateTime now)
    {
        lock (_lock)
        {
            if (!_playerLogs.TryGetValue(playerId, out var player))
            {
                player = new PlayerTrace();
                _playerLogs[playerId] = player;
            }

            if (!string.IsNullOrWhiteSpace(name))
                player.Name = name;

            player.LastSeen = now;
            player.Entries.Enqueue(new TraceEntry
            {
                Time = now,
                EventCode = eventCode,
                EventName = eventName,
                Payload = payload
            });

            while (player.Entries.Count > MaxEntriesPerPlayer)
                player.Entries.Dequeue();

            PruneUnsafe(now);
        }
    }

    private static void PruneUnsafe(DateTime now)
    {
        var stale = _playerLogs
            .Where(kv => now - kv.Value.LastSeen > MaxIdleAge)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in stale)
            _playerLogs.Remove(id);

        if (_playerLogs.Count <= MaxPlayersTracked) return;

        var removeIds = _playerLogs
            .OrderBy(kv => kv.Value.LastSeen)
            .Take(_playerLogs.Count - MaxPlayersTracked)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in removeIds)
            _playerLogs.Remove(id);
    }

    private static string SerializeParameters(EventCodes eventCode, Dictionary<byte, object> parameters)
    {
        var parts = new List<string>(parameters.Count);
        _eventParamLabels.TryGetValue(eventCode, out var labels);

        foreach (var kv in parameters.OrderBy(k => k.Key))
        {
            if (labels != null && labels.TryGetValue(kv.Key, out var label))
                parts.Add($"[{kv.Key}:{label}]={FormatValue(kv.Value, 0)}");
            else
                parts.Add($"[{kv.Key}]={FormatValue(kv.Value, 0)}");
        }
        return string.Join(" | ", parts);
    }

    private static string SerializeParameters(RequestCodes requestCode, Dictionary<byte, object> parameters)
    {
        var parts = new List<string>(parameters.Count);
        _requestParamLabels.TryGetValue(requestCode, out var labels);

        foreach (var kv in parameters.OrderBy(k => k.Key))
        {
            if (labels != null && labels.TryGetValue(kv.Key, out var label))
                parts.Add($"[{kv.Key}:{label}]={FormatValue(kv.Value, 0)}");
            else
                parts.Add($"[{kv.Key}]={FormatValue(kv.Value, 0)}");
        }

        return string.Join(" | ", parts);
    }

    private static string SerializeParameters(ResponseCodes responseCode, Dictionary<byte, object> parameters)
    {
        var parts = new List<string>(parameters.Count);
        _responseParamLabels.TryGetValue(responseCode, out var labels);

        foreach (var kv in parameters.OrderBy(k => k.Key))
        {
            if (labels != null && labels.TryGetValue(kv.Key, out var label))
                parts.Add($"[{kv.Key}:{label}]={FormatValue(kv.Value, 0)}");
            else
                parts.Add($"[{kv.Key}]={FormatValue(kv.Value, 0)}");
        }

        return string.Join(" | ", parts);
    }

    private static string FormatValue(object value, int depth)
    {
        if (value == null) return "null";
        if (depth >= 2) return "...";

        switch (value)
        {
            case byte[] bytes:
                {
                    int take = bytes.Length <= 64 ? bytes.Length : 64;
                    return $"byte[{bytes.Length}]({string.Join(",", bytes.Take(take))}{(bytes.Length > take ? ",..." : "")})";
                }
            case string s:
                return s.Length > 120 ? s[..120] + "..." : s;
            case IDictionary dict:
                {
                    var list = new List<string>();
                    int i = 0;
                    foreach (DictionaryEntry de in dict)
                    {
                        if (i++ >= 8) { list.Add("..."); break; }
                        list.Add($"{de.Key}:{FormatValue(de.Value, depth + 1)}");
                    }
                    return "{" + string.Join(",", list) + "}";
                }
            case IList list:
                {
                    var vals = new List<string>();
                    int n = Math.Min(list.Count, 12);
                    for (int i = 0; i < n; i++) vals.Add(FormatValue(list[i], depth + 1));
                    if (list.Count > n) vals.Add("...");
                    return "[" + string.Join(",", vals) + "]";
                }
            default:
                if (value is IFormattable formattable)
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                return value.ToString() ?? string.Empty;
        }
    }

    private static bool TryGetInt(object value, out int result)
    {
        result = 0;
        try
        {
            if (value is byte b) { result = b; return true; }
            if (value is short s) { result = s; return true; }
            if (value is int i) { result = i; return true; }
            if (value is long l) { result = (int)l; return true; }
            if (value is byte[] bytes)
            {
                if (bytes.Length == 4) { result = BitConverter.ToInt32(bytes, 0); return true; }
                if (bytes.Length == 2) { result = BitConverter.ToInt16(bytes, 0); return true; }
                if (bytes.Length == 1) { result = bytes[0]; return true; }
                return false;
            }

            if (value is IList || value is IDictionary)
                return false;

            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}


