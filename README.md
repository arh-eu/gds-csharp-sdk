## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
  * [Creating the client](#creating-the-client)
  * [Using your listener](#using-your-listener)
  * [Connecting](#connecting)
  * [Creating and sending messages](#creating-and-sending-messages)
    + [INSERT](#insert)
    + [UPDATE](#update)
    + [MERGE](#merge)
    + [ACK MESSAGE FOR THE INSERT, UPDATE AND MERGE](#ack-message-for-the-insert--update-and-merge)
    + [SELECT](#select)
      - [QUERY](#query)
      - [ATTACHMENT REQUEST](#attachment-request)
    + [AUTOMATIC PUSHING](#automatic-pushing)
  * [Close the connection](#close-the-connection)
  * [Reusing the client](#reusing-the-client)
  * [Thread-safety](#thread-safety)
- [Async client example](#async-client-example)


## Installation

The library is distributed via [NuGet](https://www.nuget.org/packages/gds-messages/) package. You can install this package with running this command in the Package Manager Console.

`Install-Package gds-messages -Version 2.0.1`

(The library was made by [this](https://github.com/neuecc/MessagePack-CSharp) messagepack C# implementation)

## Usage

Messages can be sent to the GDS via WebSocket protocol. The SDK contains a WebSocket client with basic functionalities, so you can use this to send and receive messages.
You can also find a GDS Server Simulator written in Java [here](https://github.com/arh-eu/gds-server-simulator). With this simulator you can test your client code without a real GDS instance.


### Creating the client

First, we create the client object and connect to the GDS. The client has an asyncronous API meaning your application will not block when you send your requests but uses a listener.

To make things easier a builder class got introduced, that way you only need to specify the parameters you wish to use.


The methods signatures available on the `AsyncGDSClientBuilder` class (which is nested in the `AsyncGDSClient`) are the following:
```csharp
    // Sets the listener for the builder to be used when instantiating the client
    AsyncGDSClientBuilder WithListener(IGDSMessageListener value);
    
    // Sets the log for the builder to be used when instantiating the client
    AsyncGDSClientBuilder WithLog(ILog value);

    // Sets the URI for the builder to be used when instantiating the client
    AsyncGDSClientBuilder WithURI(string value);

    // Sets the username for the builder to be used when instantiating the client
    AsyncGDSClientBuilder WithUserName(string value);

    // Sets the user password for the builder to be used when instantiating the client (used in password authentication)
    AsyncGDSClientBuilder WithUserPassword(SecureString value);

    // Sets the timeout for the builder to be used when instantiating the client (used for login reply awaiting). Greater than 0.
    AsyncGDSClientBuilder WithTimeout(int value); 
    
    // Sets the ping-poing interval (in seconds) for the builder to be used when instantiating the client (used to keep the connection alive). Greater than 0.
    AsyncGDSClientBuilder WithPingPongInterval(int value);
    
    // Sets whether the GDS should send its replies on the same connection as the original login was sent on. This can make attachment requests be unable to be served if set to true, as if the connection is dropped before the GDS can retrieve the binary it cannot send it later. Additionally, the client only receives Document8 messages pushed by the GDS (based on the output rights) if its value is false.
    AsyncGDSClientBuilder WithServeOnTheSameConnection(bool value);

    // Sets the certificate for the builder to be used when instantiating the client (used in TLS communication).
    AsyncGDSClientBuilder WithCertificate(X509Certificate2 value);
    
    // Builds the client using the values previously specified.
    AsyncGDSClient Build();
```

These methods always return the builder itself so the calls can be chained together. Some parameters have default values: the `username` is set to `"user"`, the timeout is `3000(ms)` and the default GDS uri is `"ws://127.0.0.1:8888/gate"`.

Creating the client is simple, you just have to get a builder instance and specify the parameters you wish to change. Keep in mind that the `listener` is a mandatory parameter, if you do not specify it (or make it `null`) you will get an exception.

```csharp
AsyncGDSClient client = AsyncGDSClient.GetBuilder()
        .WithListener(listener)
        .WithURI("ws://192.168.1.105:8888/gate")
        .WithServeOnTheSameConnection(false)
        .Build();            
``` 

If you want to use secured connection you should can also specify your certificate used in the TLS. The websocket API uses X509 certificates to encrypt the connection, so your cert should be in this format.
 
With these the connection will be secure.  You should keep in mind that the GDS uses a different port / gate for TLS communication, so you should change the GDS URI to the secure port/gate as well, and also the connection scheme should be `wss`.


```csharp
//using System.Security.Cryptography.X509Certificates;

//this example loads the cert from a file named 'my_cert_file.p12'
FileStream f = File.OpenRead("my_cert_file.p12");

byte[] data = new byte[f.Length];

f.Read(data, 0, data.Length);
f.Close();


AsyncGDSClient client = AsyncGDSClient.GetBuilder()
        .WithListener(listener)
        .WithURI("wss://192.168.1.105:8443/gates")
        .WithServeOnTheSameConnection(false)
        //The cert data is encrypted, you have to specify your password to decrypt it
        .WithCertificate(new X509Certificate2(data, "€3RT_$ecReT_P4$sW0RĐ"))
        .Build();            
``` 

The library uses [log4net](https://logging.apache.org/log4net/) for logging, so the application needs to be configured accordingly.

You can specify your own logger if you want, but if you do not want to bother with it, if you leave it null the client will create a default one using the Console as the target of the log output.

### Using your listener

Since the client uses async communication, you have to specify the listener for the client in the builder (or in the constructor) to get the messages. The reason for this is that the messages should be only processed once (of course you can delegate them however you want), therefore instead of multiple `EventHandler<..>` a specific listener is used.

This listener has a method for each type of message the GDS can send, and also has methods to notify you when the connection succeeds or fails. You will also know when the connection is closed.

The class you have to inherit from and overwrite the methods is called `IDSMessageListener`. Most of the methods should be overridden, otherwise when the client tries to call them with the message type they belong to, they will raise a `NotImplementedException`.

However, if you know you will not use them (because you never send an `EventDocument` so you do not have expect receiving an `EventDocumentACK`) you do not have to bother to overwrite each one of them.

The `onConnectionSuccess(..)` and `onDisconnect()` methods will do nothing if you do not overwrite them.

```csharp
 public abstract class IGDSMessageListener
    {
        /// <summary>
        /// Called upon successfully establishing a connection (with accepted login) towards the GDS.
        /// </summary>
        public virtual void OnConnectionSuccess(MessageHeader header, ConnectionAckData data) { }

        /// <summary>
        /// Called if the connection (or the login) is unsuccessful.
        /// The error is either caused by an exception or a login failure, this can be retreived from the parameter.
        /// </summary>
        public virtual void OnConnectionFailure(Either<Exception, KeyValuePair<MessageHeader, ConnectionAckData>> cause)
        {
            throw new NotImplementedException("IGDSMessageListener::OnConnectionFailure()");
        }

        /// <summary>
        /// Called upon disconnecting from the GDS (only if the connection was already established and successfully logged in).
        /// </summary>
        public virtual void OnDisconnect() { }

        /// <summary>
        /// Called upon receiving an EventAck data from the GDS.
        /// </summary>
        public virtual void OnEventAck3(MessageHeader header, EventAckData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Request from the GDS.
        /// </summary>
        public virtual void OnAttachmentRequest4(MessageHeader header, AttachmentRequestData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Request ACK from the GDS.
        /// </summary>
        public virtual void OnAttachmentRequestAck5(MessageHeader header, AttachmentRequestAckData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Responsefrom the GDS.
        /// </summary>
        public virtual void OnAttachmentResponse6(MessageHeader header, AttachmentResponseData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Response ACK from the GDS.
        /// </summary>
        public virtual void OnAttachmentResponseAck7(MessageHeader header, AttachmentResponseAckData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Event Document from the GDS.
        /// </summary>
        public virtual void OnEventDocument8(MessageHeader header, EventDocument data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Event Document ACK from the GDS.
        /// </summary>
        public virtual void OnEventDocumentAck9(MessageHeader header, EventDocumentAck data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Query Request ACK from the GDS.
        /// </summary>
        public virtual void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
        {
            throw new NotImplementedException();
        }
    }
```
While creating the connection, after the WebSocket connection is successfully established the client will automatically send a login message with the credentials you specified previously. If a positive acknowledgment message arrives, the `OnConnectionSuccess(..)` method will be called in your listener. If the connection fails for any reason or your login request is declined by the GDS, you will be notified on the `OnConnectionFailure(..)` callback instead. This will not trigger the `OnDisconnected()` method, which will only be invoked if the connection was successful.

The client state can be always checked by the `State` property, which returns an `int` value. These values are defined in the `ConnectionState` class as compile-time constants:

```csharp
public sealed class ConnectionState
{
    /// The client is instantiated but connect() was not yet called.
    public const int NOT_CONNECTED = 0;

    ///The connect() method was called on the client, the underlying channels are being created
    public const int INITIALIZING = 1;

    ///Channels successfully initialized, trying to establish the TCP/WebSocket Connection
    public const int CONNECTING = 2;

    /// The WebSocket connection (and TLS) is successfully established
    public const int CONNECTED = 3;

    /// The login message was successfully sent to the GDS
    public const int LOGGING_IN = 4;

    /// The login was successful. Client is ready to use
    public const int LOGGED_IN = 5;

    /// The connection was closed after a successful login (from either the client or the GDS side).
    public const int DISCONNECTED = 6;

    /// Error happened during the initialization of the client.
    public const int FAILED = 7;
}
```
### Connecting

To connect you simply have to invoke the `Connect()` method on the client. This will initiate everything in the background asynchronously so your main thread will not be blocked. Once your client is ready to be used, the `OnConnectionSuccess(..)` method will be called on the listener.

```csharp
client.Connect();
```

Please keep in mind that you should not send any messages until you have received the (positive) ACK for your login, otherwise the GDS will drop your connection as the authentication and authorization processes did not finish yet but your client is trying to send messages (which is invalid without a positive login ACK).

### Creating and sending messages

After you connected, you can send your messages to the GDS. The method names used for sending always contain the types of messages you are about to send to lead you. For example, sending attachment requests can be done by invoking the `SendAttachmentRequest4(..)` method. The `4` in the name stands for the internal type of the message.

Methods used for sending always contain optional parameters (besides what values can be passed as their data):
 - `messageID` for the message ID to be used in the header,
 - `header` the custom message header to be used
  
These two always come last if you do not want to use named parameters. If you specify them both, your `messageID` will be ignored and will not replace the one found in the header.

The default header (if you leave it empty/null) means that there is no fragmentation set, the creation and request times are set to the current system time. The message data type is determined automatically by the method you call.


In the following, take a look at what sending and receiving messages look like for different message types. The API provided by the client uses the `MessageManager` class which wraps the message creations with default parameters. You can use the manager as well to fully customise your messages and call the `client.SendMessage(MessageHeader, MessageData)` method to send them.

- [INSERT](#INSERT)
- [UPDATE](#UPDATE)
- [MERGE](#MERGE)
- [ACK message for the INSERT, UPDATE and MERGE](#ACK-MESSAGE-FOR-THE-INSERT-UPDATE-AND-MERGE)
- [SELECT](#Select)
	- [QUERY](#Query)
	- [ATTACHMENT REQUEST](#ATTACHMENT-REQUEST)
- [Automatic pushing](#AUTOMATIC-PUSHING)


#### INSERT

```csharp
string operationsStringBlock = "INSERT INTO multi_event (id, plate, images) VALUES('EVNT2006241023125476', 'ABC123', array('ATID2006241023125470'));INSERT INTO \"multi_event-@attachment\" (id, meta, data) VALUES('ATID2006241023125470', 'some_meta', 0x62696e6172795f69645f6578616d706c65)";
Dictionary<string, byte[]> binaryContentsMapping = new Dictionary<string, byte[]> { { "62696e6172795f69645f6578616d706c65", new byte[] { 1, 2, 3 } } };

client.SendEvent2(operationsStringBlock, binaryContentsMapping);
```

#### UPDATE

```csharp
string operationsStringBlock = "UPDATE multi_event SET speed = 100 WHERE id = 'TEST2006301005294810'";
client.SendEvent2(operationsStringBlock);
```

#### MERGE

```csharp
string operationsStringBlock = "MERGE INTO multi_event USING (SELECT 'TEST2006301005294810' as id, 'ABC123' as plate, 100 as speed) I " +
                    "ON (multi_event.id = I.id) " +
                    "WHEN MATCHED THEN UPDATE SET multi_event.speed = I.speed " +
                    "WHEN NOT MATCHED THEN INSERT (id, plate) VALUES (I.id, I.plate)";
client.SendEvent2(operationsStringBlock);
```


#### ACK MESSAGE FOR THE INSERT, UPDATE AND MERGE

The response is available through the listener you specified when you created the client.
```csharp
class MyListener : IGDSMessageListener
{
    // rest of the methods

    public override void OnEventAck3(MessageHeader header, EventAckData data)
    {
        if(data.Status == StatusCode.OK)
        {
            Console.WriteLine("Everything seems to be OK.");
            //process it as you want
        }
        else
        {
            Console.WriteLine(string.Format("AckStatus: {0}, Global Exception: {1}", data.Status, data.Exception));
        }
    }
}
```
#### SELECT

- [QUERY](#QUERY)
- [ATTACHMENT REQUEST](#ATTACHMENT-REQUEST)

##### QUERY

```csharp
client.SendQueryRequest10("SELECT * FROM multi_event LIMIT 1000", ConsistencyType.NONE, 10000L);
```

The ack for this message is also available through the subscribed listener. After you received the ack, you can send a 'next query page' type message. 

```csharp
GDSMessageListener listener = new GDSMessageListener() {

    //rest of the methods

    public override void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
    {
        if(data.Status != StatusCode.OK)
        {
            Console.WriteLine("Select failed!");
            Console.WriteLine(string.Format("Status Code: {0}, Reason: {1}", data.Status, data.Exception));
            return;
        }

        Console.WriteLine(string.Format("The client received {0} records.", data.AckData.NumberOfHits));

        if(data.AckData.HasMorePage)
        {
            //you can send another message to get the rest of the result
            //we use the username "user" with a timeout of 10 seconds
            client.SendMessage(
                MessageManager.GetHeader("user", DataType.NextQueryPageRequest),
                MessageManager.GetNextQueryPageRequest(data.AckData.QueryContextDescriptor, 10000L)
            );
        }
    }
};
```

##### ATTACHMENT REQUEST

Sending an attachment request goes the same way as anything else so far. 

```csharp
client.SendAttachmentRequest4("SELECT * FROM \"multi_event-@attachment\" WHERE id='TEST2006301005294740' and ownerid='TEST2006301005294810' FOR UPDATE WAIT 86400");
```

The ack for this message is available through the subscribed listener.
The ack may contain the attachment if you also requested the binary attachment.
If not contains and you requested the binary, the attachment is not yet available and will be sent as an 'attachment response' type message at a later time.

You should not forget, that if you receive the attachment in an `AttachmentResponseData` message (type 6), you are required to send the appropriate ACK back to the GDS, otherwise it will send the attachment again and again unless the ACK arrives for it.



```csharp
GDSMessageListener listener = new GDSMessageListener() {

    //rest of the methods

    public override void OnAttachmentResponse6(MessageHeader header, AttachmentResponseData data)
    {
        string messageID = header.MessageId;
        byte[] attachment = data.Result.Attachment;

        client.SendAttachmentResponseAck7(StatusCode.OK,
                new AttachmentResponseAckTypeData(StatusCode.Created,
                    new AttachmentResponseAckResult(
                        data.Result.RequestIds,
                        data.Result.OwnerTable,
                        data.Result.AttachmentId
                    )
                )
            );
    }
};
```

Note: the GDS may also send an attachment request - `OnAttachmentRequest4(MessageHeader header, AttachmentRequestData request)` - to the client.


#### AUTOMATIC PUSHING 

A user may be interested in data or changes in specific data. 
The criteria system, based on which data may be of interest to the user, is included in the configuration of the delivered system. 
This data is sent automatically by the GDS. For these, you should also send an ACK back for the same reason.


```csharp
GDSMessageListener listener = new GDSMessageListener() {

    //rest of the methods
 
    public override void OnAttachmentResponse6(MessageHeader header, AttachmentResponseData data)
         //... same as above.
     }
     
    public override void OnEventDocument8(MessageHeader header, EventDocument data)
    {
        //process the event as you need to

        client.SendEventDocumentAck9(StatusCode.OK,
                new List<EventDocumentAckResult>()
                {
                    new EventDocumentAckResult(StatusCode.OK, null)
                }
        );
    }
}
```

### Close the connection

The client is simply closed by the `Close()` method.

```csharp
client.Close();
```

### Reusing the client

The client is not reusable, meaning that if the connection (login) fails or the client is closed, the `Connect()` method cannot be invoked and will throw an `InvalidOperationException`. If you want to use the client again, you have to create a new instance.

### Thread-safety

The `AsyncGDSClient` is created with a thread-safe approach, meaning you can send messages from multiple threads without having to worry about race conditions or deadlocks. If multiple threads try to invoke the `Connect()` method on the client, only the first one will be successful, the client will raise an `InvalidOperationException` for the other threads.

## Async client example


```csharp
using Gds.Messages;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Utils;
using MessagePack;
using messages.Gds.Websocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using System.Security.Cryptography.X509Certificates;

namespace GDSExample
{

    class MyClientTest
    {
        private static CountdownEvent countdown = new CountdownEvent(1);
        private class Reference<T>
        {
            public T Value{get;set;}
            public Reference(T val)
            {
                Value = val;
            }
        }

        private class TestListener : IGDSMessageListener
        {
            private readonly Reference<AsyncGDSClient> client;
            public TestListener(Reference<AsyncGDSClient> client)
            {
                this.client = client;
            }
            public override void OnConnectionSuccess(MessageHeader header, ConnectionAckData data)
            {
                Console.WriteLine("Client successfully connected!");
                
                Console.WriteLine("Sending query message..");
                client.Value.SendQueryRequest10(
                    "SELECT * FROM multi_event LIMIT 1000", ConsistencyType.NONE, 10000L
                    );
            }

            public override void OnDisconnect()
            {
                Console.WriteLine("Client disconnected!");
            }

            public override void OnConnectionFailure(Either<Exception, KeyValuePair<MessageHeader, ConnectionAckData>> cause)
            {
                Console.WriteLine(string.Format("Client got wrecked! Reason: {0}", cause.ToString()));
                countdown.Signal();
            }

            public override void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
            {
                if(data.Status != StatusCode.OK)
                {
                    Console.WriteLine("Select failed!");
                    Console.WriteLine(string.Format("Status: {0}, Reason: {1}", data.Status, data.Exception));
                    client.Value.Close();
                    countdown.Signal();
                    return;
                }

                Console.WriteLine(string.Format("The client received {0} records.", data.AckData.NumberOfHits));

                if(data.AckData.HasMorePage)
                {
                    client.Value.SendMessage(
                       MessageManager.GetHeader("user", DataType.NextQueryPageRequest),
                       MessageManager.GetNextQueryPageRequest(data.AckData.QueryContextDescriptor, 10000L)
                   );
                }
                else
                {
                    countdown.Signal();
                    client.Value.Close();
                }
            }
        }

        static void Main(string[] args)
        {
            Reference < AsyncGDSClient > clientRef = new Reference<AsyncGDSClient>(null);
            TestListener listener = new TestListener(clientRef);

            AsyncGDSClient client = AsyncGDSClient.GetBuilder()
                .WithListener(listener)
                .WithTimeout(10000)
                .WithServeOnTheSameConnection(false)
                .Build();
            clientRef.Value = client;


            client.Connect();

            countdown.Wait();
        }
    }
}
```