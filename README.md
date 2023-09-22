# BlueMidiRelay

This application works just like [MIDIberry](http://newbodyfresher.linclip.com/) -- it **forwards MIDI messages between bluetooth MIDI devices and wired MIDI devices**. The only differences are that this application is built with .NET Core on Win32 (instead of UWP), and that it only has a command-line interface (for now).

As it is with MIDIberry, the most common use case of this application is to feed bluetooth MIDI input into virtual MIDI loopback devices, such as [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html).

I started this project because MIDIberry did not work for me. Hope this project could do the magic for you, but if it doesn't, since it is open source, you could always try to debug and improve it yourself :)

## Usage

Scan for bluetooth MIDI devices:

```sh
.\BlueMidiRelay.exe scan
.\BlueMidiRelay.exe scan --timeout 60
```

List local (aka. wired) MIDI devices:

```sh
.\BlueMidiRelay.exe list-midi
```

Monitor bluetooth MIDI input from a particular device (i.e. receive MIDI events and print them to the console):

```sh
.\BlueMidiRelay.exe monitor -a a0b1c2d3e4f5
```

Forward bluetooth MIDI input to local MIDI output:

```sh
.\BlueMidiRelay.exe forward -s a0b1c2d3e4f5 -d "loopMIDI Port"
```
