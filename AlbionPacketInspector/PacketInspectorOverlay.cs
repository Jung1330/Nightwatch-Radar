using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Collections;
using ClickableTransparentOverlay;
using ImGuiNET;
using Newtonsoft.Json;
using AlbionDataHandlers;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Enums;

namespace AlbionPacketInspector
{
    public enum PacketType { Event, Request, Response }

    public class CapturedPacket
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public PacketType Type { get; set; }
        public byte Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public Dictionary<byte, object> Parameters { get; set; } = new();
        public short? ReturnCode { get; set; }
        public string? DebugMessage { get; set; }
    }

    public class PacketInspectorOverlay : Overlay, IEventHandler
    {
        private readonly AlbionDataParser _parser;
        private readonly PacketListener _listener;
        
        // List to hold captured packets
        private readonly List<CapturedPacket> _packets = new();
        private readonly object _lock = new();

        // UI state variables
        private CapturedPacket? _selectedPacket;
        private string _searchText = string.Empty;
        private bool _showEvents = true;
        private bool _showRequests = true;
        private bool _showResponses = true;
        private bool _isPaused = false;
        private int _maxPacketCount = 500;

        // Logging state variables
        private bool _isRecording = false;
        private string? _currentLogFile;
        private readonly object _fileLock = new();
        private string? _saveMessage;
        private DateTime _saveMessageTime;

        public PacketInspectorOverlay() : base("Albion Packet Inspector", 1920, 1080)
        {
            _parser = new AlbionDataParser();
            _parser.RegisterEventHandler(this);
            _listener = new PacketListener(_parser);
            
            // Start listener
            _listener.Start();
        }

        private void StartRecording()
        {
            try
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PacketLogs");
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                _currentLogFile = System.IO.Path.Combine(dir, $"packet_record_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                _isRecording = true;
                _saveMessage = $"Started recording to PacketLogs\\{System.IO.Path.GetFileName(_currentLogFile)}";
                _saveMessageTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recording] Error starting: {ex.Message}");
            }
        }

        private void StopRecording()
        {
            if (_isRecording && !string.IsNullOrEmpty(_currentLogFile))
            {
                _saveMessage = $"Stopped recording. Saved to PacketLogs\\{System.IO.Path.GetFileName(_currentLogFile)}";
                _saveMessageTime = DateTime.Now;
            }
            _isRecording = false;
            _currentLogFile = null;
        }

        private void WritePacketToFile(CapturedPacket p)
        {
            if (!_isRecording || string.IsNullOrEmpty(_currentLogFile)) return;

            lock (_fileLock)
            {
                try
                {
                    using var sw = System.IO.File.AppendText(_currentLogFile);
                    sw.WriteLine($"[{p.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{p.Type}] Code: {p.Code} | Name: {p.Name}");
                    if (p.ReturnCode.HasValue)
                    {
                        sw.WriteLine($"  ReturnCode: {p.ReturnCode} | DebugMsg: {p.DebugMessage}");
                    }
                    foreach (var kv in p.Parameters.OrderBy(x => x.Key))
                    {
                        string valStr = FormatValue(kv.Value);
                        string typeStr = kv.Value?.GetType().Name ?? "null";
                        sw.WriteLine($"  Parameter [{kv.Key}] ({typeStr}) = {valStr}");
                    }
                    sw.WriteLine(new string('-', 80));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Recording] Error writing packet: {ex.Message}");
                }
            }
        }

        private void SaveBufferToFile()
        {
            try
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PacketLogs");
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                string filename = System.IO.Path.Combine(dir, $"packet_dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                List<CapturedPacket> bufferCopy;
                lock (_lock)
                {
                    bufferCopy = new List<CapturedPacket>(_packets);
                }

                using (var sw = new System.IO.StreamWriter(filename))
                {
                    foreach (var p in bufferCopy)
                    {
                        sw.WriteLine($"[{p.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{p.Type}] Code: {p.Code} | Name: {p.Name}");
                        if (p.ReturnCode.HasValue)
                        {
                            sw.WriteLine($"  ReturnCode: {p.ReturnCode} | DebugMsg: {p.DebugMessage}");
                        }
                        foreach (var kv in p.Parameters.OrderBy(x => x.Key))
                        {
                            string valStr = FormatValue(kv.Value);
                            string typeStr = kv.Value?.GetType().Name ?? "null";
                            sw.WriteLine($"  Parameter [{kv.Key}] ({typeStr}) = {valStr}");
                        }
                        sw.WriteLine(new string('-', 80));
                    }
                }

                _saveMessage = $"Dumped {bufferCopy.Count} packets to PacketLogs\\{System.IO.Path.GetFileName(filename)}";
                _saveMessageTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _saveMessage = $"Error saving: {ex.Message}";
                _saveMessageTime = DateTime.Now;
            }
        }

        private void SaveSinglePacketToFile(CapturedPacket p)
        {
            try
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PacketLogs");
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
                string filename = System.IO.Path.Combine(dir, $"packet_single_{p.Type}_{p.Code}_{p.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                using (var sw = new System.IO.StreamWriter(filename))
                {
                    sw.WriteLine($"[{p.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{p.Type}] Code: {p.Code} | Name: {p.Name}");
                    if (p.ReturnCode.HasValue)
                    {
                        sw.WriteLine($"  ReturnCode: {p.ReturnCode} | DebugMsg: {p.DebugMessage}");
                    }
                    sw.WriteLine(new string('=', 40));
                    foreach (var kv in p.Parameters.OrderBy(x => x.Key))
                    {
                        string valStr = FormatValue(kv.Value);
                        sw.WriteLine($"parameter[{kv.Key}] = {valStr}");
                    }
                }

                _saveMessage = $"Saved selected packet to PacketLogs\\{System.IO.Path.GetFileName(filename)}";
                _saveMessageTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _saveMessage = $"Error saving selected packet: {ex.Message}";
                _saveMessageTime = DateTime.Now;
            }
        }

        public void OnEvent(EventCodes eventCode, Dictionary<byte, object> parameters)
        {
            if (_isPaused) return;

            Console.WriteLine($"[Parser] Parsed OnEvent. Code: {(byte)eventCode} ({eventCode})");

            var captured = new CapturedPacket
            {
                Type = PacketType.Event,
                Code = (byte)eventCode,
                Name = eventCode.ToString(),
                Parameters = new Dictionary<byte, object>(parameters)
            };

            lock (_lock)
            {
                _packets.Add(captured);
                if (_packets.Count > _maxPacketCount)
                {
                    _packets.RemoveAt(0);
                }
            }

            WritePacketToFile(captured);
        }

        public void OnRequest(RequestCodes requestCode, Dictionary<byte, object> parameters)
        {
            if (_isPaused) return;

            Console.WriteLine($"[Parser] Parsed OnRequest. Code: {(byte)requestCode} ({requestCode})");

            var captured = new CapturedPacket
            {
                Type = PacketType.Request,
                Code = (byte)requestCode,
                Name = requestCode.ToString(),
                Parameters = new Dictionary<byte, object>(parameters)
            };

            lock (_lock)
            {
                _packets.Add(captured);
                if (_packets.Count > _maxPacketCount)
                {
                    _packets.RemoveAt(0);
                }
            }

            WritePacketToFile(captured);
        }

        public void OnResponse(ResponseCodes responseCode, Dictionary<byte, object> parameters)
        {
            if (_isPaused) return;

            Console.WriteLine($"[Parser] Parsed OnResponse. Code: {(byte)responseCode} ({responseCode})");

            var captured = new CapturedPacket
            {
                Type = PacketType.Response,
                Code = (byte)responseCode,
                Name = responseCode.ToString(),
                Parameters = new Dictionary<byte, object>(parameters)
            };

            lock (_lock)
            {
                _packets.Add(captured);
                if (_packets.Count > _maxPacketCount)
                {
                    _packets.RemoveAt(0);
                }
            }

            WritePacketToFile(captured);
        }

        protected override void Render()
        {
            // Set window size & position on first run, but let user move/resize it
            ImGui.SetNextWindowSize(new Vector2(1600, 950), ImGuiCond.FirstUseEver);
            
            ImGui.Begin("Albion Packet Inspector", ImGuiWindowFlags.None);

            // --- Top Toolbar ---
            RenderToolbar();

            if (_listener.OpenedDevicesCount == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
                ImGui.TextWrapped("WARNING: No network adapters were successfully opened! Please run this application as Administrator to allow packet capture.");
                ImGui.PopStyleColor();
            }

            ImGui.Separator();

            // --- Main Content (Split screen) ---
            if (ImGui.BeginTable("InspectorLayout", 2, ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("PacketsList", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableSetupColumn("PacketDetails", ImGuiTableColumnFlags.WidthStretch, 0.6f);
                ImGui.TableNextRow();

                // --- Column 1: Packet List ---
                ImGui.TableNextColumn();
                RenderPacketList();

                // --- Column 2: Detail view ---
                ImGui.TableNextColumn();
                RenderPacketDetails();

                ImGui.EndTable();
            }

            ImGui.End();
        }

        private void RenderToolbar()
        {
            // Search Input
            ImGui.SetNextItemWidth(200f);
            ImGui.InputTextWithHint("##search", "Search by code or name...", ref _searchText, 100);
            
            ImGui.SameLine();

            // Filters Checkboxes
            ImGui.Checkbox("Events", ref _showEvents);
            ImGui.SameLine();
            ImGui.Checkbox("Requests", ref _showRequests);
            ImGui.SameLine();
            ImGui.Checkbox("Responses", ref _showResponses);

            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "|");
            ImGui.SameLine();

            // Record to File Checkbox
            bool isRec = _isRecording;
            if (ImGui.Checkbox("Record to File", ref isRec))
            {
                if (isRec) StartRecording();
                else StopRecording();
            }

            ImGui.SameLine();

            // Save Buffer Button
            if (ImGui.Button("Save Buffer"))
            {
                SaveBufferToFile();
            }

            // Save Message Toast
            if (!string.IsNullOrEmpty(_saveMessage) && (DateTime.Now - _saveMessageTime).TotalSeconds < 5)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), _saveMessage);
            }

            ImGui.SameLine(ImGui.GetWindowWidth() - 350f);

            // Pause / Play
            if (_isPaused)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
                if (ImGui.Button("Resume Capture"))
                {
                    _isPaused = false;
                }
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f));
                if (ImGui.Button("Pause Capture"))
                {
                    _isPaused = true;
                }
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();

            // Clear Button
            if (ImGui.Button("Clear"))
            {
                lock (_lock)
                {
                    _packets.Clear();
                    _selectedPacket = null;
                }
            }

            ImGui.SameLine();
            
            // Limit Item Width
            ImGui.SetNextItemWidth(80f);
            ImGui.SliderInt("Max", ref _maxPacketCount, 100, 2000);
        }

        private void RenderPacketList()
        {
            ImGui.BeginChild("ListPanel", new Vector2(0, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Borders);

            List<CapturedPacket> filteredList;
            lock (_lock)
            {
                filteredList = _packets.Where(p =>
                {
                    // Filter Type
                    if (p.Type == PacketType.Event && !_showEvents) return false;
                    if (p.Type == PacketType.Request && !_showRequests) return false;
                    if (p.Type == PacketType.Response && !_showResponses) return false;

                    // Filter Search Text
                    if (!string.IsNullOrEmpty(_searchText))
                    {
                        var codeStr = p.Code.ToString();
                        if (!codeStr.Contains(_searchText) && !p.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                }).ToList();
            }

            if (filteredList.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No packets captured matching filters.");
            }
            else
            {
                for (int i = filteredList.Count - 1; i >= 0; i--) // Render new packets at the top
                {
                    var p = filteredList[i];
                    string typeLabel = p.Type switch
                    {
                        PacketType.Event => "[EV]",
                        PacketType.Request => "[RQ]",
                        PacketType.Response => "[RS]",
                        _ => "[??]"
                    };

                    Vector4 typeColor = p.Type switch
                    {
                        PacketType.Event => new Vector4(0.2f, 0.7f, 1.0f, 1.0f),    // Blue for Events
                        PacketType.Request => new Vector4(1.0f, 0.8f, 0.2f, 1.0f),  // Gold/Yellow for Requests
                        PacketType.Response => new Vector4(0.2f, 0.9f, 0.2f, 1.0f), // Green for Responses
                        _ => new Vector4(1f, 1f, 1f, 1f)
                    };

                    string label = $"{p.Timestamp:HH:mm:ss.fff} {typeLabel} [{p.Code}] {p.Name}##{i}";
                    
                    ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
                    bool isSelected = _selectedPacket == p;
                    if (ImGui.Selectable(label, isSelected))
                    {
                        _selectedPacket = p;
                    }
                    ImGui.PopStyleColor();

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }

            ImGui.EndChild();
        }

        private void RenderPacketDetails()
        {
            ImGui.BeginChild("DetailsPanel", new Vector2(0, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Borders);

            if (_selectedPacket == null)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Select a packet from the list to view its parameters.");
                ImGui.EndChild();
                return;
            }

            var p = _selectedPacket;
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1f, 1f), $"Packet Details");
            ImGui.SameLine();
            if (ImGui.Button("Save Packet to File"))
            {
                SaveSinglePacketToFile(p);
            }
            ImGui.Text($"Time: {p.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            ImGui.Text($"Type: {p.Type} | Code: {p.Code} | Name: {p.Name}");
            if (p.ReturnCode.HasValue)
            {
                ImGui.Text($"Return Code: {p.ReturnCode} | Debug Msg: {p.DebugMessage}");
            }
            ImGui.Text($"Parameters Count: {p.Parameters.Count}");

            ImGui.Separator();

            if (p.Parameters.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "This packet has no parameters.");
            }
            else
            {
                if (ImGui.BeginTable("ParametersTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed, 50f);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 150f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60f);
                    ImGui.TableHeadersRow();

                    foreach (var kv in p.Parameters.OrderBy(x => x.Key))
                    {
                        ImGui.TableNextRow();
                        
                        // Key
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(kv.Key.ToString());

                        // Type
                        ImGui.TableSetColumnIndex(1);
                        string typeName = kv.Value?.GetType().Name ?? "null";
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), typeName);

                        // Value
                        ImGui.TableSetColumnIndex(2);
                        string valString = FormatValue(kv.Value);
                        ImGui.TextWrapped(valString);

                        // Actions
                        ImGui.TableSetColumnIndex(3);
                        if (ImGui.Button($"Copy##{kv.Key}"))
                        {
                            ImGui.SetClipboardText(valString);
                        }
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.EndChild();
        }

        private string FormatValue(object? value)
        {
            if (value == null) return "null";

            // If it is a byte array or IList, format nicely
            if (value is byte[] bytes)
            {
                return $"[Bytes: {bytes.Length}] " + string.Join(", ", bytes.Select(b => b.ToString("X2")));
            }
            
            if (value is float[] floats)
            {
                return "[" + string.Join(", ", floats) + "]";
            }

            if (value is double[] doubles)
            {
                return "[" + string.Join(", ", doubles) + "]";
            }

            if (value is int[] ints)
            {
                return "[" + string.Join(", ", ints) + "]";
            }

            if (value is string[] strings)
            {
                return "[" + string.Join(", ", strings.Select(s => $"\"{s}\"")) + "]";
            }

            if (value is IList list)
            {
                var elements = new List<string>();
                foreach (var el in list)
                {
                    elements.Add(FormatValue(el));
                }
                return "[ " + string.Join(", ", elements) + " ]";
            }

            if (value is IDictionary dict)
            {
                var pairs = new List<string>();
                foreach (DictionaryEntry de in dict)
                {
                    pairs.Add($"{de.Key}: {FormatValue(de.Value)}");
                }
                return "{ " + string.Join(", ", pairs) + " }";
            }

            return value.ToString() ?? "null";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRecording();
                _listener.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
