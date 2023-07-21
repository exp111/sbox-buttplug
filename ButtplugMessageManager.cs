// <copyright file="ButtplugConnectorMessageSorter.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.Buttplug
{
    public class ButtplugMessageManager
    {
        /// <summary>
        /// Stores messages waiting for reply from the server.
        /// </summary>
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<MessageBase>> _waitingMsgs = new ConcurrentDictionary<uint, TaskCompletionSource<MessageBase>>();

        /// <summary>
        /// Holds count for message IDs, if needed.
        /// </summary>
        private int _counter;
        private readonly WebSocket _webSocket;
        private uint _pingTimeout;
        private string _webSocketUri;

        /// <summary>
        /// Gets the next available message ID. In most cases, setting the message ID is done automatically.
        /// </summary>
        public uint NextMsgId => Convert.ToUInt32(Interlocked.Increment(ref _counter));

        private readonly ButtplugClient _client;

        public ButtplugMessageManager(ButtplugWebsocketConnectorOptions connectorOptions, ButtplugClient client)
        {
            _webSocketUri = connectorOptions.NetworkAddress.AbsoluteUri;
            _webSocket = new();
            _client = client;
            _webSocket.OnMessageReceived += RecieveMessage;
            _webSocket.OnDisconnected += (status, reason) => _client.OnServerDisconnect(this, new(status, reason));
        }

        public async Task Connect()
        {
            await _webSocket.Connect(_webSocketUri);
            var result = await SendClientMessage(new RequestServerInfo() { ClientName = _client.Name, MessageVersion = 2 });

            if (result is ServerInfo serverInfo)
            {
                _pingTimeout = serverInfo.MaxPingTime;
                await GameTask.RunInThreadAsync(Pings);


                var result2 = await SendClientMessage(new RequestDeviceList() { });

                if (result2 is DeviceList list)
                {
                    foreach (var device in list.Devices)
                    {
                        AddDevice(device);
                    }
                }

            }

        }

        private void Pings()
        {
            bool wspings = _pingTimeout == 0;
            if (wspings) _pingTimeout = 1000 * 10 * 2;
            while (_webSocket.IsConnected)
            {
                if (wspings)
                    _webSocket.Send(new byte[] { 0x9 }); //Ping
                else
                    SendClientMessage(new Ping());
                GameTask.Delay((int)_pingTimeout / 2);
            }
        }

        public void Disconnect()
        {
            _webSocket.Dispose();
        }


        private void RecieveMessage(string message)
        {
            List<Message> messages = JsonSerializer.Deserialize<List<Message>>(message);
            foreach (var msg in messages)
            {
                CheckMessage(msg.To());
            }
        }

        public void Shutdown()
        {
            // If we've somehow destructed while holding tasks, throw exceptions at all of them.
            foreach (var task in _waitingMsgs.Values)
            {
                task.TrySetException(new ButtplugConnectorException("Sorter has been destroyed with live tasks still in queue, most likely due to a disconnection."));
            }

            _webSocket.Dispose();
        }

        public Task<MessageBase> SendClientMessage(MessageBase aMsg)
        {
            var id = NextMsgId;
            // The client always increments the IDs on outgoing messages
            aMsg.Id = id;

            var promise = new TaskCompletionSource<MessageBase>();
            _waitingMsgs.TryAdd(id, promise);
            string jsonString = JsonSerializer.Serialize<List<Message>>(new List<Message>() { Message.From(aMsg) }, options: new()
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            });
            _webSocket.Send(jsonString);

            return promise.Task;
        }

        public void CheckMessage(MessageBase aMsg)
        {
            // We'll never match a system message, those are server -> client only.
            if (aMsg is Error error)
            {
                if (error.ErrorCode == Error.ErrorCodeEnum.ERROR_PING)
                {
                    _client.OnPingTimeout(this, null);
                }
                _client.OnErrorReceived(this, new ButtplugExceptionEventArgs(ButtplugException.FromError(error)));
            }

            if (aMsg.Id == 0)
            {
                if (aMsg is DeviceAdded deviceAdded)
                {
                    AddDevice(deviceAdded);

                }
                if (aMsg is DeviceRemoved deviceRemoved)
                {
                    var device = _client.Devices.SingleOrDefault(x => x.Index == deviceRemoved.DeviceIndex);
                    if (device != null)
                        _client.OnDeviceRemoved(_client, new DeviceRemovedEventArgs(device));
                    else
                        _client.OnErrorReceived(this, new ButtplugExceptionEventArgs(new ButtplugDeviceException($"Cannot remove device index {deviceRemoved.DeviceIndex}, device not found.")));
                }

                if (aMsg is ScanningFinished)
                    _client.OnScanningFinished(_client, new EventArgs()); ;

                return;
            }

            // If we haven't gotten a system message and we're not currently waiting for the message
            // id, throw.
            if (!_waitingMsgs.TryRemove(aMsg.Id, out var queued))
            {
                throw new ButtplugMessageException("Message with non-matching ID received.");
            }

            if (aMsg is Error error2)
            {
                queued.SetException(ButtplugException.FromError(error2));
            }
            else
            {
                queued.SetResult(aMsg);
            }
        }

        private void AddDevice(DeviceAdded deviceAdded)
        {
            if (_client.Devices.Any(x => x.Index == deviceAdded.DeviceIndex))
                _client.OnErrorReceived(this, new ButtplugExceptionEventArgs(new ButtplugDeviceException("A duplicate device index was received. This is most likely a bug, please file at https://github.com/buttplugio/buttplug-rs-ffi")));
            else
                _client.OnDeviceAdded(_client, new DeviceAddedEventArgs(new ButtplugClientDevice(this, deviceAdded.DeviceIndex, deviceAdded.DeviceName, deviceAdded.DeviceMessages)));
        }
    }
}
