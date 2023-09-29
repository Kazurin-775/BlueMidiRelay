using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BlueMidiRelay
{
    internal class BleMidiDevice : IDisposable
    {
        public struct MidiMessage
        {
            public uint Timestamp;
            public byte Status;
            public byte Data0;
            public byte Data1;
        }

        public event EventHandler<MidiMessage>? MessageReceived;
        private readonly ulong _deviceId;
        private BluetoothLEDevice? _device;
        private GattDeviceService? _gattService;
        private GattCharacteristic? _characteristic;
        private SemaphoreSlim _disconnectionSema = new(0, 1);

        public bool IsConnected
        {
            get { return _device?.ConnectionStatus == BluetoothConnectionStatus.Connected; }
        }

        public BleMidiDevice(ulong deviceId)
        {
            _deviceId = deviceId;
        }

        public async Task<bool> Connect()
        {
            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(_deviceId);
            if (_device == null)
            {
                Console.WriteLine("Error: failed to connect to device.");
                return false;
            }
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            var midiService = await _device.GetGattServicesForUuidAsync(Constants.UUID_MIDI_SERVICE);
            if (midiService.Status != GattCommunicationStatus.Success
                || midiService.Services.Count != 1)
            {
                Console.WriteLine("Error: failed to discover MIDI service on target device: " + midiService.Status);
                // Prevent possible resource leaks
                foreach (var service in midiService.Services)
                    service.Dispose();
                return false;
            }

            // Prevent resource leaks
            _gattService = midiService.Services[0];

            var midiCharacteristic = await _gattService.GetCharacteristicsForUuidAsync(Constants.UUID_MIDI_DATA_CHARACTERISTIC);
            if (midiCharacteristic.Status != GattCommunicationStatus.Success
                || midiCharacteristic.Characteristics.Count != 1)
            {
                // Note: AccessDenied errors are usually caused by previous resource leaks
                // https://stackoverflow.com/questions/71620883
                Console.WriteLine("Error: failed to access MIDI data I/O characteristic on target device: " + midiCharacteristic.Status);
                return false;
            }

            _characteristic = midiCharacteristic.Characteristics[0];
            _characteristic.ProtectionLevel = GattProtectionLevel.EncryptionRequired;
            _characteristic.ValueChanged += OnValueChanged;

            // This does not always succeed, but our OnValueChanged() may not
            // take effect if this step is omitted
            await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

            return true;
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                // Console.WriteLine($"Warning: device disconnected");
                _disconnectionSema.Release();
            }
        }

        private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs e)
        {
            var data = e.CharacteristicValue.ToArray();
            // Parse MIDI packet
            if ((data[0] >> 6) != 2 || (data[1] >> 7) != 1)
            {
                Console.WriteLine("Warning: bad header byte");
                return;
            }

            uint timestampHigh = ((uint)data[0] & 0x3F) << 7;
            uint timestamp = 999;
            byte status = 0;
            int i = 1;
            bool firstMessage = true;
            while (i < data.Length)
            {
                // Parse timestamp and status byte
                bool hasTimestampByte = false;
                if (data[i] >> 7 == 1)
                {
                    timestamp = timestampHigh | ((uint)data[i] & 0x7F);
                    hasTimestampByte = true;
                    i++;
                    if (i >= data.Length)
                    {
                        Console.WriteLine("Warning: short read (after timestamp byte)");
                        return;
                    }
                }
                if (data[i] >> 7 == 1)
                {
                    status = data[i];
                    i++;
                }

                if (firstMessage && !hasTimestampByte)
                {
                    Console.WriteLine("Warning: missing timestamp byte before first message");
                    return;
                }

                if (i >= data.Length || data[i] >> 7 != 0)
                {
                    Console.WriteLine($"Note: system common / RT message received (status {status:#x})");
                    continue;
                }
                if (i + 2 > data.Length)
                {
                    Console.WriteLine("Warning: shortened MIDI message");
                    return;
                }
                if (data[i] >> 7 != 0 || data[i + 1] >> 7 != 0)
                {
                    Console.WriteLine("Warning: malformed MIDI message");
                    return;
                }
                var message = new MidiMessage
                {
                    Timestamp = timestamp,
                    Status = status,
                    Data0 = data[i],
                    Data1 = data[i + 1],
                };
                i += 2;
                firstMessage = false;

                try
                {
                    MessageReceived?.Invoke(this, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MidiRelay: error calling OnMessageReceived");
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public async Task WaitUntilDisconnect()
        {
            await _disconnectionSema.WaitAsync();
        }

        public void Dispose()
        {
            MessageReceived = null;
            _gattService?.Dispose();
            _device?.Dispose();
            _disconnectionSema.Dispose();
        }
    }
}
