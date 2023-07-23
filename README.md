# sbox-buttplug
This is a managed Buttplug.io fork (https://github.com/Er1807/ManagedButtplugIo, chosen because it had the least amount of files lmao) that contains changes to run in the s&box sandbox.
Specifically I had to remove the dependency on WebSocketSharp and Newtonsoft.Json, use s&box GameTasks for Threads and remove the need for reflection.

# Usage
Add to your s&box project settings by adding it as a package.
Then use it as usual
```csharp
var client = new ButtplugClient( "My s&box Game" );
var connector = new ButtplugWebsocketConnectorOptions( new( "ws://localhost:8080" ) );
var task = client.ConnectAsync( connector );
// Get your devices and do something
```
You may need to scan for devices with `client.StartScanningAsync()`.
The devices can then be accessed from `client.Devices`.

As this is merely a client implementation, users will have to use a buttplug.io compatible server software such as Intiface® Central (https://intiface.com/central/).

# Known Issues
- You can not connect to localhost on other ports than 80, 443, 8080 or 8443. That means you probably have to change your Intiface Central Server Port, unless you're connecting to another device.
