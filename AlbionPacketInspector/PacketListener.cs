using System;
using System.Collections.Generic;
using System.Threading;
using SharpPcap;
using PacketDotNet;
using AlbionDataHandlers;

namespace AlbionPacketInspector
{
    public class PacketListener : IDisposable
    {
        private readonly AlbionDataParser _parser;
        private readonly List<ILiveDevice> _devicesOpened = new();
        private Thread? _captureThread;
        private bool _isRunning;

        public int OpenedDevicesCount => _devicesOpened.Count;

        public PacketListener(AlbionDataParser parser)
        {
            _parser = parser;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _captureThread = new Thread(StartCaptureLoop)
            {
                IsBackground = true,
                Name = "AlbionPacketSniffer"
            };
            _captureThread.Start();
        }

        private void StartCaptureLoop()
        {
            try
            {
                CaptureDeviceList.Instance.Refresh();
                var allDevices = CaptureDeviceList.Instance;
                if (allDevices.Count < 1)
                {
                    Console.WriteLine("[Sniffer] No network devices found.");
                    return;
                }

                foreach (ILiveDevice device in allDevices)
                {
                    if (string.IsNullOrEmpty(device.Description)) continue;

                    string desc = device.Description.ToLowerInvariant();
                    // Exclude pseudo-devices (like loopbacks or special raw interfaces)
                    if (desc.Contains("pseudo-device"))
                    {
                        continue;
                    }

                    try
                    {
                        Console.WriteLine($"[Sniffer] Opening device: {device.Description}");
                        device.OnPacketArrival += OnPacketArrival;
                        device.Open(DeviceModes.Promiscuous, 1);
                        device.StartCapture();
                        _devicesOpened.Add(device);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Sniffer] Failed to open device {device.Description}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sniffer] Sniffing loop exception: {ex.Message}");
            }
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            if (!_isRunning) return;

            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var udpPacket = packet.Extract<UdpPacket>();
                
                if (udpPacket != null)
                {
                    // Albion Online game client / server uses port 5056 UDP for game events
                    if (udpPacket.SourcePort == 5056 || udpPacket.DestinationPort == 5056)
                    {
                        Console.WriteLine($"[Listener] Sniffed UDP Port 5056 Packet: {udpPacket.PayloadData.Length} bytes");
                        _parser.ReceivePacket(udpPacket.PayloadData);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors on corrupt packets
            }
        }

        public void Stop()
        {
            _isRunning = false;
            foreach (var device in _devicesOpened)
            {
                try
                {
                    if (device.Started)
                    {
                        device.StopCapture();
                    }
                    device.OnPacketArrival -= OnPacketArrival;
                    device.Close();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
            _devicesOpened.Clear();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
