using BlueMidiRelay;
using CommandLine;

var parsedArgs = Parser.Default
    .ParseArguments<ScanOptions, MonitorOptions, ListMidiOptions, ForwardOptions>(args);
await parsedArgs.WithParsedAsync(async (ScanOptions opts) =>
    {
        var scanner = new Scanner(opts.ShowNonMidi);
        await scanner.Run((int)(opts.Timeout * 1000));
    });
await parsedArgs.WithParsedAsync(async (MonitorOptions opts) =>
{
    if (opts.Address.Length != 12 || !opts.Address.All(IsHexDigit))
    {
        Console.WriteLine("Error: malformed device address");
        return;
    }
    var address = ulong.Parse(opts.Address, System.Globalization.NumberStyles.HexNumber);
    var midiRelay = new MidiRelay(address);
    midiRelay.MessageReceived += (_, e) =>
    {
        if (e.Status >> 5 == 4)
        {
            var channel = e.Status & 0xF;
            var action = (e.Status & 0x10) != 0 ? "on" : "off";
            Console.WriteLine($"Channel {channel} note {action}, note {e.Data0}, velocity {e.Data1}");
        }
        else
        {
            Console.WriteLine($"Unknown MIDI event {e.Status:#x} received");
        }
    };
    if (await midiRelay.Connect())
    {
        Console.WriteLine("Device connected");
        await Task.Delay(Timeout.Infinite);
    }
    else
    {
        Console.WriteLine("Error: cannot connect to device.");
    }
});
parsedArgs.WithParsed((ListMidiOptions opts) => MidiDevice.ListAll());
await parsedArgs.WithParsedAsync(async (ForwardOptions opts) =>
{
    if (opts.Source.Length != 12 || !opts.Source.All(IsHexDigit))
    {
        Console.WriteLine("Error: malformed device address");
        return;
    }
    var address = ulong.Parse(opts.Source, System.Globalization.NumberStyles.HexNumber);

    var midiOut = MidiDevice.FindMidiOutByName(opts.Destination);
    if (midiOut == null)
    {
        Console.WriteLine("Error: cannot find MIDI device " + opts.Destination);
        return;
    }
    var midiRelay = new MidiRelay(address);
    midiRelay.MessageReceived += (_, e) =>
    {
        MidiDevice.SendMessageTo(e, midiOut);
    };
    if (!await midiRelay.Connect())
    {
        Console.WriteLine("Error: cannot connect to bluetooth MIDI device.");
        return;
    }
    Console.WriteLine("Devices connected, forwarding MIDI messages...");

    await Task.Delay(Timeout.Infinite);
});

bool IsHexDigit(char c)
{
    return (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'f')
        || (c >= 'A' && c <= 'F');
}

[Verb("scan", HelpText = "Scan for bluetooth MIDI devices")]
class ScanOptions
{
    [Option('t', "timeout", Default = 15u, HelpText = "The number of seconds that the scanner runs")]
    public uint Timeout { get; set; }

    [Option("show-non-midi", HelpText = "Also show non-MIDI bluetooth devices")]
    public bool ShowNonMidi { get; set; }
}

[Verb("monitor", HelpText = "Monitor input from a bluetooth MIDI device")]
class MonitorOptions
{
    [Option('a', "address", Required = true, HelpText = "The device's bluetooth address")]
    public string Address { get; set; }
}

[Verb("list-midi", HelpText = "List all local MIDI devices")]
class ListMidiOptions { }

[Verb("forward", HelpText = "Forward bluetooth MIDI input to local output")]
class ForwardOptions
{
    [Option('s', "source", Required = true, HelpText = "The source device's bluetooth address")]
    public string Source { get; set; }

    [Option('d', "dest", Required = true, HelpText = "Name of the destination MIDI output")]
    public string Destination { get; set; }
}
