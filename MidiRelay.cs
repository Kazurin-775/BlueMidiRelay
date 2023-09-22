﻿using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BlueMidiRelay
{
    internal class MidiRelay
    {
        public struct MidiMessage
        {
            public uint Timestamp;
            public byte Status;
            public byte Data0;
            public byte Data1;
        }

        public event EventHandler<MidiMessage> MessageReceived;
        private readonly ulong _deviceId;
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _characteristic;

        public MidiRelay(ulong deviceId)
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
            if (midiService == null)
            {
                Console.WriteLine("Error: the device does not support MIDI service.");
                return false;
            }

            var midiCharacteristic = await midiService.Services[0].GetCharacteristicsForUuidAsync(Constants.UUID_MIDI_DATA_CHARACTERISTIC);
            if (midiCharacteristic == null)
            {
                Console.WriteLine("Error: MIDI data I/O characteristic not found?!");
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
                Console.WriteLine($"Warning: device disconnected");
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

                MessageReceived?.Invoke(this, message);
            }
        }
    }
}
