﻿using Gds.Messages;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Websocket;
using MessagePack;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace messages.Gds.Websocket
{
    class GdsWebSocketClient
    {
        private readonly WebSocketClient client;
        private readonly string userName;
        private readonly string password;

        public event EventHandler<Tuple<Message, MessagePackSerializationException>> MessageReceived;
        public event EventHandler<byte[]> BinaryMessageReceived;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        private bool connectionAckMessageReceived = false;

        private static Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly string uri;

        public GdsWebSocketClient(String uri, String userName, String password)
        {
            this.client = new WebSocketClient(uri);
            this.userName = userName;
            this.password = password;

            client.MessageReceived += Client_MessageReceived;
            client.Disconnected += Client_Disconnected;

            this.uri = uri;
        }

        private void Info(string msg)
        {
            if (logger != null)
            {
                logger.Info(msg);
            }
        }

        private void error(string msg)
        {
            if (logger != null)
            {
                logger.Error(msg);
            }
        }

        private void Client_MessageReceived(object sender, byte[] e)
        {
            BinaryMessageReceived?.Invoke(sender, e);
            try
            {
                Message message = MessageManager.GetMessageFromBinary(e);
                MessageReceived?.Invoke(sender, new Tuple<Message, MessagePackSerializationException>(message, null));
            }
            catch (MessagePackSerializationException exception)
            {
                MessageReceived?.Invoke(sender, new Tuple<Message, MessagePackSerializationException>(null, exception));
            }
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            Disconnected?.Invoke(sender, e);
        }

        private void SendConnectionMessage() 
        {
            MessageHeader header = MessageManager.GetHeader(
                userName,
                Guid.NewGuid().ToString(),
                DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                false,
                null,
                null,
                null,
                null,
                DataType.Connection);
            MessageData data = MessageManager.GetConnectionData(true, 1, false, null, password);
            Message message = MessageManager.GetMessage(header, data);
            byte[] binary = MessageManager.GetBinaryFromMessage(message);

            int tryCount = 3;
            while (tryCount > 0)
            {
                try
                {
                    Message ack = SendSync(message, 5000);
                    if (ack.Data.IsConnectionAckData())
                    {
                        if (ack.Data.AsConnectionAckData().Status.Equals(StatusCode.OK))
                        {
                            Info("GdsWebSocketClient connected to " + uri);
                            connectionAckMessageReceived = true;
                            Connected?.Invoke(this, null);
                            break;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    tryCount--;
                }
            }
        }

        public Message SendSync(byte[] message, int timeout)
        {
            return MessageManager.GetMessageFromBinary(client.SendSync(message, timeout));
        }

        public Message SendSync(Message message, int timeout)
        {
            return SendSync(MessageManager.GetBinaryFromMessage(message), timeout);
        }

        public Message SendSync(MessageData data, String messageId, int timeout)
        {
            MessageHeader header = MessageManager.GetHeader(
                            userName,
                            messageId ?? Guid.NewGuid().ToString(),
                            DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            false,
                            null,
                            null,
                            null,
                            null,
                            DataType.Connection);
            return SendSync((MessageManager.GetMessage(header, data)), timeout);
        }

        public Message SendSync(MessageData data, int timeout)
        {
            return SendSync(data, null, timeout);
        }

        public void SendAsync(byte[] message)
        {
            client.SendAsync(message);
        }

        public void SendAsync(Message message)
        {
            SendAsync(MessageManager.GetBinaryFromMessage(message));
        }

        public void SendAsync(MessageData data, string messageId)
        {
            MessageHeader header = MessageManager.GetHeader(
                            userName,
                            messageId ?? Guid.NewGuid().ToString(),
                            DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            false,
                            null,
                            null,
                            null,
                            null,
                            DataType.Connection);
            SendAsync(MessageManager.GetMessage(header, data));
        }

        public void SendAsync(MessageData data)
        {
            SendAsync(data, null);
        }

        public void Connect()
        {
            if (!client.IsConnected())
            {
                client.ConnectSync();
            }
            if (!connectionAckMessageReceived)
            {
                SendConnectionMessage();
            }
        }

        public bool IsConnected() 
        {
            return client.IsConnected() && connectionAckMessageReceived;
        }

        public void Close()
        {
            client.CloseSync();
            connectionAckMessageReceived = false;
        }
    }
}