using SharpPcap;
using Spectre.Console;
using System.Net;
using System.Net.NetworkInformation;

AnsiConsole.MarkupLine("[bold green]HomeMapper Console Application[/]");
AnsiConsole.MarkupLine("Searching for devices....");

var devices = CaptureDeviceList.Instance;

foreach(var device in devices)
{
    AnsiConsole.MarkupLine($"[blue]{device.Name}[/] - [green]{device.Description}[/]");
}

AnsiConsole.MarkupLine("");
AnsiConsole.Markup("[green]Scan complete.[/]");
Console.ReadLine();

AnsiConsole.MarkupLine("Scanning Network addresses now");

var networkDevices = NetworkInterface.GetAllNetworkInterfaces();
foreach(var device in networkDevices)
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
            }
        }
    }
}

// Making a change to force git to track it