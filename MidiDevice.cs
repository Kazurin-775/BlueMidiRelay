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

        public static void SendMessageTo(MidiRelay.MidiMessage message, MidiOut device)
        {
            if (message.Status >> 5 == 4)
            {
                // Note on or note off event
                var noteOn = new NoteOnEvent(0, (message.Status & 0xF) + 1, message.Data0, message.Data1, 0);
                var msgToSend = (message.Status & 0x10) != 0 ? noteOn.GetAsShortMessage() : noteOn.OffEvent.GetAsShortMessage();
                device.Send(msgToSend);

                var action = (message.Status & 0x10) != 0 ? "on" : "off";
                Console.WriteLine($"Note {noteOn.NoteNumber} {action} (velocity {noteOn.Velocity})");
            }
            else
            {
                Console.WriteLine($"Warning: ignoring unknown MIDI event {message.Status:#x}");
            }
        }
    }
}
