﻿using System;

namespace Sandbox.Buttplug
{
    public class ButtplugWebsocketConnectorOptions
    {
        public Uri NetworkAddress { get; set; }

        public ButtplugWebsocketConnectorOptions(Uri aAddress)
        {
            NetworkAddress = aAddress;
        }
    }
}
