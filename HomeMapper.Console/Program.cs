using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

AnsiConsole.MarkupLine("[bold green]HomeMapper Console Application[/]");
AnsiConsole.MarkupLine("Searching for devices....");

// This loop displays all of the network cards found ON MY COMPUTER. These are NOT devices that are ssimply connected to the network
// CaptureDeviceList.Instance returns a list of all the network devices on the computer, not the devices connected to the network.
// To get the devices connected to the network, you need to use a different method, such as ARP scanning or ping sweeping.
var localDevices = CaptureDeviceList.Instance;
foreach (var device in localDevices)
{
    AnsiConsole.MarkupLine($"[blue]{device.Name}[/] - [green]{device.Description}[/]");
}

AnsiConsole.MarkupLine("");
AnsiConsole.Markup("[green]Local Network Device Scan complete.[/]");
Console.ReadLine();

AnsiConsole.MarkupLine("Scanning Network addresses now");

// This filters down those network card/devices on my computer, and filters it down to just what my computer is actually using
// This gets the IP address and the subnet mask
var networkDevices = NetworkInterface.GetAllNetworkInterfaces();
NetworkInterface myNetworkCard = null;

foreach (var networkDevice in networkDevices)
{
   if(networkDevice.OperationalStatus == OperationalStatus.Up && 
        networkDevice.NetworkInterfaceType != NetworkInterfaceType.Loopback && 
        (networkDevice.NetworkInterfaceType == NetworkInterfaceType.Ethernet || networkDevice.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
    {
        foreach(var address in networkDevice.GetIPProperties().UnicastAddresses)
        {
            if(address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var network = IPNetwork2.Parse(address.Address, address.IPv4Mask);
                var deviceAddresses = network.ListIPAddress(Filter.Usable);
                AnsiConsole.MarkupLine($"[blue]{networkDevice.Name}[/] - [green]{address.Address}[/] - [yellow]{address.IPv4Mask}[/]");
                myNetworkCard = networkDevice;


                break;
            }
        }
    }

    // Once we find a network card that we can use, break out of the loop. We will use the first one we find.
    if (myNetworkCard != null)
    {
        break;
    }
}

// We will use the first NetworkInterface device to ping the networks and create the listener and broadcasting threads
IPAddress iPAddress = null;
IPAddress mask = null;
PhysicalAddress physicalAddress = null;
if (myNetworkCard != null)
{
    var result = GetNetworkInfo(myNetworkCard);
    if (result.HasValue)
    {
        iPAddress = result.Value.ipAddress;
        mask = result.Value.mask;
        physicalAddress = result.Value.physicalAddress;
    }
}
else
{
    AnsiConsole.MarkupLine("[red]No network card found that is up and not a loopback device.[/]");
    return;
}

// Being the ping sweep of the network to find devices on the network. This will take a while, so we will run it in a separate thread.

// Find the device with the matching MAC address of the network device
LibPcapLiveDevice matchingDevice = null;

foreach (var localDevice in localDevices)
{
    var deviceMacAddress = localDevice.MacAddress;
    if (deviceMacAddress != null && deviceMacAddress.Equals(physicalAddress))
    {
        AnsiConsole.MarkupLine($"[blue]{localDevice.Name}[/] - [green]{localDevice.Description}[/] - [yellow]{deviceMacAddress}[/]");
        matchingDevice = localDevice as LibPcapLiveDevice;
        break;
    }
}

if(matchingDevice == null)
{
    AnsiConsole.MarkupLine("[red]Unexpected error finding the network device.");
    return;
}

matchingDevice.Open(DeviceModes.Promiscuous);
matchingDevice.Filter = "arp";

// Start a new thread to listen for ARP packets
var discoveredDevices = new ConcurrentDictionary<string, IPAddress>();
matchingDevice.OnPacketArrival += (sender, e) => PacketArrivalEventHandler(sender, e, discoveredDevices); // Register the event handler outside the thread. If registered INSIDE the thread, there is a chance a couple of packets sneak through
var listenerThread = new Thread(() =>
{
    matchingDevice.StartCapture();
});

// Build the sender thread
var senderThread = new Thread(() =>
{
    // Build the list of IP addresses to ping based on the network card's IP address and subnet mask
    var network = IPNetwork2.Parse(iPAddress, mask); // Gets the network address and subnet mask from the IP address and subnet mask of the network card
    var targets = network.ListIPAddress(Filter.Usable); // Gets the list of usable IP addresses on the network

    Thread.Sleep(500); // Give the Listener a chance to start

    // You need three things to build the packet:
    // 1. Your own MAC address (the sender hardware address)
    // 2. Your own IP address (the sender protocol address)  
    // 3. The target IP you're asking about

    foreach (var target in targets)
    {
        var ethernetPacket = new EthernetPacket(
                    physicalAddress,                          // your MAC
                    PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF"),  // broadcast
                    EthernetType.Arp
                    );

        var arpPacket = new ArpPacket(
            ArpOperation.Request,
            PhysicalAddress.Parse("00-00-00-00-00-00"),  // target MAC unknown (that's the point)
            target.MapToIPv4(),                           // the IP we're asking about
            physicalAddress,                          // your MAC
            iPAddress                            // your IP
        );

        ethernetPacket.PayloadPacket = arpPacket;
        matchingDevice.SendPacket(ethernetPacket);

        Thread.Sleep(5);
    }  
});

AnsiConsole.MarkupLine("Sending ARP requests to all devices on the network...");
Stopwatch stopwatch = Stopwatch.StartNew();
stopwatch.Start();

listenerThread.Start();
senderThread.Start();

senderThread.Join();
Thread.Sleep(1500);
matchingDevice.StopCapture();
matchingDevice.Close();

stopwatch.Stop();

AnsiConsole.MarkupLine($"[green]ARP requests completed in {stopwatch.ElapsedMilliseconds} ms");

foreach (var device in discoveredDevices)
{
    AnsiConsole.MarkupLine($"[blue]{device.Key}[/] - [green]{device.Value}[/]");
}

// --- Helper Methods ---
static (IPAddress ipAddress, IPAddress mask, PhysicalAddress physicalAddress)? GetNetworkInfo(NetworkInterface networkInterface)
    {
        IPAddress ipAddress = null;
        IPAddress mask = null;
        PhysicalAddress physicalAddress = networkInterface.GetPhysicalAddress();

        foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
        {
            if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                ipAddress = address.Address;
                mask = address.IPv4Mask;
                break;
            }
        }

        return (ipAddress, mask, physicalAddress);
    }

/// Parse the packet into usable information and display it to the console. This is the event handler for when a packet is captured.
static void PacketArrivalEventHandler(object sender, PacketCapture e, ConcurrentDictionary<string, IPAddress> discoveredDevices)
{
    // A raw Packet is a lot like an onion - in order to get to certain portions of the packet, you need to peel away the layers.
    // The first layer is the Ethernet layer, which contains the MAC addresses and the type of packet (ARP, IP, etc.).
    // The second layer is the ARP layer, which contains the IP addresses and the operation (Request or Response).

    // Get the raw captured data packet
    var rawCapture = e.GetPacket();

    // Parse the raw data packet into a typed packet
    Packet genericPacket = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);

    // Extract the Ethernet layer
    var ethernetPacket = genericPacket.Extract<EthernetPacket>();
    if(ethernetPacket != null)
    {
        if (ethernetPacket.Type == EthernetType.Arp)
        {
            var arpPacket = ethernetPacket.Extract<ArpPacket>();
            if (arpPacket != null)
            {
                if (arpPacket.Operation == ArpOperation.Response)
                {
                    // NOTE: This is an ARP REPLY. This means that my computer is THE TARGET DEVICE
                    //       And the SENDER is the device that is responding to my computer's ARP REQUEST.

                    string formattedMac = string.Join(":", arpPacket.SenderHardwareAddress.ToString().Chunk(2).Select(c => new string(c)));
                    discoveredDevices.TryAdd(formattedMac, arpPacket.SenderProtocolAddress);
                }
            }
        }
    }
}