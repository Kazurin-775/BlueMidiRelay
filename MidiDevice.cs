using NAudio.Midi;

namespace BlueMidiRelay
{
    internal class MidiDevice
    {
        private MidiDevice() { }

        public static void ListAll()
        {
            Console.WriteLine("----- MIDI in -----");
            for (int device = 0; device < MidiIn.NumberOfDevices; device++)
            {
                Console.WriteLine($"#{device}: {MidiIn.DeviceInfo(device).ProductName}");
            }
            Console.WriteLine();

            Console.WriteLine("----- MIDI out -----");
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
            {
                Console.WriteLine($"#{device}: {MidiOut.DeviceInfo(device).ProductName}");
            }
            Console.WriteLine();

            Console.WriteLine("----- Done -----");
        }

        public static MidiOut? FindMidiOutByName(string name)
        {
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
            {
                if (MidiOut.DeviceInfo(device).ProductName.Equals(name))
                {
                    return new MidiOut(device);
                }
            }
            return null;
        }

        public static void SendMessageTo(BleMidiDevice.MidiMessage message, MidiOut device)
        {
            int channel = (message.Status & 0xF) + 1;
            switch (message.Status & 0xF0)
            {
                case 0x80:
                case 0x90:
                    // Note on / off
                    var noteOn = new NoteOnEvent(0, channel, message.Data0, message.Data1, 0);
                    var msgToSend = (message.Status & 0x10) != 0 ? noteOn.GetAsShortMessage() : noteOn.OffEvent.GetAsShortMessage();
                    device.Send(msgToSend);

                    var action = (message.Status & 0x10) != 0 ? "on" : "off";
                    Console.WriteLine($"Note {noteOn.NoteNumber} {action} (velocity {noteOn.Velocity})");
                    break;

                case 0xB0:
                    // Control / mode change
                    var controlModeChange = new ControlChangeEvent(0, channel, (MidiController)message.Data0, message.Data1);
                    device.Send(controlModeChange.GetAsShortMessage());

                    Console.WriteLine($"Control change: {controlModeChange.Controller} -> {controlModeChange.ControllerValue}");
                    break;

                case 0xE0:
                    // Pitch bend change (portamento)
                    var pitchBendChange = new PitchWheelChangeEvent(0, channel, message.Data0 | (message.Data1 << 7));
                    device.Send(pitchBendChange.GetAsShortMessage());

                    var sign = pitchBendChange.Pitch > 0x2000 ? "+" : (pitchBendChange.Pitch == 0x2000 ? " " : "");
                    Console.WriteLine($"Pitch bend change: {sign}{pitchBendChange.Pitch / (double)0x1000 - 2:f2} semitones");
                    break;

                default:
                    Console.WriteLine($"Warning: ignoring unknown MIDI event 0x{message.Status:X}");
                    break;
            }
        }
    }
}
