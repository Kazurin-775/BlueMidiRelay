using Windows.Devices.Bluetooth.Advertisement;

namespace BlueMidiRelay
{
    internal class Scanner
    {
        // Use class instead of struct since it provides pass-by-ref semantics.
        private class AdvertisedDevice
        {
            public string? LocalName;
            public bool HasMidiService;
            public bool AlreadyShown;
        }

        private readonly Dictionary<ulong, AdvertisedDevice> _discovered = new();
        private readonly bool _showNonMidi;

        public Scanner(bool showNonMidi)
        {
            _showNonMidi = showNonMidi;
        }

        public async Task Run(int ms)
        {
            var watcher = new BluetoothLEAdvertisementWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += OnAdvertismentReceived;

            watcher.Start();

            await Task.Delay(ms);

            watcher.Stop();
            ShowAllDevicesWithoutNames();
            Console.WriteLine("Scanner stopped.");
        }

        private void OnAdvertismentReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs e)
        {
            if (!_discovered.TryGetValue(e.BluetoothAddress, out var device))
            {
                device = new();
                _discovered[e.BluetoothAddress] = device;
            }

            if (e.Advertisement.LocalName != "")
            {
                device.LocalName = e.Advertisement.LocalName;
            }
            if (e.Advertisement.ServiceUuids.Contains(Constants.UUID_MIDI_SERVICE))
            {
                device.HasMidiService = true;
            }
            if (!device.AlreadyShown && device.LocalName != null && (device.HasMidiService || _showNonMidi))
            {
                device.AlreadyShown = true;
                Console.WriteLine($"Found device: {device.LocalName} (at {e.BluetoothAddress:x})");
            }
        }

        private void ShowAllDevicesWithoutNames()
        {
            foreach (var pair in _discovered)
            {
                if (pair.Value.AlreadyShown || (!_showNonMidi && !pair.Value.HasMidiService))
                {
                    continue;
                }
                Console.WriteLine($"Found device with no name at {pair.Key:x}");
            }
        }
    }
}
