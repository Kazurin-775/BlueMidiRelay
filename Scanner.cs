using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BlueMidiRelay
{
    internal class Scanner
    {
        private readonly HashSet<ulong> _discovered = new();
        private readonly bool _showNonMidi;

        public Scanner(bool showNonMidi)
        {
            _showNonMidi = showNonMidi;
        }

        public async Task Run(int ms)
        {
            var watcher = new BluetoothLEAdvertisementWatcher();
            var discovered = new HashSet<ulong>();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += OnAdvertismentReceived;

            watcher.Start();

            await Task.Delay(ms);

            watcher.Stop();
            Console.WriteLine("Scanner stopped.");
        }

        private async void OnAdvertismentReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs e)
        {
            if (_discovered.Contains(e.BluetoothAddress))
            {
                return;
            }
            _discovered.Add(e.BluetoothAddress);

            using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(e.BluetoothAddress);
            // TODO: consider retrying if device == null
            if (device != null)
            {
                if (!_showNonMidi)
                {
                    var service = await device.GetGattServicesForUuidAsync(Constants.UUID_MIDI_SERVICE);
                    if (service.Status != GattCommunicationStatus.Success || service.Services.Count == 0)
                    {
                        return;
                    }
                }
                Console.WriteLine($"Found device: {device.Name} (at {device.BluetoothAddress:x})");
            }
        }
    }
}
