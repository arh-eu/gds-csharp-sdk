## Installation

The library is distributed via [NuGet](https://www.nuget.org/packages/gds-messages/) package.

`Install-Package gds-messages`

(The library was made by [this](https://github.com/neuecc/MessagePack-CSharp) messagepack c# implementation)

## Examples

### Create the Message object

A message consists of two parts, a header and a data.

The following example shows how to create the header part:

```csharp
MessageHeader header = MessageManager.GetHeader("user", "870da92f-7fff-48af-825e-05351ef97acd", 1582612168230, 1582612168230, false, null, null, null, null, DataType.Connection);

```
The data part is made in the same way:

```csharp
MessageData data = MessageManager.GetConnectionData(false, 1, false, null, "pass");
```

Once you have the header and the data part, the message can be created:

```csharp
Message message = MessageManager.GetMessage(header, data);
```

### Pack the Message (create binary from object)

A message object can be easily converted into binary:

```csharp
byte[] binary = MessageManager.GetBinaryFromMessage(message);
```

### Unpack the Message (create object from binary)

```csharp
Message unpackedMessage = MessageManager.GetMessageFromBinary(binary);
```

Once you have unpacked a message, you can check the data type and also you can use the is/as methods.

```csharp
DataType dataType = unpackedMessage.Data.GetDataType();
```

```csharp
if(unpackedMessage.Data.IsConnectionData())
{
    ConnectionData connectionData = unpackedMessage.Data.AsConnectionData();
}
```

### Send and receive messages

The library contains a simple websocket client with basic functionalities.

First, you need to create a client and connect to GDS:

```csharp
GDSWebSocketClient client = new GDSWebSocketClient("ws://127.0.0.1:8080/websocket");
client.ConnectSync();
```

After you connected, you can easily send and receive messages with both synchron and asynchron mode:

```csharp
Tuple<Message, MessagePackSerializationException> response = client.SendSync(message, 3000);
```

To send a message asynchronously:

```csharp
client.SendAsync(message);
```

The response can be accessed through the event handler subscription:

```csharp
client.MessageReceived += Client_MessageReceived;
```
