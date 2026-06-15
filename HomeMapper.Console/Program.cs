using SharpPcap;
using Spectre.Console;

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

// Making a change to force git to track it