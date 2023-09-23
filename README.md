# BlueMidiRelay

This application works just like [MIDIberry](http://newbodyfresher.linclip.com/) -- it **forwards MIDI messages between bluetooth MIDI devices and wired MIDI devices**. The only differences are that this application is built with .NET Core on Win32 (instead of UWP), and that it only has a command-line interface (for now).

As it is with MIDIberry, the most common use case of this application is to feed bluetooth MIDI input into virtual MIDI loopback devices, such as [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html).

I started this project because MIDIberry did not work for me. Hope this project could do the magic for you, but if it doesn't, since it is open source, you could always try to debug and improve it yourself :)

**Minimum required OS version**: this application supports **Windows 10 ver. 1703 (build 15063) or up**, as this is the first version in which modern BLE APIs are implemented. The theoretical minimum OS version we would be able to support shall be 1511 (build 10586), but supporting it would (at least) require rewriting quite a bit of the code, and thus won't be considered for now. Even earlier OSes don't have built-in BLE support, and thus supporting them would be nearly impossible.

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

## What about bluetooth latency?

After some time of testing, I would say that the latency introduced by my own configuration (bluetooth MIDI keyboard → laptop built-in bluetooth adapter → BlueMidiRelay → loopMIDI → DAW or VST) is only barely perceptible (compared to a wired input device), and should be more than enough for day-to-day practice and music composition work (or even rhythm games, if you would like to give a try). Anyway, how on earth could the latency of a MIDI keyboard be comparable to a real-world acoustic instrument?

(By the way, many thanks to loopMIDI, ASIO, and the work of many others which made this application possible!)

If you ever encounter latency issues when using this application, please double check that the bluetooth versions supported by your PC and MIDI keyboard is recent enough (preferably &geq; 5.0), and that your audio interface / ASIO is set up properly.
