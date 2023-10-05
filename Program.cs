using BlueMidiRelay;
using CommandLine;

using var interrupt = new SemaphoreSlim(0, 1);

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
    using var bleMidi = new BleMidiDevice(address);
    bleMidi.MessageReceived += (_, e) =>
    {
        var channel = (e.Status & 0xF) + 1;
        switch (e.Status & 0xF0)
        {
            case 0x80:
            case 0x90:
                // Note on / off
                var action = (e.Status & 0x10) != 0 ? "on" : "off";
                Console.WriteLine($"Channel {channel} note {action}, note {e.Data0}, velocity {e.Data1}");
                break;

            case 0xB0:
                // Control / mode change
                Console.WriteLine($"Channel {channel} control change: {(NAudio.Midi.MidiController)e.Data0} -> {e.Data1}");
                break;

            case 0xE0:
                // Pitch bend change (portamento)
                var pitchDelta = e.Data0 | (e.Data1 << 7);
                var sign = pitchDelta > 0x2000 ? "+" : (pitchDelta == 0x2000 ? " " : "");
                Console.WriteLine($"Channel {channel} pitch bend change: {sign}{pitchDelta / (double)0x1000 - 2:f2} semitones");
                break;

            default:
                Console.WriteLine($"Unknown MIDI event 0x{e.Status:X}");
                break;
        }
    };
    bleMidi.SysexMessageReceived += (_, e) =>
    {
        Console.Write("SysEx message:");
        foreach (var b in e.Data)
            Console.Write($" {b:X2}");
        Console.WriteLine();
    };
    if (await bleMidi.Connect())
    {
        Console.WriteLine("Device connected");
        HookConsoleInterrupt();
        await Task.WhenAny(bleMidi.WaitUntilDisconnect(), interrupt.WaitAsync());
        if (!bleMidi.IsConnected)
            Console.WriteLine("Device disconnected");
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

    using var midiOut = MidiDevice.FindMidiOutByName(opts.Destination);
    if (midiOut == null)
    {
        Console.WriteLine("Error: cannot find MIDI device " + opts.Destination);
        return;
    }
    using var bleMidi = new BleMidiDevice(address);
    bleMidi.MessageReceived += (_, e) =>
    {
        MidiDevice.SendMessageTo(e, midiOut);
    };
    bleMidi.SysexMessageReceived += (_, e) =>
    {
        MidiDevice.SendSysexMessageTo(e, midiOut);
    };
    if (!await bleMidi.Connect())
    {
        Console.WriteLine("Error: cannot connect to bluetooth MIDI device.");
        return;
    }
    Console.WriteLine("Devices connected, forwarding MIDI messages...");

    HookConsoleInterrupt();
    await Task.WhenAny(bleMidi.WaitUntilDisconnect(), interrupt.WaitAsync());
    if (!bleMidi.IsConnected)
        Console.WriteLine("Device disconnected");
});

bool IsHexDigit(char c)
{
    return (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'f')
        || (c >= 'A' && c <= 'F');
}

void HookConsoleInterrupt()
{
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Console.WriteLine("Ctrl+C pressed, stopping process...");
        interrupt?.Release();
    };
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
    public string Address { get; set; } = "";
}

[Verb("list-midi", HelpText = "List all local MIDI devices")]
class ListMidiOptions { }

[Verb("forward", HelpText = "Forward bluetooth MIDI input to local output")]
class ForwardOptions
{
    [Option('s', "source", Required = true, HelpText = "The source device's bluetooth address")]
    public string Source { get; set; } = "";

    [Option('d', "dest", Required = true, HelpText = "Name of the destination MIDI output")]
    public string Destination { get; set; } = "";
}
