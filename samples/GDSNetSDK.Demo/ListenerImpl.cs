using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Utils;
using log4net;
using log4net.Repository.Hierarchy;
using messages.Gds.Websocket;

namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Simple listener implementation that sends a complex INSERT with attachments
    /// and a SELECT query once the connection is established, and logs the acknowledgements.
    /// </summary>
    internal class ListenerImpl : IGDSMessageListener
    {
        private static readonly string OPTIONAL_EVENT_ID_SUFFIX = "_EVENT"; // Optional
        private static readonly string OPTIONAL_FRONT_ATTACHMENT_SUFFIX = "_FRONT"; // Optional
        private static readonly string OPTIONAL_REAR_ATTACHMENT_SUFFIX = "_REAR"; // Optional

        private readonly ILog _logger;
        private readonly string _sourceId;
        private readonly IdGenerator _idGenerator;
        private AsyncGDSClient? _client;

        private static readonly ConcurrentDictionary<string, string> requests = new ConcurrentDictionary<string, string>();

        public ListenerImpl(ILog logger, string sourceId, IdGenerator idGenerator)
        {
            _logger = logger;
            _sourceId = sourceId;
            _idGenerator = idGenerator;
        }

        public void AttachClient(AsyncGDSClient client)
        {
            _client = client;
        }

        public override void OnConnectionSuccess(MessageHeader header, ConnectionAckData data)
        {
            _logger.Info("Connection to GDS succeeded, login accepted.");
            base.OnConnectionSuccess(header, data);

            if (_client == null)
            {
                _logger.Error("Client reference is null in listener. Set it on the AttachClient method!");
                return;
            }

            try
            {
                // 1) Generate unique IDs for the event and its attachments
                string eventId = _idGenerator.Generate() + OPTIONAL_EVENT_ID_SUFFIX;
                string frontImageId = _idGenerator.Generate() + OPTIONAL_FRONT_ATTACHMENT_SUFFIX;
                string rearImageId = _idGenerator.Generate() + OPTIONAL_REAR_ATTACHMENT_SUFFIX;

                string frontImageIdHex = HexUtil.ToHexString(frontImageId);
                string rearImageIdHex = HexUtil.ToHexString(rearImageId);

                // 2) Example JSON payload describing the event
                string extraDataJson = @"{
  ""eventType"": ""demo-insert"",
  ""description"": ""Sample event with two PNG attachments from C# GDS SDK demo"",
  ""plateCountry"": ""HU"",
  ""confidence"": 0.99
}";
                string escapedExtraData = StringEscapeUtil.Escape(extraDataJson);

                // 3) Build the complex INSERT SQL for the main table + attachments
                string plate = "ABC123";
                string insertSql = SqlBuilder.BuildInsertWithAttachments(
                    eventId,
                    _sourceId,
                    plate,
                    escapedExtraData,
                    frontImageId,
                    frontImageIdHex,
                    rearImageId,
                    rearImageIdHex
                );

                // 4) Load binary image contents (same PNG used for both front and rear in this demo)
                Dictionary<string, byte[]> binaryContents =
                    AttachmentLoader.LoadDefaultDemoImages(frontImageIdHex, rearImageIdHex);

                _logger.Info("Sending INSERT event (SendEvent2) with two attachments...");
                _logger.Debug($"INSERT SQL:\n{insertSql}");

                string insertMessageId = Guid.NewGuid().ToString();
                requests.TryAdd(insertMessageId, eventId);
                _client.SendEvent2(insertSql, binaryContents, null, insertMessageId);

                // 5) Send a simple SELECT query
                string querySql = SqlBuilder.BuildSampleQuery();
                _logger.Info("Sending SELECT query (SendQueryRequest10)...");
                _logger.Debug($"QUERY SQL:\n{querySql}");

                string queryMessageId = Guid.NewGuid().ToString();
                requests.TryAdd(queryMessageId, queryMessageId);
                _client.SendQueryRequest10(querySql, ConsistencyType.NONE, null, null, null, queryMessageId);
            }
            catch (Exception ex)
            {
                _logger.Error("Error while building or sending demo INSERT/QUERY.", ex);
            }
        }

        public override void OnConnectionFailure(
            Either<Exception, KeyValuePair<MessageHeader, ConnectionAckData>> cause)
        {
            _logger.Error($"Connection to GDS failed. Cause: {cause}");
            if (cause != null)
            {
                if (cause.IsLeft)
                {
                    _logger.Error("Listener event: CONNECTION FAILURE occurred. See exception details.", cause.Left);
                }
                else
                {
                    _logger.Error(string.Format("Listener event: CONNECTION FAILURE occurred. Cause (from GDS Server): {0}", cause.Right.Value.Status));
                }
            }
            else
            {
                _logger.Error("Listener event: CONNECTION FAILURE occurred. Reason is unknown (cause is null).");
            }
        }

        public override void OnDisconnect()
        {
            _logger.Info("Disconnected from GDS.");
            _logger.Error("Listener event: OnDisconnect occurred - WEBSOCKET CLOSED.");
            base.OnDisconnect();
        }

        public override void OnEventAck3(MessageHeader header, EventAckData data)
        {
            _logger.Info("Listener event OnEventAck3 occurred.");
            string? outString;
            if (requests.ContainsKey(header.MessageId))
            {
                requests.TryGetValue(header.MessageId, out outString);
                _logger.Info("Insert response for request: " + header.MessageId + " record id: " + outString);
                requests.TryRemove(header.MessageId, out _);
            }
            if (data.Status != StatusCode.OK)
            {
                _logger.Warn("Insert failed with message id: " + header.MessageId);
                _logger.Warn(string.Format("Status Code: {0}, Reason: {1}", data.Status, data.Exception));
                return;
            }

            foreach (OperationResponse oneResponse in data.AckData)
            {
                _logger.Info("On a level the status: " + oneResponse.Status);
                foreach (SubResult subResult in oneResponse.SubResults)
                {
                    _logger.Info("For the record with id: " + subResult.Id + " was the status: " + subResult.SubStatus);
                }
            }
        }

        public override void OnEventDocument8(MessageHeader header, EventDocument data)
        {
            _logger.Info($"Listener event OnEventDocument8 arrived. MessageId: {header.MessageId}. Sending ACK9 answer with status OK.");

            if (_client != null)
            {
                try
                {
                    _client.SendEventDocumentAck9(StatusCode.OK,
                            new List<EventDocumentAckResult>()
                            {
                    new EventDocumentAckResult(StatusCode.OK, null)
                            },
                        null,
                        header.MessageId
                    );
                }
                catch (Exception ex)
                {
                    _logger.Error("ACK9 send error occurred!", ex);
                }
            }
            else
            {
                _logger.Error("GDS Client is null!");
            }
        }

        public override void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
        {
            if (data.Status != StatusCode.OK)
            {
                _logger.Warn("Select failed!");
                _logger.Warn(string.Format("Status Code: {0}, Reason: {1}", data.Status, data.Exception));
                return;
            }

            string queryMessageId = header.MessageId;
            if (requests.ContainsKey(queryMessageId))
            {
                _logger.Info("Response arraived for the query with id: " + queryMessageId);
                requests.TryRemove(queryMessageId, out _);
            }

            _logger.Info(string.Format("The client received {0} records.", data.AckData.NumberOfHits));

            if (0 < data.AckData.NumberOfHits)
            {
                _logger.Info($"Listener event OnQueryRequestAck11 occurred. No. of hits: {data.AckData.NumberOfHits}. First item: " +
                    $"field name = {data.AckData.FieldDescriptors[0].FieldName}, value: '{data.AckData.Records[0][0].ToString()}'");
            }

            // Example for requesting the next page (if you need pagination):
            // if (data.AckData.HasMorePage && _client != null)
            // {
            //     _logger.Info("Requesting next query page...");
            //     _client.SendMessage(
            //         MessageManager.GetHeader("user", DataType.NextQueryPageRequest),
            //         MessageManager.GetNextQueryPageRequest(data.AckData.QueryContextDescriptor, 10000L)
            //     );
            // }
        }

        public override void OnAttachmentRequest4(MessageHeader header, AttachmentRequestData data)
        {
            _logger.Info("Listener event OnAttachmentRequest4 occurred.");
        }

        public override void OnAttachmentRequestAck5(MessageHeader header, AttachmentRequestAckData data)
        {
            _logger.Info("Listener event OnAttachmentRequestAck5 occurred.");
        }

        public override void OnAttachmentResponse6(MessageHeader header, AttachmentResponseData data)
        {
            _logger.Info("Listener event OnAttachmentResponse6 occurred.");
        }

        public override void OnAttachmentResponseAck7(MessageHeader header, AttachmentResponseAckData data)
        {
            _logger.Info("Listener event OnAttachmentResponseAck7 occurred.");
        }

        public override void OnEventDocumentAck9(MessageHeader header, EventDocumentAck data)
        {
            _logger.Info("Listener event OnEventDocumentAck9 occurred.");
        }
    }
}
