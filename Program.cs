using BlueMidiRelay;
using CommandLine;

var parsedArgs = Parser.Default
    .ParseArguments<ScanOptions, MonitorOptions>(args);
await parsedArgs.WithParsedAsync(async (ScanOptions opts) =>
    {
        var scanner = new Scanner(opts.ShowNonMidi);
        await scanner.Run((int)(opts.Interval * 1000));
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

bool IsHexDigit(char c)
{
    return (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'f')
        || (c >= 'A' && c <= 'F');
}

[Verb("scan", HelpText = "Scan for bluetooth MIDI devices")]
class ScanOptions
{
    [Option('i', "interval", Default = 15u, HelpText = "The number of seconds that the scanner runs")]
    public uint Interval { get; set; }

    [Option("show-non-midi", HelpText = "Also show non-MIDI bluetooth devices")]
    public bool ShowNonMidi { get; set; }
}

[Verb("monitor", HelpText = "Monitor input from a bluetooth MIDI device")]
class MonitorOptions
{
    [Option('a', "address", Required = true, HelpText = "The device's bluetooth address")]
    public string Address { get; set; }
}
