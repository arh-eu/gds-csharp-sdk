﻿/*
 * Copyright 2020 ARH Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Gds.Messages;
using MessagePack;
using NLog;
using System;
using System.Threading;
using WebSocket4Net;

namespace Gds.Websocket
{
    /// <summary>
    /// Simple websocket class with basic functionality for sending and receiving messages.
    /// </summary>
    public class WebSocketClient
    {
        private readonly WebSocket client;
        private readonly AutoResetEvent messageReceiveEvent = new AutoResetEvent(false);

        private byte[] lastMessageReceived;
        public event EventHandler<byte[]> MessageReceived;

        public event EventHandler Connected;
        public event EventHandler Disconnected;

        private readonly string uri;

        private static Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketClient"/> class
        /// </summary>
        /// <param name="uri">websocket server uri</param>
        public WebSocketClient(string uri)
        {
            client = new WebSocket(uri);
            client.Opened += new EventHandler(Opened);
            client.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(Error);
            client.Closed += new EventHandler(Closed);
            client.DataReceived += new EventHandler<DataReceivedEventArgs>(DataReceived);

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
            if(logger != null)
            {
                logger.Error(msg);
            }
        }

        /// <summary>
        /// Connect to the server (specified by the uri) asynchronously
        /// </summary>
        public void ConnectAsync()
        {
            Info("WebSocketClient connecting to " + uri);
            client.Open();
        }

        /// <summary>
        /// Connect to the server (specified by the uri) synchronously
        /// </summary>
        /// <returns></returns>
        public bool ConnectSync()
        {
            ConnectAsync();
            while (client.State == WebSocketState.Connecting) { };
            return client.State == WebSocketState.Open;
        }

        /// <summary>
        /// Whether the client is connected
        /// </summary>
        public bool IsConnected()
        {
            return client.State == WebSocketState.Open;
        }

        /// <summary>
        /// Close the connection asynchronously
        /// </summary>
        public void CloseAsync()
        {
            Info("WebSocketClient close connection");
            client.Close();
        }

        /// <summary>
        /// Close the connection synchronously
        /// </summary>
        public bool CloseSync()
        {
            CloseAsync();
            while (client.State == WebSocketState.Closing) { };
            return client.State == WebSocketState.Closed;
        }

        /// <summary>
        /// Whether the connection is closed
        /// </summary>
        public bool IsClosed()
        {
            return client.State == WebSocketState.Closed;
        }

        /// <summary>
        /// Get the client state
        /// </summary>
        /// <returns></returns>
        public WebSocketState GetState()
        {
            return client.State;
        }

        /// <summary>
        /// Send a message to the server asynchronously
        /// </summary>
        /// <param name="message">The message to be send</param>
        public void SendAsync(byte[] message)
        {
            if (message == null)
            {
                error("An error occurred while sending binary message. Message cannot be null.");
                throw new InvalidOperationException("Parameter 'message' cannot be null");
            }
            Info("WebSocketClient sending binary message...");
            client.Send(message, 0, message.Length);
        }

        /// <summary>
        /// Send a message to the server synchronously
        /// </summary>
        /// <param name="message">The message to be send</param>
        /// <param name="timeout">The timeout value in milliseconds</param>
        /// <returns></returns>
        public byte[] SendSync(byte[] message, int timeout)
        {
            SendAsync(message);
            if (!messageReceiveEvent.WaitOne(timeout))
            {
                error("A timeout occurred while sending binary message");
                throw new TimeoutException("The timeout period elapsed");
            }
            return lastMessageReceived;
        }

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            Info("WebSocketClient received binary message");
            lastMessageReceived = e.Data;
            MessageReceived?.Invoke(sender, lastMessageReceived);
            messageReceiveEvent.Set();
        }

        private void Opened(object sender, EventArgs e)
        {
            Info("WebSocketClient connected to " + uri);
            Connected?.Invoke(sender, e);
        }
        private void Closed(object sender, EventArgs e) 
        {
            Info("WebSocketClient disconnected from " + uri);
            Disconnected?.Invoke(sender, e);
        }
        private void Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e) 
        {
            error(e.Exception.Message);
        }
    }
}