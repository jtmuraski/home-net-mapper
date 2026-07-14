using PacketDotNet;
using SharpPcap;
using Spectre.Console;
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
ICaptureDevice matchingDevice = null;
foreach (var localDevice in localDevices)
{
    var deviceMacAddress = localDevice.MacAddress;
    if (deviceMacAddress != null && deviceMacAddress.Equals(physicalAddress))
    {
        AnsiConsole.MarkupLine($"[blue]{localDevice.Name}[/] - [green]{localDevice.Description}[/] - [yellow]{deviceMacAddress}[/]");
        matchingDevice = localDevice;
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
matchingDevice.OnPacketArrival += new PacketArrivalEventHandler(PacketArrivalEventHandler); // Register the event handler outside the thread. If registered INSIDE the thread, there is a chance a couple of packets sneak through
var listenerThread = new Thread(() =>
{
    matchingDevice.StartCapture();
});

// Build the sender thread
var senderThread = new Thread(() =>
{
    var network = IPNetwork2.Parse(iPAddress, mask);
    var targets = network.Up
    Thread.Sleep(500); // Give the Listener a chance to start

    // You need three things to build the packet:
    // 1. Your own MAC address (the sender hardware address)
    // 2. Your own IP address (the sender protocol address)  
    // 3. The target IP you're asking about

    var ethernetPacket = new EthernetPacket(
        physicalAddress,                          // your MAC
        PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF"),  // broadcast
        EthernetType.Arp
    );

    var arpPacket = new ArpPacket(
        ArpOperation.Request,
        PhysicalAddress.Parse("00-00-00-00-00-00"),  // target MAC unknown (that's the point)
        targetIp,                           // the IP we're asking about
        physicalAddress,                          // your MAC
        iPAddress                            // your IP
    );

    ethernetPacket.PayloadPacket = arpPacket;

    device.SendPacket(ethernetPacket);
});

listenerThread.Start();
senderThread.Start();

senderThread.Join();
Thread.Sleep(1500);
matchingDevice.StopCapture();

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

static void PacketArrivalEventHandler(object sender, PacketCapture e)
{
    var time = e.Header.Timeval.Date;
    var len = e.Data.Length;
    var rawPacket = e.GetPacket();
    AnsiConsole.WriteLine("{0}:{1}:{2},{3} Len={4}",
        time.Hour, time.Minute, time.Second, time.Millisecond, len);
    AnsiConsole.WriteLine(rawPacket.ToString());
}