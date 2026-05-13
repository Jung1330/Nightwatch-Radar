using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AOSnifferNET
{
    class PacketReciever
    {
        readonly PacketHandler photonParser;
        readonly Thread photonThread;
        readonly string localIp;
        bool serverDetected = false;
        private int _lastPhotonPort = 0;

        public PacketReciever()
        {
            this.photonParser = new PacketHandler();
            this.localIp = GetLocalIPAddress();

            try
            {
                this.photonThread = new Thread(() => this.CreateListener()) { };
                this.photonThread.Start();
            }
            catch (Exception ea)
            {
                Console.WriteLine(ea.ToString());
            }
        }

        // Detecta la IP local de la VM
        private string GetLocalIPAddress()
        {
            string localIP = "";
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(localIP))
                throw new Exception("No se pudo determinar la IP local.");

            Console.WriteLine($"Local IP detected: {localIP}");
            return localIP;
        }

        private void CreateListener()
        {
            List<ILiveDevice> devicesOpened = new List<ILiveDevice>();
            Console.WriteLine("Start Listening for Devices...");

            bool isLinux = File.Exists("/proc/sys/kernel/ostype");
            string[] virtualKeywords =
            {
                "loopback",
                "npcap",
                "vmware",
                "hyper-v",
                "vbox",
                "tap",
                "wan miniport",
                "wsl",
                "pseudo"
            };

            bool running = true;
            while (running)
            {
                try
                {
                    CaptureDeviceList.Instance.Refresh();
                    var allDevices = CaptureDeviceList.Instance;
                    if (allDevices.Count < 1)
                    {
                        throw new Exception("No interfaces found! Make sure NPcap is installed.");
                    }

                    foreach (ILiveDevice deviceSelected in allDevices)
                    {
                        if (string.IsNullOrEmpty(deviceSelected.Description))
                            continue;

                        string desc = deviceSelected.Description.ToLowerInvariant();
                        // Excluir adaptadores virtuales
                        if (!isLinux)
                        {
                            bool isVirtual = virtualKeywords.Any(k => desc.Contains(k));
                            if (isVirtual)
                                continue;
                        }
                        else
                        {
                            if (!desc.Contains("pseudo-device"))
                                continue;
                        }


                        if (devicesOpened.Contains(deviceSelected))
                            continue;

                        Console.WriteLine($"Open physical device: {deviceSelected.Description}");

                        deviceSelected.OnPacketArrival += PacketHandler;
                        deviceSelected.Open(DeviceModes.Promiscuous, 1);
                        deviceSelected.StartCapture();
                        devicesOpened.Add(deviceSelected);
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error while listening for devices: {e.Message}");
                }

                Console.CancelKeyPress += (sender, e) =>
                {
                    Console.WriteLine("Ctrl+C or Ctrl+Break has been pressed. Performing closing tasks...");
                    StopDevices(devicesOpened);
                    running = false;
                    e.Cancel = true;
                };

                AppDomain.CurrentDomain.DomainUnload += (sender, e) =>
                {
                    Console.WriteLine("Managing SIGTERM. Performing closing tasks...");
                    StopDevices(devicesOpened);
                    running = false;
                    Console.WriteLine("Closure completed.");
                };
            }
        }

        private void StopDevices(List<ILiveDevice> devicesOpened)
        {
            foreach (ILiveDevice device in devicesOpened)
            {
                if (device != null && device.Started)
                {
                    device.StopCapture();
                    device.OnPacketArrival -= this.PacketHandler;
                    device.Close();
                }
            }
        }

        private void PacketHandler(object sender, PacketCapture e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
                UdpPacket udp_packet = packet.Extract<UdpPacket>();

                if (udp_packet != null)
                {
                    if (packet.PayloadPacket is IPv4Packet ip_packet)
                    {
                        // --- O BÝZÝ KÖR EDEN VE VERÝYÝ ÇÖPE ATAN IP KONTROLÜNÜ KALDIRDIK! ---
                        // Artýk sanal IP (Hyper-V) falan umrumuzda deðil, paket geçiyorsa yakalýyoruz.

                        // Detectar región del servidor una sola vez
                        if (!serverDetected)
                        {
                            string gameServerIP = ip_packet.SourceAddress.ToString() == this.localIp
                                ? ip_packet.DestinationAddress.ToString()
                                : ip_packet.SourceAddress.ToString();

                            if (gameServerIP.StartsWith("5.188.125."))
                            {
                                Console.WriteLine("[ServerRegion][{\"server\":\"America\"}]");
                                serverDetected = true;
                            }
                            else if (gameServerIP.StartsWith("5.45.187."))
                            {
                                Console.WriteLine("[ServerRegion][{\"server\":\"Asia\"}]");
                                serverDetected = true;
                            }
                            else if (gameServerIP.StartsWith("193.169.238."))
                            {
                                Console.WriteLine("[ServerRegion][{\"server\":\"Europe\"}]");
                                serverDetected = true;
                            }
                        }
                    }
                    // Puerto 5056 (Albion Asýl Portu)
                    if (udp_packet.SourcePort == 5056 || udp_packet.DestinationPort == 5056)
                    {
                        this.photonParser.ReceivePacket(udp_packet.PayloadData);
                    }
                    // Puerto 5055 (Albion Login Portu)
                    else if (udp_packet.SourcePort == 5055 || udp_packet.DestinationPort == 5055)
                    {
                        if (packet.PayloadPacket is IPv4Packet ip_pkt)
                        {
                            if (ip_pkt.SourceAddress.ToString() == "5.188.125.60" ||
                                ip_pkt.SourceAddress.ToString() == "5.45.187.118")
                            {
                                var output = new StreamWriter(Console.OpenStandardOutput());
                                output.WriteLine("[onLogin][{\"status\":\"New Packet\"}]");
                                output.Flush();
                            }
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }
    }
}


