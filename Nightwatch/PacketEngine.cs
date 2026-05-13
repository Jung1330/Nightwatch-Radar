using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AlbionDataHandlers;
using AlbionDataHandlers.Enums;
using AlbionDataHandlers.Handlers;
using AlbionDataHandlers.Handlers.MapHandler;
using BaseUtils.Logger.Impl;
using PacketDotNet;
using SharpPcap;
using Nightwatch.Managers;
using Nightwatch.UserControls.Language;

namespace Nightwatch
{
    public sealed class UdpPortStat
    {
        public int Port { get; set; }
        public int PacketCount { get; set; }
        public int PhotonLikeCount { get; set; }
        public DateTime LastSeen { get; set; }
        public string LastAdapter { get; set; } = string.Empty;
    }

    public static class UdpPortInspector
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<int, UdpPortStat> _stats = new();
        private static int _targetPort = 5056;
        private static bool _manualOverrideRequested = false;
        private static int _manualOverridePort = 0;

        public static void SetTargetPort(int port)
        {
            if (port <= 0) return;
            lock (_lock) _targetPort = port;
        }

        public static int GetTargetPort()
        {
            lock (_lock) return _targetPort;
        }

        public static void RequestManualOverride(int port)
        {
            if (port <= 0) return;
            lock (_lock)
            {
                _targetPort = port;
                _manualOverrideRequested = true;
                _manualOverridePort = port;
            }
        }

        public static bool TryConsumeManualOverride(out int port)
        {
            lock (_lock)
            {
                if (_manualOverrideRequested && _manualOverridePort > 0)
                {
                    port = _manualOverridePort;
                    _manualOverrideRequested = false;
                    _manualOverridePort = 0;
                    return true;
                }

                port = 0;
                return false;
            }
        }

        public static void Clear()
        {
            lock (_lock) _stats.Clear();
        }

        public static void ReportTraffic(int port, bool photonLike, string adapter)
        {
            if (port <= 0) return;

            lock (_lock)
            {
                if (!_stats.TryGetValue(port, out var s))
                {
                    s = new UdpPortStat { Port = port };
                    _stats[port] = s;
                }

                s.PacketCount++;
                if (photonLike) s.PhotonLikeCount++;
                s.LastSeen = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(adapter)) s.LastAdapter = adapter;
            }
        }

        public static List<UdpPortStat> Snapshot()
        {
            lock (_lock)
            {
                return _stats.Values
                    .Select(x => new UdpPortStat
                    {
                        Port = x.Port,
                        PacketCount = x.PacketCount,
                        PhotonLikeCount = x.PhotonLikeCount,
                        LastSeen = x.LastSeen,
                        LastAdapter = x.LastAdapter
                    })
                    .OrderByDescending(x => x.PhotonLikeCount)
                    .ThenByDescending(x => x.PacketCount)
                    .ThenBy(x => x.Port)
                    .ToList();
            }
        }
    }

    // --- GÜNCELLENMÝÞ HANDLER ---
    public class InternalMapHandler : IEventHandler
    {
        private readonly Action<string> _onRealMapChanged;
        private string _lastMapKey = string.Empty;
        private DateTime _lastMapChangeAt = DateTime.MinValue;

        public InternalMapHandler(Action<string> onRealMapChanged)
        {
            _onRealMapChanged = onRealMapChanged;
        }

        public void OnEvent(EventCodes code, Dictionary<byte, object> parameters) { }

        public void OnRequest(RequestCodes code, Dictionary<byte, object> parameters) { }
        public void OnResponse(ResponseCodes code, Dictionary<byte, object> parameters)
        {
            if (code != ResponseCodes.PlayerJoiningMap && code != ResponseCodes.PlayerChangeCluster)
                return;

            string mapKey = ResolveMapKey(parameters);
            if (string.IsNullOrWhiteSpace(mapKey))
                return;

            DateTime now = DateTime.UtcNow;
            if (string.Equals(_lastMapKey, mapKey, StringComparison.OrdinalIgnoreCase))
                return;

            // Çok kýsa aralýkta gelen çakýþýk cevaplarý filtrele
            if (_lastMapChangeAt != DateTime.MinValue && (now - _lastMapChangeAt).TotalMilliseconds < 500)
                return;

            _lastMapKey = mapKey;
            _lastMapChangeAt = now;
            UIConsole.Log($"[InternalMapHandler] Real map change detected: {mapKey}", LogLevel.Info);
            _onRealMapChanged?.Invoke(mapKey);
        }

        private static string ResolveMapKey(Dictionary<byte, object> parameters)
        {
            // Güncel gözleme göre map/cluster bilgisi bu alanlarda gelebiliyor.
            byte[] candidateKeys = { 8, 61, 67 };
            foreach (var key in candidateKeys)
            {
                if (!parameters.TryGetValue(key, out var value) || value == null)
                    continue;

                string text = value.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }

            return string.Empty;
        }
    }

    public class PacketEngine
    {
        private readonly AlbionDataParser _albionDataParser;
        private ICaptureDevice _device;
        private readonly List<ICaptureDevice> _openedDevices = new();
        private readonly string _localIp;

        // Kuyruk Sistemi (limitsiz)
        private readonly BlockingCollection<byte[]> _packetQueue = new BlockingCollection<byte[]>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // --- Port kontrol için flag ---
        private bool _port5056Detected = false;
        private int _targetUdpPort = 5056;
        private int _lastAppliedTargetPort = -1;
        private DateTime _lastAnyUdpTrafficSeenAt = DateTime.MinValue;
        private DateTime _lastPacketParserErrorAt = DateTime.MinValue;

        public PacketEngine(AlbionDataParser albionDataParser, GameStateManager gameStateManager)
        {
            _albionDataParser = albionDataParser;
            _localIp = GetLocalIPv4();
            UdpPortInspector.SetTargetPort(_targetUdpPort);

            var mapHandler = new InternalMapHandler((mapKey) =>
            {
                PurgeQueue();
                gameStateManager.ClearAllData();
                UIConsole.Log($"[PacketEngine] State reset on map change: {mapKey}", LogLevel.Warning);
            });

            _albionDataParser.RegisterEventHandler(mapHandler);

            // Ýþçiyi baþlat
            Task.Factory.StartNew(ProcessQueue, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private static string GetLocalIPv4()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private bool TryOpenDevice(ICaptureDevice device, int timeoutMs = 1000)
        {
            try
            {
                device.Open(DeviceModes.MaxResponsiveness, timeoutMs);
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                try
                {
                    device.Open(DeviceModes.Promiscuous, timeoutMs);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? MaxResponsiveness desteklenmiyor, Promiscuous fallback kullanildi.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 85 | {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Code : 86 | {ex.Message}");
                return false;
            }
        }

        // --- YENÝ EKLENEN: KUYRUK TEMÝZLEME FONKSÝYONU ---

        private void PurgeQueue()
        {
            // Sýrada bekleyen (iþlenmemiþ) tüm paketleri boþalt
            int dropped = 0;
            while (_packetQueue.TryTake(out _))
            {
                dropped++;
            }
            UIConsole.Log(string.Format(Lang.Get("Packet_Engine"), dropped), LogLevel.Warning);
        }

        private void ProcessQueue()
        {
            foreach (var payload in _packetQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                try
                {
                    if (payload == null || payload.Length < 6)
                        continue;

                    _albionDataParser.ReceivePacket(payload);
                }
                catch (Exception ex)
                {
                    if ((DateTime.Now - _lastPacketParserErrorAt).TotalSeconds >= 2)
                    {
                        _lastPacketParserErrorAt = DateTime.Now;
                        Console.WriteLine($"Error Code : 15 | {ex.GetType().Name} | {ex.Message}");
                    }
                }
            }
        }

        // --- YENÝ: ADAPTÖR KARÞILAÞTIRMA ---
        private void UpdateAdapterIfChanged(ICaptureDevice selectedDevice, string adapterMemPath)
        {
            try
            {
                string currentDescription = selectedDevice.Description;

                // Önceki adaptör bilgisini oku
                string savedAdapterDesc = "";
                if (File.Exists(adapterMemPath))
                {
                    savedAdapterDesc = File.ReadAllText(adapterMemPath).Trim();
                }

                // Eðer farklýysa, güncelle
                if (savedAdapterDesc != currentDescription)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Adaptör deðiþtirildi!");
                    Console.WriteLine($"    Eski: {(string.IsNullOrEmpty(savedAdapterDesc) ? "Kayýt yok" : savedAdapterDesc)}");
                    Console.WriteLine($"    Yeni: {currentDescription}");
                    Console.ResetColor();

                    File.WriteAllText(adapterMemPath, currentDescription);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? last_adapter.txt güncellendi!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Code : 16 | {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Adaptör güncelleme hatasý: {ex.Message}");
                Console.ResetColor();
            }
        }

        // --- YENÝ: UDP PORT KONTROLÜ ---
        private bool ValidatePort(ICaptureDevice device, int port, int timeoutSeconds = 10)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? Port {port} kontrol ediliyor ({timeoutSeconds} saniye bekleniyor)...");
            Console.ResetColor();

            _port5056Detected = false;
            PacketArrivalEventHandler tempHandler = null;
            bool success = false;
            int timeoutMs = Math.Max(1, timeoutSeconds) * 1000;

            try
            {
                tempHandler = (sender, e) =>
                {
                    try
                    {
                        var rawCapture = e.GetPacket();
                        var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data).Extract<UdpPacket>();

                        if (packet != null && (packet.SourcePort == port || packet.DestinationPort == port))
                        {
                            if (!_port5056Detected)
                            {
                                _port5056Detected = true;

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Port {port} aktif! Albion trafiði algýlandý!");
                                Console.WriteLine($"    Kaynak Port: {packet.SourcePort} › Hedef Port: {packet.DestinationPort}");
                                Console.ResetColor();

                                success = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error Code : 17 | {ex.Message}");
                    }
                };

                device.OnPacketArrival += tempHandler;
                if (!TryOpenDevice(device, 1000))
                    return false;
                device.Filter = $"udp port {port}";
                device.StartCapture();

                // Timeout ile bekle
                int elapsed = 0;
                while (elapsed < timeoutMs && !_port5056Detected)
                {
                    Thread.Sleep(500);
                    elapsed += 500;
                    int remainingSeconds = Math.Max(0, timeoutSeconds - (elapsed / 1000));
                    Console.Write($"\r[{DateTime.Now:HH:mm:ss}] ? Bekleniyor... ({remainingSeconds}s kaldý)");
                }

                Console.WriteLine(); // Yeni satýr

                if (!_port5056Detected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? UYARI: Port {port}'dan paket gelmedi!");
                    Console.WriteLine("   Lütfen kontrol et:");
                    Console.WriteLine("   1. Albion Online çalýþýyor mu?");
                    Console.WriteLine("   2. Doðru adaptörü seçtin mi?");
                    Console.WriteLine($"   3. Firewall port {port}'u engelliyor mu?");
                    Console.ResetColor();

                    success = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Code : 18 | {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Port kontrol hatasý: {ex.Message}");
                Console.ResetColor();
                success = false;
            }
            finally
            {
                try
                {
                    if (device != null)
                    {
                        device.StopCapture();
                        if (tempHandler != null)
                            device.OnPacketArrival -= tempHandler;
                        device.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 19 | {ex.Message}");
                }
            }

            return success;
        }

        private int DiscoverAlbionPort(ICaptureDevice device, int timeoutSeconds = 12)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? Tüm UDP portlarý taranýyor (auto discover)...");
            Console.ResetColor();

            var scoreByPort = new ConcurrentDictionary<int, int>();
            var probeParser = new AlbionDataParser();

            PacketArrivalEventHandler? tempHandler = null;
            int timeoutMs = Math.Max(1, timeoutSeconds) * 1000;
            int bestPort = 0;
            string adapterName = device?.Description ?? device?.Name ?? "Adapter";

            // KeÅŸiften önce önceki kalýntý istatistikleri temizleyelim ki canlý sayaç net görünsün.
            UdpPortInspector.Clear();

            try
            {
                tempHandler = (sender, e) =>
                {
                    try
                    {
                        var rawCapture = e.GetPacket();
                        var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data).Extract<UdpPacket>();
                        if (packet == null) return;

                        int[] ports = { packet.SourcePort, packet.DestinationPort };
                        foreach (var p in ports)
                        {
                            if (p <= 0) continue;
                            scoreByPort.AddOrUpdate(p, 1, (_, old) => old + 1);
                            UdpPortInspector.ReportTraffic(p, false, adapterName);
                        }

                        var payload = packet.PayloadData;
                        if (payload == null || payload.Length < 12) return;

                        // Photon parse'e düþmeden önce hafif bir eleme
                        byte cmdCount = payload.Length > 3 ? payload[3] : (byte)0;
                        if (cmdCount == 0 || cmdCount > 64) return;

                        try
                        {
                            probeParser.ReceivePacket((byte[])payload.Clone());
                            foreach (var p in ports)
                            {
                                if (p <= 0) continue;
                                scoreByPort.AddOrUpdate(p, 8, (_, old) => old + 8);
                                UdpPortInspector.ReportTraffic(p, true, adapterName);
                            }
                        }
                        catch
                        {
                            // parse baþarýsýzsa skor vermiyoruz
                        }
                    }
                    catch
                    {
                    }
                };

                device.OnPacketArrival += tempHandler;
                if (!TryOpenDevice(device, 1000))
                    return 0;
                device.Filter = "udp";
                device.StartCapture();

                int elapsed = 0;
                while (elapsed < timeoutMs)
                {
                    Thread.Sleep(250);
                    elapsed += 250;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Code : 82 | {ex.Message}");
            }
            finally
            {
                try
                {
                    if (device != null)
                    {
                        device.StopCapture();
                        if (tempHandler != null)
                            device.OnPacketArrival -= tempHandler;
                        device.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 83 | {ex.Message}");
                }
            }

            if (scoreByPort.Count > 0)
            {
                bestPort = scoreByPort.OrderByDescending(x => x.Value).First().Key;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Ahanda portun bu: {bestPort}");
                Console.ResetColor();
            }

            return bestPort;
        }

        public void Start()
        {
            CaptureDeviceList.Instance.Refresh();
            var devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(Lang.Get("Err_NoAdapter"));
                Console.ResetColor();
                return;
            }

            string[] virtualKeywords =
            {
                "loopback", "npcap", "vmware", "hyper-v", "vbox", "tap", "wan miniport", "wsl", "pseudo"
            };

            int opened = 0;
            foreach (var dev in devices)
            {
                try
                {
                    string desc = (dev.Description ?? string.Empty).ToLowerInvariant();
                    if (virtualKeywords.Any(k => desc.Contains(k)))
                        continue;

                    dev.OnPacketArrival += PacketHandler;
                    dev.Open(DeviceModes.Promiscuous, 1);
                    dev.Filter = "udp";
                    dev.StartCapture();
                    _openedDevices.Add(dev);
                    if (_device == null) _device = dev;
                    opened++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 22 | {ex.Message}");
                }
            }

            Console.ForegroundColor = opened > 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] AOSniffer-style capture aktif. Açýlan adaptör: {opened}");
            Console.ResetColor();
        }
        private void PacketHandler(object sender, PacketCapture e)
        {
            try
            {
                RawCapture rawCapture = e.GetPacket();
                var packetRoot = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
                var packet = packetRoot.Extract<UdpPacket>();
                if (packet != null)
                {
                    _lastAnyUdpTrafficSeenAt = DateTime.UtcNow;
                }

                if (packet == null) return;

                UdpPortInspector.ReportTraffic(packet.SourcePort, false, ((ICaptureDevice)sender).Description ?? string.Empty);
                UdpPortInspector.ReportTraffic(packet.DestinationPort, false, ((ICaptureDevice)sender).Description ?? string.Empty);

                if (packet.SourcePort == 5056 || packet.DestinationPort == 5056)
                {
                    UdpPortInspector.ReportTraffic(packet.SourcePort, true, ((ICaptureDevice)sender).Description ?? string.Empty);
                    UdpPortInspector.ReportTraffic(packet.DestinationPort, true, ((ICaptureDevice)sender).Description ?? string.Empty);

                    _packetQueue.Add(packet.PayloadData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Code : 23 | {ex.Message}");
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _packetQueue.CompleteAdding();

            foreach (var dev in _openedDevices)
            {
                try
                {
                    dev.StopCapture();
                    dev.OnPacketArrival -= PacketHandler;
                    dev.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 24 | {ex.Message}");
                }
            }

            _openedDevices.Clear();
        }
    }
}


