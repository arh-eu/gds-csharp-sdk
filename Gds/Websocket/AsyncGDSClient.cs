using Gds.Messages;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Utils;
using log4net;
using MessagePack;
using SuperSocket.ClientEngine;
using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using WebSocket4Net;

namespace messages.Gds.Websocket
{
    /// <summary>
    /// A client class that can be used to communicate with a GDS instance by WebSocket connection.
    /// The class is thread-safe, meaning can be used from multiple threads to send messages (still only uses one listener).
    /// </summary>
    public sealed class AsyncGDSClient : IDisposable
    {
        private readonly CountdownEvent countdown;
        private readonly IGDSMessageListener listener;
        private readonly ILog log;
        private readonly string userName;
        private readonly SecureString userPassword;
        private readonly int timeout;
        private readonly WebSocket websocketClient;
        private volatile int state;
        private readonly int PingPongInterval;

        private bool disposed = false;

        /// <summary>
        /// Creates a new Client instance with the specified parameters
        /// </summary>
        /// <param name="listener">The Listener used for callbacks</param>
        /// <param name="uri">The URI of the GDS instance</param>
        /// <param name="userName">The username used for communication. Cannot be null or empty</param>
        /// <param name="userPassword">The password used for password authentication. Null otherwise</param>
        /// <param name="timeout">Timeout used for the connection establishment. Value most be strictly positive</param>
        /// <param name="cert">The certificate used for TLS authentication</param>
        /// <param name="log">The log used by the client.</param>
        /// <param name="PingPongInterval">The interval used to send automatic ping-pong in seconds</param>
        public AsyncGDSClient(IGDSMessageListener listener, string uri, string userName, SecureString userPassword, int timeout, X509Certificate2 cert, ILog log, int PingPongInterval)
        {
            this.listener = Utils.RequireNonNull(listener, "The message listener cannot be set to null!");

            Utils.RequireNonNull(uri, "The URI cannot be set to null!");
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("The username cannot be null, empty or set to only whitespaces!");
            }

            if (timeout < 1)
            {
                throw new ArgumentOutOfRangeException(string.Format("The timeout must be to positive! (Specified: {0})", timeout));
            }

            countdown = new CountdownEvent(1);
            if (log == null)
            {
                log4net.Config.BasicConfigurator.Configure(LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly()));
                this.log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            }
            else
            {
                this.log = log;
            }
            this.userName = userName;
            this.userPassword = userPassword;
            this.timeout = timeout;

            if (PingPongInterval < 1)
            {
                throw new ArgumentOutOfRangeException(string.Format("The ping-pong interval must be to positive! (Specified: {0})", PingPongInterval));
            }
            this.PingPongInterval = PingPongInterval;

            state = ConnectionState.NOT_CONNECTED;

            websocketClient = new WebSocket(uri, sslProtocols: SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13);
            if (cert != null)
            {
                //The GDS uses self-signed certificate therefore we have to enable them to be used for the protocol
                websocketClient.Security.AllowCertificateChainErrors = true;
                websocketClient.Security.AllowNameMismatchCertificate = true;

                websocketClient.Security.Certificates.Add(cert);
            }
        }

        /// <summary>
        /// disposes the countdown if it was still present and closes the connection if it was still open.
        /// </summary>
        ~AsyncGDSClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Closes the connection towards the GDS, releasing any network resources still held.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    countdown.Dispose();
                    Close();
                }
                disposed = true;
            }
        }

        /// <summary>
        /// Used to check the current state of the client. See <see cref="ConnectionState"/> for possible values.
        /// </summary>
        public int State => state;

        /// <summary>
        /// Used to check whether the client is already connected to the GDS.
        /// </summary>
        public bool IsConnected => State == ConnectionState.LOGGED_IN;

        /// <summary>
        /// Used to close the underlying WebSocket connection.
        /// </summary>
        public void Close()
        {
            if (State != ConnectionState.FAILED)
            {
                state = ConnectionState.DISCONNECTED;
            }
            websocketClient.Close();
        }


        /// <summary>
        /// Sends an event message
        /// </summary>
        /// <param name="operationsStringBlock">The operations in standard SQL statements, separated with ';' characters.</param>
        /// <param name="binaryContentsMapping">The mapping of the binary contents.</param>
        /// <param name="executionPriorityStructure">The execution priority structure.</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        /// <returns>The created EventData object.</returns>
        public void SendEvent2(string operationsStringBlock, Dictionary<string, byte[]> binaryContentsMapping = null,
            List<List<Dictionary<int, bool>>> executionPriorityStructure = null, string messageID = null, MessageHeader header = null)
        {
            MessageData data = MessageManager.GetEventData(operationsStringBlock, binaryContentsMapping, executionPriorityStructure);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.Event);

            SendMessage(messageHeader, data);
        }

        /// <summary>
        /// Sends an Attachment Request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendAttachmentRequest4(string request, string messageID = null, MessageHeader header = null)
        {
            MessageData data = MessageManager.GetAttachmentRequestData(request);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.AttachmentRequest);

            SendMessage(messageHeader, data);
        }


        /// <summary>
        /// Sends an Attachment Request ACK.
        /// </summary>
        /// <param name="status">The status code</param>
        /// <param name="ackData">The ack data for the request</param>
        /// <param name="exception">The global exception</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendAttachmentRequestACK5(StatusCode status, AttachmentRequestAckTypeData ackData, string exception = null, string messageID = null, MessageHeader header = null)
        {
            MessageData data = MessageManager.GetAttachmentRequestAckData(status, ackData, exception);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.AttachmentRequestAck);

            SendMessage(messageHeader, data);
        }


        /// <summary>
        /// Sends an Attachment Response.
        /// </summary>
        /// <param name="result">The result of the attachment</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendAttachmentResponse6(AttachmentResult result, string messageID = null, MessageHeader header = null)
        {

            MessageData data = MessageManager.GetAttachmentResponseData(result);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.AttachmentResponse);

            SendMessage(messageHeader, data);
        }


        /// <summary>
        /// Sends an Attachment Response ACK.
        /// </summary>
        /// <param name="status">The status code</param>
        /// <param name="ackData">The ack data for the request</param>
        /// <param name="exception">The global exception</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendAttachmentResponseAck7(StatusCode status, AttachmentResponseAckTypeData ackData, string exception = null, string messageID = null, MessageHeader header = null)
        {

            MessageData data = MessageManager.GetAttachmentResponseAckData(status, ackData, exception);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.AttachmentResponseAck);

            SendMessage(messageHeader, data);
        }

        /// <summary>
        /// Sends an Event Document
        /// </summary>
        /// <param name="tableName">The name of the table</param>
        /// <param name="fieldDescriptors">The field descriptors</param>
        /// <param name="records">The records</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendEventDocument8(string tableName, List<FieldDescriptor> fieldDescriptors, List<List<object>> records, string messageID = null, MessageHeader header = null)
        {

            MessageData data = MessageManager.GetEventDocumentData(tableName, fieldDescriptors, records);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.EventDocument);

            SendMessage(messageHeader, data);
        }

        /// <summary>
        /// Sends an Event Document ACK.
        /// </summary>
        /// <param name="status">The status code</param>
        /// <param name="ackData">The ack data for the request</param>
        /// <param name="exception">The global exception</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendEventDocumentAck9(StatusCode status, List<EventDocumentAckResult> ackData, string exception = null, string messageID = null, MessageHeader header = null)
        {

            MessageData data = MessageManager.GetEventDocumentAckData(status, ackData, exception);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.EventDocumentAck);

            SendMessage(messageHeader, data);
        }


        /// <summary>
        /// Sends a Query Request
        /// </summary>
        /// <param name="selectStringBlock">The SELECT statement.</param>
        /// <param name="consistencyType">The consistency type used for the query.</param>
        /// <param name="timeout">The timeout value in milliseconds.</param>
        /// <param name="queryPageSize">The number of records per page.</param>
        /// <param name="queryType">The query type</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendQueryRequest10(string selectStringBlock, ConsistencyType consistencyType, long? timeout = null, int? queryPageSize = null,
                    QueryType? queryType = null, string messageID = null, MessageHeader header = null)
        {
            if (timeout == null) { timeout = this.timeout; }
            MessageData data = MessageManager.GetQueryRequest(selectStringBlock, consistencyType, (long)timeout, queryPageSize, queryType);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.QueryRequest);

            SendMessage(messageHeader, data);
        }



        /// <summary>
        /// Sends a Next Query Page Request
        /// </summary>
        /// <param name="queryContextDescriptor">Query status descriptor for querying the next pages.</param>
        /// <param name="timeout">The timeout value in milliseconds.</param>
        /// <param name="messageID">The messageID to be used. If not present, random one will be generated.</param>
        /// <param name="header">The header to be used in the message. If not present, default one will be generated.</param>
        public void SendNextQueryPageRequest12(QueryContextDescriptor queryContextDescriptor, long timeout, string messageID = null, MessageHeader header = null)
        {
            MessageData data = MessageManager.GetNextQueryPageRequest(queryContextDescriptor, timeout);
            MessageHeader messageHeader = header ?? MessageManager.GetHeader(userName, messageID, DataType.NextQueryPageRequest);

            SendMessage(messageHeader, data);
        }

        /// <summary>
        /// Sends a message to the GDS the client is currently connected to.
        /// <exception cref="InvalidOperationException">If the client state is invalid (ie. sending messages before the connection is ready)
        /// or if the header and data types mismatch</exception>
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        public void SendMessage(MessageHeader header, MessageData data)
        {
            if (State != ConnectionState.LOGGED_IN)
            {
                throw new InvalidOperationException(string.Format("Expected state LOGGED_IN but got {0}", ConnectionState.AsText(State)));
            }
            if (header.DataType != data.GetDataType())
            {
                throw new InvalidOperationException(string.Format("The header and the message data types mismatch! Header: {0}, Data: {1}",
                    header.DataType, data.GetDataType()));
            }
            byte[] binary = MessageManager.GetBinaryFromMessage(header, data);
            websocketClient.Send(binary, 0, binary.Length);
        }

        /// <summary>
        /// Connects to the GDS instance using the values specified in the constructor.
        /// <exception cref="InvalidOperationException">If the client state is invalid (ie. was already used)</exception>
        /// </summary>
        public void Connect()
        {
            if (ConnectionState.NOT_CONNECTED == Interlocked.CompareExchange(ref state, ConnectionState.INITIALIZING, ConnectionState.NOT_CONNECTED))
            {
                new Thread(() =>
                {
                    websocketClient.Opened += OnSocketOpened;
                    websocketClient.Error += OnSocketError;
                    websocketClient.DataReceived += OnMessageReceived;
                    websocketClient.Closed += OnSocketClosed;
                    websocketClient.EnableAutoSendPing = true;
                    websocketClient.AutoSendPingInterval = PingPongInterval;
                    websocketClient.Open();
                    Interlocked.CompareExchange(ref state, ConnectionState.CONNECTING, ConnectionState.INITIALIZING);
                    try
                    {
                        if (!countdown.Wait(timeout))
                        {
                            int currentState = State;
                            if (currentState != ConnectionState.FAILED && currentState != ConnectionState.DISCONNECTED)
                            {
                                state = ConnectionState.FAILED;
                                listener.OnConnectionFailure(new TimeoutException(string.Format("The GDS did not reply within {0} ms!", timeout)));
                            }
                        }
                    }
                    catch (ObjectDisposedException ode)
                    {
                        LogFatal(ode.Message);
                    }
                }).Start();
            }
            else
            {
                throw new InvalidOperationException("Could not initialize the connection as the state is not 'NOT_CONNECTED' (this client was already used)!");
            }
        }

        /// <summary>
        /// Returns a Builder instance that can be used to customize the parameters more easily.
        /// </summary>
        /// <returns>The new Builder instance</returns>
        public static AsyncGDSClientBuilder GetBuilder() => new();

        private void OnSocketOpened(object sender, EventArgs args)
        {
            LogInfo("WebSocket Connection successfully established");
            int oldState = Interlocked.CompareExchange(ref state, ConnectionState.CONNECTED, ConnectionState.CONNECTING);
            if (oldState != ConnectionState.CONNECTING && oldState != ConnectionState.DISCONNECTED)
            {
                throw new InvalidOperationException(string.Format("Expected state CONNECTING or DISCONNECTED but got {0}", ConnectionState.AsText(oldState)));
            }
            MessageHeader header = MessageManager.GetHeader(
                userName,
                DataType.Connection);
            //the current GDS version is 5.1
            MessageData data = MessageManager.GetConnectionData(true, (5 << 16 | 1), false, null, userPassword?.ToString());
            Message message = MessageManager.GetMessage(header, data);
            byte[] binary = MessageManager.GetBinaryFromMessage(message);

            websocketClient.Send(binary, 0, binary.Length);
            oldState = Interlocked.CompareExchange(ref state, ConnectionState.LOGGING_IN, ConnectionState.CONNECTED);
            if (oldState != ConnectionState.CONNECTED && oldState != ConnectionState.DISCONNECTED)
            {
                throw new InvalidOperationException(string.Format("Expected state CONNECTED or DISCONNECTED but got {0}", ConnectionState.AsText(oldState)));
            }
        }

        private void OnSocketError(object sender, ErrorEventArgs eventArgs)
        {
            log.Error(eventArgs.Exception.Message);
            websocketClient.Close();
            if (State != ConnectionState.FAILED && State != ConnectionState.LOGGED_IN)
            {
                state = ConnectionState.FAILED;
                listener.OnConnectionFailure(eventArgs.Exception);
            }
        }

        private void OnSocketClosed(object sender, EventArgs args)
        {
            LogInfo("WebSocket client disconnected");
            if (State == ConnectionState.DISCONNECTED ||
                ConnectionState.LOGGED_IN == Interlocked.CompareExchange(ref state, ConnectionState.DISCONNECTED, ConnectionState.LOGGED_IN))
            {
                listener.OnDisconnect();
            }
            else if (State != ConnectionState.FAILED)
            {
                log.Warn(String.Format("The state should be FAILED or LOGGED_IN, but found {0} instead!", ConnectionState.AsText(State)));
            }
        }

        private void OnMessageReceived(object sender, DataReceivedEventArgs eventArgs)
        {

            LogInfo("WebSocket client received message");
            try
            {
                byte[] binary = eventArgs.Data;
                Message message = MessageManager.GetMessageFromBinary(binary);
                MessageHeader header = message.Header;
                MessageData data = message.Data;

                switch (message.Data.GetDataType())
                {
                    case DataType.ConnectionAck:
                        {
                            countdown.Signal();
                            ConnectionAckData ackData = data.AsConnectionAckData();
                            if (ackData.Status != StatusCode.OK)
                            {
                                int oldState = Interlocked.CompareExchange(ref state, ConnectionState.FAILED, ConnectionState.LOGGING_IN);
                                if (oldState != ConnectionState.LOGGING_IN)
                                {
                                    int currentState = State;
                                    if (currentState != ConnectionState.DISCONNECTED)
                                    {
                                        throw new InvalidOperationException(string.Format("Expected state LOGGING_IN but got {0}", ConnectionState.AsText(currentState)));
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                                Close();
                                listener.OnConnectionFailure(new KeyValuePair<MessageHeader, ConnectionAckData>(header, ackData));
                            }
                            else
                            {
                                int oldState = Interlocked.CompareExchange(ref state, ConnectionState.LOGGED_IN, ConnectionState.LOGGING_IN);
                                if (oldState != ConnectionState.LOGGING_IN)
                                {
                                    int currentState = State;
                                    if (currentState != ConnectionState.DISCONNECTED)
                                    {
                                        throw new InvalidOperationException(string.Format("Expected state LOGGING_IN but got {0}", ConnectionState.AsText(currentState)));
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                                listener.OnConnectionSuccess(header, ackData);
                            }
                        }
                        break;
                    case DataType.EventAck:
                        listener.OnEventAck3(header, data.AsEventAckData());
                        break;
                    case DataType.AttachmentRequest:
                        listener.OnAttachmentRequest4(header, data.AsAttachmentRequestData());
                        break;
                    case DataType.AttachmentRequestAck:
                        listener.OnAttachmentRequestAck5(header, data.AsAttachmentRequestAckData());
                        break;
                    case DataType.AttachmentResponse:
                        listener.OnAttachmentResponse6(header, data.AsAttachmentResponseData());
                        break;
                    case DataType.AttachmentResponseAck:
                        listener.OnAttachmentResponseAck7(header, data.AsAttachmentResponseAckData());
                        break;
                    case DataType.EventDocument:
                        listener.OnEventDocument8(header, data.AsEventDocumentData());
                        break;
                    case DataType.EventDocumentAck:
                        listener.OnEventDocumentAck9(header, data.AsEventDocumentAckData());
                        break;
                    case DataType.QueryRequestAck:
                        listener.OnQueryRequestAck11(header, data.AsQueryRequestAck());
                        break;
                    default:
                        LogWarn(string.Format("Unexpected type of message received: {0}", message.Data.GetDataType()));
                        break;
                }
            }
            catch (MessagePackSerializationException e)
            {
                LogError(string.Format("The format of the received binary message is invalid! {0}", e.ToString()));
            }
        }


        private void LogDebug(string message)
        {
            log.DebugFormat("[{0:yyyy-MM-dd HH:mm:ss}] [DEBUG] | {1}", DateTime.Now, message);
        }

        private void LogError(string message)
        {
            log.ErrorFormat("[{0:yyyy-MM-dd HH:mm:ss}] [ERROR] | {1}", DateTime.Now, message);
        }

        private void LogFatal(string message)
        {
            log.FatalFormat("[{0:yyyy-MM-dd HH:mm:ss}] [FATAL] | {1}", DateTime.Now, message);
        }

        private void LogInfo(string message)
        {
            log.InfoFormat("[{0:yyyy-MM-dd HH:mm:ss}] [INFO] | {1}", DateTime.Now, message);
        }

        private void LogWarn(string message)
        {
            log.WarnFormat("[{0:yyyy-MM-dd HH:mm:ss}] [WARN] | {1}", DateTime.Now, message);
        }

        /// <summary>
        /// Builder class used to make instantation of the client easier
        /// </summary>
        public sealed class AsyncGDSClientBuilder
        {
            private IGDSMessageListener listener;
            private ILog log;
            private string URI;
            private string userName;
            private SecureString userPassword;
            private int timeout;
            private int PingPongInterval;

            private X509Certificate2 certificate;

            /// <summary>
            /// Creates the builder with default values for userName, URI and timeout ("user", "ws://127.0.0.1:8888/gate", 3000ms)
            /// </summary>
            public AsyncGDSClientBuilder()
            {
                userName = "user";
                URI = "ws://127.0.0.1:8888/gate";
                timeout = 3000;
            }

            /// <summary>
            /// Sets the listener for the builder to be used when instantiating the client
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithListener(IGDSMessageListener value)
            {
                listener = value;
                return this;
            }

            /// <summary>
            /// Sets the log for the builder to be used when instantiating the client
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithLog(ILog value)
            {
                log = value;
                return this;
            }

            /// <summary>
            /// Sets the URI for the builder to be used when instantiating the client
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithURI(string value)
            {
                URI = value;
                return this;
            }

            /// <summary>
            /// Sets the username for the builder to be used when instantiating the client
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithUserName(string value)
            {
                userName = value;
                return this;
            }

            /// <summary>
            /// Sets the user password for the builder to be used when instantiating the client (used in password authentication)
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithUserPassword(SecureString value)
            {
                userPassword = value;
                return this;
            }


            /// <summary>
            /// Sets the timeout for the builder to be used when instantiating the client (used for login reply awaiting)
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithTimeout(int value)
            {
                if (value < 1)
                {
                    throw new ArgumentException(string.Format("Timeout must be positive! Specified: {0}", value));
                }
                timeout = value;
                return this;
            }

            /// <summary>
            /// Sets the ping-poing interval for the builder to be used when instantiating the client (used to keep the connection alive)
            /// </summary>
            /// <param name="value">The interval used to send automatic ping-pong in seconds</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithPingPongInterval(int value)
            {
                if (value < 1)
                {
                    throw new ArgumentException(string.Format("Ping-Pong interval must be positive! Specified: {0}", value));
                }
                PingPongInterval = value;
                return this;
            }
            /// <summary>
            /// Sets the certificate for the builder to be used when instantiating the client (used in TLS communication)
            /// </summary>
            /// <param name="value">The new value to be used</param>
            /// <returns>itself</returns>
            public AsyncGDSClientBuilder WithCertificate(X509Certificate2 value)
            {
                this.certificate = value;
                return this;
            }

            /// <summary>
            /// Builds the client using the values previously specified.
            /// </summary>
            /// <returns>The created client instance</returns>
            public AsyncGDSClient Build() => new(listener, URI, userName, userPassword, timeout, certificate, log, PingPongInterval);
        }
    }
}
