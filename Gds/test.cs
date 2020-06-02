using Gds.Messages;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Websocket;
using MessagePack;
using messages.Gds.Websocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace messages.Gds
{
    class test
    {
        public static void Main(String[] args)
        {
            var logRepository = log4net.LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly());    
            log4net.Config.BasicConfigurator.Configure(logRepository);

            GdsWebSocketClient client = new GdsWebSocketClient("ws://127.0.0.1:8080/gate", "user", null);
            client.MessageReceived += Client_MessageReceived;
            client.BinaryMessageReceived += Client_BinaryMessageReceived;

            client.Connect();

            
            MessageHeader eventMessageHeader = MessageManager.GetHeader("user", "c08ea082-9dbf-4d96-be36-4e4eab6ae624", 1582612168230, 1582612168230, false, null, null, null, null, DataType.Event);
            string operationsStringBlock = "INSERT INTO events (id, some_field, images) VALUES('EVNT202001010000000000', 'some_field', array('ATID202001010000000000'));INSERT INTO \"events-@attachment\" (id, meta, data) VALUES('ATID202001010000000000', 'some_meta', 0x62696e6172795f6964315f6578616d706c65)";
            Dictionary<string, byte[]> binaryContentsMapping = new Dictionary<string, byte[]> { { "62696e6172795f69645f6578616d706c65", new byte[] { 1, 2, 3 } } };
            MessageData eventMessageData = MessageManager.GetEventData(operationsStringBlock, binaryContentsMapping);
            Message eventMessage = MessageManager.GetMessage(eventMessageHeader, eventMessageData);


            client.SendAsync(eventMessage);


            Thread.Sleep(5000);

            client.Close();
        }

        static void Client_BinaryMessageReceived(object sender, byte[] e)
        {
           //...
        }

        private static void Client_MessageReceived(object sender, Tuple<Message, MessagePackSerializationException> e)
        {
            if (e.Item2 == null)
            {
                Message eventResponseMessage = e.Item1;
                if (eventResponseMessage.Header.DataType.Equals(DataType.EventAck))
                {
                    EventAckData eventAckData = eventResponseMessage.Data.AsEventAckData();
                    // do something with the event ack data...
                }
            }
        }
    }
}
