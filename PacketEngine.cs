using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
    // --- GÜNCELLENMÝÞ HANDLER ---
    public class InternalMapHandler : IEventHandler
    {
        private readonly Action _onJoinFinished; // Sadece JoinFinished tetiklenecek

        public InternalMapHandler(Action onJoinFinished)
        {
            _onJoinFinished = onJoinFinished;
        }

        public void OnEvent(EventCodes code, Dictionary<byte, object> parameters)
        {
            // ÝPTAL EDÝLDÝ: EventCodes.Leave (1) yanýmýzdan biri ayrýlýnca da tetikleniyor.
            // Bu yüzden sürekli ekraný siliyordu. Bunu kaldýrdýk.
            /*
            if (code == EventCodes.Leave)
            {
                
            }
            */

            // Sadece Harita Yüklenmesi Bittiðinde (JoinFinished) çalýþ
            if (code == EventCodes.JoinFinished)
            {
                UIConsole.Log($"[InternalMapHandler] JoinFinished (Map Loaded) - Cleanup Starting...", LogLevel.Info);
                _onJoinFinished?.Invoke();
            }
        }

        public void OnRequest(RequestCodes code, Dictionary<byte, object> parameters) { }
        public void OnResponse(ResponseCodes code, Dictionary<byte, object> parameters) { }
    }

    public class PacketEngine
    {
        private readonly AlbionDataParser _albionDataParser;
        private ICaptureDevice _device;

        // Kuyruk Sistemi (limitsiz)
        private readonly BlockingCollection<byte[]> _packetQueue = new BlockingCollection<byte[]>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // --- Port kontrol için flag ---
        private bool _port5056Detected = false;
        private DateTime _lastPacketParserErrorAt = DateTime.MinValue;

        public PacketEngine(AlbionDataParser albionDataParser, GameStateManager gameStateManager)
        {
            _albionDataParser = albionDataParser;

            // Handler'ý oluþturuyoruz
            var mapHandler = new InternalMapHandler(() =>
            {
                // 1. Önce eski mapten kalan ve sýrada bekleyen paketleri ÇÖPE AT
                PurgeQueue();

                // 2. Sonra ekraný temizle
                /* gameStateManager.ClearAllData(); */
            });

            _albionDataParser.RegisterEventHandler(mapHandler);

            // Ýþçiyi baþlat
            Task.Factory.StartNew(ProcessQueue, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
                    _albionDataParser.ReceivePacket(payload);
                }
                catch (Exception ex)
                {
                    if ((DateTime.Now - _lastPacketParserErrorAt).TotalSeconds >= 2)
                    {
                        _lastPacketParserErrorAt = DateTime.Now;
                        Console.WriteLine($"Error Code : 15 | {ex.Message}");
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

        // --- YENÝ: PORT 5056 KONTROLÜ ---
        private bool ValidatePort5056(ICaptureDevice device, int timeoutSeconds = 10)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? Port 5056 kontrol ediliyor ({timeoutSeconds} saniye bekleniyor)...");
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

                        if (packet != null && (packet.SourcePort == 5056 || packet.DestinationPort == 5056))
                        {
                            if (!_port5056Detected)
                            {
                                _port5056Detected = true;

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Port 5056 aktif! Albion trafiði algýlandý!");
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
                device.Open(DeviceModes.MaxResponsiveness, 1000);
                device.Filter = "udp port 5056";
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
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? UYARI: Port 5056'dan paket gelmedi!");
                    Console.WriteLine("   Lütfen kontrol et:");
                    Console.WriteLine("   1. Albion Online çalýþýyor mu?");
                    Console.WriteLine("   2. Doðru adaptörü seçtin mi?");
                    Console.WriteLine("   3. Firewall port 5056'ý engelliyor mu?");
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

        public void Start()
        {
            var devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(Lang.Get("Err_NoAdapter"));
                Console.ResetColor();
                return;
            }

            // --- 1. HAFIZA (CACHE) KONTROLÜ ---
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            string adapterMemPath = Path.Combine(configDir, "last_adapter.txt");

            if (File.Exists(adapterMemPath))
            {
                try
                {
                    string savedAdapterDesc = File.ReadAllText(adapterMemPath).Trim();
                    // Hafýzadaki isimle eþleþen cihazý bul
                    var memoryDevice = devices.FirstOrDefault(d => d.Description == savedAdapterDesc || d.Name == savedAdapterDesc);

                    if (memoryDevice != null)
                    {

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(string.Format(Lang.Get("AdapterLoaded"), memoryDevice.Description));

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine(Lang.Get("AdapterHint"));
                        Console.ResetColor();

                        // --- PORT 5056 KONTROLÜ YAP ---
                        if (!ValidatePort5056(memoryDevice, 10))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Port kontrolü baþarýsýz, 'Ambush' moduna geçiliyor...");
                            Console.ResetColor();
                            // Ambush moduna geç
                        }
                        else
                        {
                            _device = memoryDevice;
                            _device.OnPacketArrival += PacketHandler;
                            _device.Open(DeviceModes.MaxResponsiveness, 1000);
                            _device.Filter = "udp port 5056"; // CPU Optimizasyonu
                            _device.StartCapture();
                            return; // Baþarýlý, çýk!
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 20 | {ex.Message}");
                }
            }

            // --- 2. HAFIZADA YOKSA "PUSU (AMBUSH)" MODUNA GEÇ ---
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Lang.Get("AdapterSearch"));
            Console.WriteLine(Lang.Get("AdapterWait"));
            Console.ResetColor();

            ICaptureDevice selectedDevice = null;
            var resetEvent = new ManualResetEventSlim(false);

            PacketArrivalEventHandler tempHandler = (sender, e) =>
            {
                try
                {
                    var rawCapture = e.GetPacket();
                    var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data).Extract<UdpPacket>();

                    if (packet != null && (packet.SourcePort == 5056 || packet.DestinationPort == 5056))
                    {
                        if (selectedDevice == null)
                        {
                            selectedDevice = (ICaptureDevice)sender;
                            resetEvent.Set();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 21 | {ex.Message}");
                }
            };

            // Bütün adaptörlere pusu kur
            foreach (var dev in devices)
            {
                try
                {
                    dev.OnPacketArrival += tempHandler;
                    dev.Open(DeviceModes.MaxResponsiveness, 1000);
                    dev.Filter = "udp port 5056";
                    dev.StartCapture();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 22 | {ex.Message}");
                }
            }

            bool gotPacket = resetEvent.Wait(TimeSpan.FromSeconds(20)); // Albion verisi için en fazla 20 sn bekle

            // Diðer adaptörleri temizle
            foreach (var dev in devices)
            {
                try
                {
                    dev.StopCapture();
                    dev.OnPacketArrival -= tempHandler;
                    dev.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 50 | {ex.Message}");
                }
            }

            // --- 3. KAZANAN ADAPTÖRÜ BAÐLA VE HAFIZAYA YAZ ---
            if (!gotPacket || selectedDevice == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Albion trafiði algýlanamadý. Adaptör seçimi baþarýsýz.");
                Console.ResetColor();
                return;
            }

            if (selectedDevice != null)
            {
                // ADAPTÖR DEÐIÞIM KONTROLÜ YAP
                UpdateAdapterIfChanged(selectedDevice, adapterMemPath);

                _device = selectedDevice;
                _device.OnPacketArrival += PacketHandler;
                _device.Open(DeviceModes.MaxResponsiveness, 1000);
                _device.Filter = "udp port 5056";
                _device.StartCapture();

                Console.Write(Lang.Get("AdapterHook"));
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(_device.Description);
                Console.ResetColor();
            }
        }
        private void PacketHandler(object sender, PacketCapture e)
        {
            try
            {
                RawCapture rawCapture = e.GetPacket();
                var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data).Extract<UdpPacket>();

                if (packet != null && (packet.SourcePort == 5056 || packet.DestinationPort == 5056))
                {
                    // Paketi kuyruða at
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
            if (_device != null)
            {
                try
                {
                    _device.StopCapture();
                    _device.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Code : 24 | {ex.Message}");
                }
            }
        }
    }
}


