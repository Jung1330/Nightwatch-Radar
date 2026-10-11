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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private int _targetUdpPort = 5056;
        private DateTime _lastAnyUdpTrafficSeenAt = DateTime.MinValue;
        private DateTime _lastPacketParserErrorAt = DateTime.MinValue;

        public PacketEngine(AlbionDataParser albionDataParser, GameStateManager gameStateManager)
        {
            _albionDataParser = albionDataParser;
            _localIp = GetLocalIPv4();
            UdpPortInspector.SetTargetPort(_targetUdpPort);



            // İşçiyi başlat
            Task.Factory.StartNew(ProcessQueue, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task.Factory.StartNew(StartIpcListener, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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

        private bool TryOpenDevice(ICaptureDevice device, int timeoutMs = 20)
        {
            try
            {
                // FIX: MaxResponsiveness and Promiscuous modes cause Hypervisor_Error BSOD on Hyper-V enabled machines
                device.Open(DeviceModes.None, timeoutMs);
                return true;
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                return false;
            }
        }

        // --- YENİ EKLENEN: KUYRUK TEMİZLEME FONKSİYONU ---

        public void PurgeQueue()
        {
            // Sırada bekleyen (işlenmemiş) tüm paketleri boşalt
            int dropped = 0;
            while (_packetQueue.TryTake(out _))
            {
                dropped++;
            }
            Nightwatch.UIConsole.Log(string.Format(Lang.Get("Packet_Engine"), dropped), LogLevel.Warning);
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

        private void StartIpcListener()
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, 50599);
                listener.Start();
                Nightwatch.UIConsole.Log("[PacketEngine] Fake Server IPC Listener started on 127.0.0.1:50599", LogLevel.Info);

                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (listener.Pending())
                    {
                        var client = listener.AcceptTcpClient();
                        Task.Run(() => HandleIpcClient(client));
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[PacketEngine] IPC Listener Error: {ex.Message}", LogLevel.Error);
            }
        }

        private void HandleIpcClient(TcpClient client)
        {
            try
            {
                using var reader = new StreamReader(client.GetStream());
                string json = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var payload = JsonConvert.DeserializeObject<FakePacketPayload>(json);
                    if (payload != null)
                    {
                        var parameters = ConvertJObjectToDictionary(payload.Parameters);
                        
                        if (payload.Type.Equals("Event", StringComparison.OrdinalIgnoreCase))
                        {
                            _albionDataParser.InjectParsedEvent(payload.Code, parameters);
                        }
                        else if (payload.Type.Equals("Request", StringComparison.OrdinalIgnoreCase))
                        {
                            _albionDataParser.InjectParsedRequest(payload.Code, parameters);
                        }
                        else if (payload.Type.Equals("Response", StringComparison.OrdinalIgnoreCase))
                        {
                            _albionDataParser.InjectParsedResponse(payload.Code, parameters);
                        }
                        
                        Nightwatch.UIConsole.Log($"[PacketEngine] Injected Fake {payload.Type} Code {payload.Code}", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[PacketEngine] Failed to parse injected packet: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                client.Close();
            }
        }

        private Dictionary<byte, object> ConvertJObjectToDictionary(JObject jObject)
        {
            var dict = new Dictionary<byte, object>();
            if (jObject == null) return dict;

            foreach (var kvp in jObject)
            {
                if (byte.TryParse(kvp.Key, out byte key))
                {
                    dict[key] = ParseJToken(kvp.Value);
                }
            }
            return dict;
        }

        private object ParseJToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            return token.Type switch
            {
                JTokenType.Object => ConvertJObjectToDictionary((JObject)token),
                JTokenType.Array => token.Select(ParseJToken).ToArray(),
                JTokenType.Integer => token.ToObject<long>(),
                JTokenType.Float => token.ToObject<float>(),
                JTokenType.Boolean => token.ToObject<bool>(),
                JTokenType.String => token.ToObject<string>(),
                _ => token.ToString()
            };
        }

        // --- YENİ: ADAPTÖR KARŞILAŞTIRMA ---
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

                // Eğer farklıysa, güncelle
                if (savedAdapterDesc != currentDescription)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Adaptör değiştirildi!");
                    Console.WriteLine($"    Eski: {(string.IsNullOrEmpty(savedAdapterDesc) ? "Kayıt yok" : savedAdapterDesc)}");
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
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Adaptör güncelleme hatası: {ex.Message}");
                Console.ResetColor();
            }
        }



        // --- PacketEngine.cs içindeki Start() metodunu bununla değiştirin ---
        public void Start()
        {
            CaptureDeviceList.Instance.Refresh();
            var devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                Nightwatch.UIConsole.Log(Lang.Get("Err_NoAdapter"), Nightwatch.LogLevel.Error);
                return;
            }

            // Config/last_adapter.txt dosyasından kayıtlı adaptörü oku
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            string adapterMemPath = Path.Combine(configDir, "last_adapter.txt");
            string savedAdapter = File.Exists(adapterMemPath) ? File.ReadAllText(adapterMemPath).Trim() : "";

            string[] virtualKeywords = { "loopback", "npcap", "vmware", "hyper-v", "vbox", "tap", "wan miniport", "wsl", "pseudo" };
            int opened = 0;
            bool customAdapterOpened = false;

            // 1. Eğer kullanıcı ayarlardan özel bir adaptör seçmişse SADECE ONU dinle
            if (!string.IsNullOrEmpty(savedAdapter))
            {
                foreach (var dev in devices)
                {
                    if (dev.Description == savedAdapter)
                    {
                        try
                        {
                            dev.OnPacketArrival += PacketHandler;
                            dev.Open(DeviceModes.None, 20);
                            dev.Filter = "udp";
                            dev.StartCapture();
                            _openedDevices.Add(dev);
                            if (_device == null) _device = dev;
                            opened++;
                            customAdapterOpened = true;
                            Nightwatch.UIConsole.Log($"[Device] Kayıtlı özel adaptör aktif: {dev.Description}", LogLevel.Info);
                            break;
                        }
                        catch (Exception ex) { Nightwatch.UIConsole.Log($"Adaptör açılamadı: {ex.Message}", Nightwatch.LogLevel.Error); }
                    }
                }
            }

            // 2. Özel adaptör yoksa veya hata verdiyse eski otomatik tarama sistemini çalıştır
            if (!customAdapterOpened)
            {
                foreach (var dev in devices)
                {
                    string desc = (dev.Description ?? "").ToLowerInvariant();
                    if (virtualKeywords.Any(k => desc.Contains(k))) continue;

                    try
                    {
                        dev.OnPacketArrival += PacketHandler;
                        dev.Open(DeviceModes.None, 20);
                        dev.Filter = "udp";
                        dev.StartCapture();
                        _openedDevices.Add(dev);
                        if (_device == null) _device = dev;
                        opened++;
                    }
                    catch (Exception ex)
                    {
                        if (_device == null) Nightwatch.UIConsole.Log($"[PacketEngine] Warning: Could not open {desc} - {ex.Message}", LogLevel.Warning);
                    }
                }
            }
            Nightwatch.UIConsole.Log($"PacketEngine aktif. Dinlenen adaptör sayısı: {opened}", LogLevel.Info);
        }

        // --- Sınıfın içine (Start'ın altına) bu yardımcıları ekle ---
        public static List<string> GetAvailableAdapters()
        {
            CaptureDeviceList.Instance.Refresh();
            return CaptureDeviceList.Instance.Select(d => d.Description).ToList();
        }

        public static void SaveSelectedAdapter(string desc)
        {
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "last_adapter.txt"), desc);
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

                string adapterName = ((ICaptureDevice)sender).Description ?? string.Empty;
                bool photonLike = packet.SourcePort == 5056 || packet.DestinationPort == 5056;

                // A19: paket başına TEK rapor (eskiden sayaçlar iki kez artıyordu)
                UdpPortInspector.ReportTraffic(packet.SourcePort, photonLike, adapterName);
                if (packet.DestinationPort != packet.SourcePort)
                    UdpPortInspector.ReportTraffic(packet.DestinationPort, photonLike, adapterName);

                if (photonLike)
                {
                    _packetQueue.Add(packet.PayloadData);
                }
            }
            catch (Exception ex)
            {
                Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
            }
        }

        // --- AĞ TANILAMA ARACI (HANGİ KARTTAN VERİ AKIYOR BULMA) ---
        public static Dictionary<string, bool> TestAllAdaptersForAlbion()
        {
            var results = new Dictionary<string, bool>();
            try
            {
                CaptureDeviceList.Instance.Refresh();
                var devices = CaptureDeviceList.Instance;

                var lockObj = new object();
                var openedDevices = new List<ICaptureDevice>();

                foreach (var dev in devices)
                {
                    string devName = dev.Description ?? dev.Name;
                    results[devName] = false; // Varsayılan olarak hepsine NO veriyoruz

                    try
                    {
                        dev.Open(DeviceModes.None, 20);
                        // Sadece Albion'un kullandığı portları filtrele
                        dev.Filter = "udp port 5056 or udp port 5057 or udp port 5055";

                        PacketArrivalEventHandler handler = (sender, e) =>
                        {
                            lock (lockObj)
                            {
                                var d = (ICaptureDevice)sender;
                                results[d.Description ?? d.Name] = true; // Albion paketi geldiyse YES yap
                            }
                        };

                        dev.OnPacketArrival += handler;
                        dev.StartCapture();
                        openedDevices.Add(dev);
                    }
                    catch (Exception ex)
                    {
                        // Desteklenmeyen (bozuk) sanal kartlar hata verirse konsola debug olarak yaz
                        Console.WriteLine($"[TestAllAdapters] Adapter open failed for {devName}: {ex.Message}");
                    }
                }

                // 3 Saniye boyunca kullanıcıdan hareket etmesini bekle ve paketleri topla
                Thread.Sleep(3000);

                // İşlem bitince tüm ajanları geri çek ve kartları kapat
                foreach (var dev in openedDevices)
                {
                    try
                    {
                        dev.StopCapture();
                        dev.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TestAllAdapters] Adapter close failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test Hatası: {ex.Message}");
            }

            return results;
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
                    Nightwatch.UIConsole.Log($"[HATA] Error Code: " + ex.Message, Nightwatch.LogLevel.Error);
                }
            }

            _openedDevices.Clear();
        }
    }

    public class FakePacketPayload
    {
        public string Type { get; set; } // "Event", "Request", "Response"
        public byte Code { get; set; }
        public JObject Parameters { get; set; }
    }
}






