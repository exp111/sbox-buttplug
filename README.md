The library I forked doesn't work with the newest buttplug.io spec, so this repo will be archived. I could also delete it but who knows.

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

# Known Issues
- You can not connect to localhost on other ports than 80, 443, 8080 or 8443. That means you probably have to change your Intiface Central Server Port.
