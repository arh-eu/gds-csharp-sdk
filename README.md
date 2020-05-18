## Installation

The library is distributed via [NuGet](https://www.nuget.org/packages/gds-messages/) package.

`Install-Package gds-messages -Version 1.0.0`

(The library was made by [this](https://github.com/neuecc/MessagePack-CSharp) messagepack c# implementation)

## How to create messages

A message consists of two parts, a header and a hata. You can create these objects through the Gds.Messages.MessageManager class.

The following example shows the process of creating messages by creating an attachment request type message.

First, we create the header part.
```csharp
MessageHeader header = MessageManager.GetHeader("user", "870da92f-7fff-48af-825e-05351ef97acd", 1582612168230, 1582612168230, false, null, null, null, null, DataType.AttachmentRequest);
```

After that, we create the data part.
```csharp
MessageData data = MessageManager.GetAttachmentRequestData("SELECT * FROM \"events-@attachment\" WHERE id='ATID202001010000000000' and ownerid='EVNT202001010000000000' FOR UPDATE WAIT 86400");
```

Once we have a header and a data, we can create the message object.
```csharp
Message message = MessageManager.GetMessage(header, data);
```

## How to send and receive messages

Messages can be sent to the GDS via WebSocket protocol. The SDK contains a WebSocket client with basic functionalities, so you can use this to send and receive messages.
You can also find a GDS Server Simulator written in Java [here](https://github.com/arh-eu/gds-server-simulator). With this simulator you can test your client code without a real GDS instance.

A message can be sent as follows.

First, we create the client object and connect to the GDS.
```csharp
GDSWebSocketClient client = new GDSWebSocketClient("ws://127.0.0.1:8080/gate");
``` 
connect synchronously 
```csharp
client.ConnectSync();
``` 
or asynchronously
```csharp
client.ConnectAsync();
``` 

Before sending any message to gds, it is also necessary to send a connection type message. So we will send such a message first. 
This message can be created in the same way as any other (see [How to create messages](##How-to-create-messages)).

An example for creating a connection type message.
```csharp
MessageHeader connectionMessageHeader = MessageManager.GetHeader("user", "870da92f-7fff-48af-825e-05351ef97acd", 1582612168230, 1582612168230, false, null, null, null, null, DataType.Connection);
MessageData connectionMessageData = MessageManager.GetConnectionData(false, 1, false, null, "pass");
Message connectionMessage = MessageManager.GetMessage(connectionMessageHeader, connectionMessageData);
```

Now, we can send this message to the GDS through the previously created client object. You can send this in both synchronous and asynchronous mode. 
Let's see the synchronous mode first.
```csharp
Tuple<Message, MessagePackSerializationException> connectionResponse = client.SendSync(connectionMessage, 3000);
```

The second parameter of the SendSync() method is the timeout value in milliseconds. If the timeout is occured, a System.TimeoutException is thrown.
The response is a Tuple object. The first value is the respone message received by the GDS if the serialization/deserialization was successful.
If a MessagePack.MessagePackSerializationException is occured, the first value is null and you can get the exception object with the second value. 

Once we have received the response, we can process it as follows, for example.
```csharp
if(connectionResponse.Item2 == null)
{
    Message connectionResponseMessage = connectionResponse.Item1;
    if(connectionResponseMessage.Header.DataType.Equals(DataType.ConnectionAck))
    {
        ConnectionAckData connectionAckData = connectionResponseMessage.Data.AsConnectionAckData();
        // do something with the connection response ack data...
    }
}
```

Let's look at the same in asynchronous mode.

First, we need to create and subsribe to the GDSWebSocketClient.MessageReceived event handler. 
```csharp
static void Client_MessageReceived(object sender, Tuple<Message, MessagePackSerializationException> e)
{
	// do something with the connection response ack data...
}
```

```csharp
client.MessageReceived += Client_MessageReceived;
```

Now, we can send the message.
```csharp
client.SendAsync(connectionMessage);
```

After you received a positive acknowledgement for the connection message, you can send any message type. Let's see an event message for example. 
```csharp
MessageHeader eventMessageHeader = MessageManager.GetHeader("user", "c08ea082-9dbf-4d96-be36-4e4eab6ae624", 1582612168230, 1582612168230, false, null, null, null, null, DataType.Event);
string operationsStringBlock = "INSERT INTO events (id, some_field, images) VALUES('EVNT202001010000000000', 'some_field', array('ATID202001010000000000'));INSERT INTO \"events-@attachment\" (id, meta, data) VALUES('ATID202001010000000000', 'some_meta', 0x62696e6172795f6964315f6578616d706c65)";
Dictionary<string, byte[]> binaryContentsMapping = new Dictionary<string, byte[]> { { "62696e6172795f69645f6578616d706c65", new byte[] { 1, 2, 3 } } };
MessageData eventMessageData = MessageManager.GetEventData(operationsStringBlock, binaryContentsMapping);
Message eventMessage = MessageManager.GetMessage(eventMessageHeader, eventMessageData);

Tuple<Message, MessagePackSerializationException> eventResponse = client.SendSync(eventMessage, 3000);
if (eventResponse.Item2 == null)
{
    Message eventResponseMessage = eventResponse.Item1;
    if (eventResponseMessage.Header.DataType.Equals(DataType.EventAck))
    {
        EventAckData eventAckData = eventResponseMessage.Data.AsEventAckData();
        // do something with the event ack data...
    }
}
```

At the end, we close the websocket connection as well.
```csharp
client.CloseSync();
```
