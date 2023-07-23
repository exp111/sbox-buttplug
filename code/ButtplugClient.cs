using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox.Buttplug
{
    public class ButtplugClient
    {

        internal ButtplugMessageManager _messageManager;

        /// <summary>
        /// Stores information about devices currently connected to the server.
        /// </summary>
        private readonly ConcurrentDictionary<uint, ButtplugClientDevice> _devices;

        public ButtplugClientDevice[] Devices => _devices.Values.ToArray();

        /// <summary>
        /// Name of the client, used for server UI/permissions.
        /// </summary>
        public string Name { get; }
        public bool Connected { get; private set; }

        /// <summary>
        /// Event fired on Buttplug device added, either after connect or while scanning for devices.
        /// </summary>
        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;

        public void OnDeviceAdded(object sender, DeviceAddedEventArgs args)
        {
            _devices.TryAdd(args.Device.Index, args.Device);
            DeviceAdded?.Invoke(sender, args);
        }

        /// <summary>
        /// Event fired on Buttplug device removed. Can fire at any time after device connection.
        /// </summary>
        public event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;

        public void OnDeviceRemoved(object sender, DeviceRemovedEventArgs args)
        {
            _devices.TryRemove(args.Device.Index, out _);
            DeviceRemoved?.Invoke(sender, args);
        }

        /// <summary>
        /// Fires when an error that was not provoked by a client action is received from the server,
        /// such as a device exception, message parsing error, etc... Server may possibly disconnect
        /// after this event fires.
        /// </summary>
        public event EventHandler<ButtplugExceptionEventArgs> ErrorReceived;

        public void OnErrorReceived(object sender, ButtplugExceptionEventArgs args)
        {
            ErrorReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// Event fired when the server has finished scanning for devices.
        /// </summary>
        public event EventHandler ScanningFinished;
        public void OnScanningFinished(object sender, EventArgs args)
        {
            IsScanning = false;
            ScanningFinished?.Invoke(sender, args);
        }
        /// <summary>
        /// Event fired when a server ping timeout has occured.
        /// </summary>
        public event EventHandler PingTimeout;
        public void OnPingTimeout(object sender, EventArgs args)
        {
            PingTimeout?.Invoke(sender, args);
        }
        /// <summary>
        /// Event fired when a server disconnect has occured.
        /// </summary>
        public event EventHandler ServerDisconnect;
        public class ButtplugServerDisconnectEventArgs : EventArgs
        {
            public int Status { get; }
            public string Reason { get; }

            public ButtplugServerDisconnectEventArgs(int status, string reason)
            {
                Status = status;
                Reason = reason;
            }
        }

        public void OnServerDisconnect(object sender, ButtplugServerDisconnectEventArgs args)
        {
            Connected = false;
            _devices.Clear();
            ServerDisconnect?.Invoke(sender, args);
        }

        public bool IsScanning { get; private set; }

        public ButtplugClient(string aClientName)
        {
            Name = aClientName;
            _devices = new ConcurrentDictionary<uint, ButtplugClientDevice>();
        }

        public async Task ConnectAsync(ButtplugWebsocketConnectorOptions aConnector)
        {
            _messageManager = new ButtplugMessageManager(aConnector, this);
            await _messageManager.Connect();

            Connected = true;
        }

        public async Task ConnectAsync(Uri url)
        {
            await ConnectAsync(new ButtplugWebsocketConnectorOptions(url));
        }

        public async Task ConnectAsync(string url)
        {
            await ConnectAsync(new Uri(url));
        }

        public async Task DisconnectAsync()
        {
            _messageManager.Disconnect();

            _devices.Clear();
            Connected = false;
        }

        public async Task StartScanningAsync()
        {
            IsScanning = true;
            await _messageManager.SendClientMessage(new StartScanning());
        }

        public async Task StopScanningAsync()
        {
            IsScanning = false;
            await _messageManager.SendClientMessage(new StopScanning());
        }

        public async Task StopAllDevicesAsync()
        {
            await _messageManager.SendClientMessage(new StopAllDevices());
        }
        public async Task PingAsync()
        {
            await _messageManager.SendClientMessage(new Ping());
        }
    }
}
