using Gds.Messages;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Websocket;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace messages.Gds
{
    class test
    {
        public static void Main(String[] args)
        {
            GDSWebSocketClient client = new GDSWebSocketClient("ws://127.0.0.1:8080/gate");
            client.ConnectSync();

            MessageHeader connectionMessageHeader = MessageManager.GetHeader("user", "870da92f-7fff-48af-825e-05351ef97acd", 1582612168230, 1582612168230, false, null, null, null, null, DataType.Connection);
            MessageData connectionMessageData = MessageManager.GetConnectionData(false, 1, false, null, "pass");
            Message connectionMessage = MessageManager.GetMessage(connectionMessageHeader, connectionMessageData);

            client.MessageReceived += Client_MessageReceived;

            client.SendAsync(connectionMessage);

            Tuple<Message, MessagePackSerializationException> connectionResponse = client.SendSync(connectionMessage, 3000);
            if (connectionResponse.Item2 == null)
            {
                Message connectionResponseMessage = connectionResponse.Item1;
                if (connectionResponseMessage.Header.DataType.Equals(DataType.ConnectionAck))
                {
                    ConnectionAckData connectionAckData = connectionResponseMessage.Data.AsConnectionAckData();
                }
            }

        }

        private static void Client_MessageReceived(object sender, Tuple<Message, MessagePackSerializationException> e)
        {
            Console.WriteLine(e.Item1.Header.DataType);
        }
    }
}
