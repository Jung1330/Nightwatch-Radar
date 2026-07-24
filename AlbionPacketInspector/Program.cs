using System;
using System.Threading.Tasks;

namespace AlbionPacketInspector
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("Albion Packet Inspector (ImGui)");
            Console.WriteLine("=======================================");
            Console.WriteLine("Starting network sniffer on port 5056...");
            Console.WriteLine("Launching ImGui Overlay window...");
            Console.WriteLine("Close the ImGui window to exit.");
            Console.WriteLine("=======================================");

            using var overlay = new PacketInspectorOverlay();
            await overlay.Run();
            
            Console.WriteLine("Exiting Packet Inspector...");
        }
    }
}
