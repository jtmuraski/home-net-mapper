using SharpPcap;
using Spectre.Console;
using System.Net;
using System.Net.NetworkInformation;

AnsiConsole.MarkupLine("[bold green]HomeMapper Console Application[/]");
AnsiConsole.MarkupLine("Searching for devices....");

var devices = CaptureDeviceList.Instance;

// This loop displays all of the network cards found ON MY COMPUTER. These are NOT devices that are simlpy connected to the network
// CaptureDeviceList.Instance returns a list of all the network devices on the computer, not the devices connected to the network.
// To get the devices connected to the network, you need to use a different method, such as ARP scanning or ping sweeping.
foreach (var device in devices)
{
    AnsiConsole.MarkupLine($"[blue]{device.Name}[/] - [green]{device.Description}[/]");
}

AnsiConsole.MarkupLine("");
AnsiConsole.Markup("[green]Scan complete.[/]");
Console.ReadLine();

AnsiConsole.MarkupLine("Scanning Network addresses now");

// This filters down those network card/devices on my computer, and filters it down to just what my computer is actually using
// This gets the IP address and the subnet mask
var networkDevices = NetworkInterface.GetAllNetworkInterfaces();
PhysicalAddress physicalAddress;
IPAddress ipAddress;
IPAddress mask;

foreach (var device in networkDevices)
{
   if(device.OperationalStatus == OperationalStatus.Up && 
        device.NetworkInterfaceType != NetworkInterfaceType.Loopback && 
        (device.NetworkInterfaceType == NetworkInterfaceType.Ethernet || device.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
    {
        foreach(var address in device.GetIPProperties().UnicastAddresses)
        {
            if(address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var network = IPNetwork2.Parse(address.Address, address.IPv4Mask);
                var deviceAddresses = network.ListIPAddress(Filter.Usable);
                AnsiConsole.MarkupLine($"[blue]{device.Name}[/] - [green]{address.Address}[/] - [yellow]{address.IPv4Mask}[/]");

                physicalAddress = device.GetPhysicalAddress();
                ipAddress = address.Address;
                mask = address.IPv4Mask;

                break;
            }
        }
    }
}

// We will use the first NetworkInterface device to ping the networkd

