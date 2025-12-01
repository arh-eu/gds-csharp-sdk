using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Utils;
using log4net;
using messages.Gds.Websocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDSNetSDK.InsertQueryDemo
{
    /// <summary>
    /// Wrapper around AsyncGDSClient that:
    /// - exposes Task-based APIs per request (awaitable),
    /// - ensures every request has a timeout,
    /// - guarantees pending dictionaries are cleaned up on completion / timeout / connection failure.
    /// </summary>
    public class SyncGDSClient : IGDSMessageListener, IDisposable
    {
        // Pending requests keyed by messageId
        private readonly ConcurrentDictionary<string, TaskCompletionSource<MessageData>> _pendingMessages
            = new();

        private AsyncGDSClient _client;
        private readonly ILog _log;
        private bool _disposed;

        private SyncGDSClient(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Factory method to create the wrapper and the underlying AsyncGDSClient.
        /// </summary>
        public static SyncGDSClient Create(
            string gdsUrl,
            string userName,
            string password,
            int pingPongIntervalSeconds,
            ILog log,
            bool serveOnSameConnection = false)
        {
            if (gdsUrl == null) throw new ArgumentNullException(nameof(gdsUrl));
            if (userName == null) throw new ArgumentNullException(nameof(userName));
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (log == null) throw new ArgumentNullException(nameof(log));

            // Temporary instance for listener wiring
            SyncGDSClient wrapper = new SyncGDSClient(log);

            AsyncGDSClient client = AsyncGDSClient.GetBuilder()
                .WithListener(wrapper)
                .WithURI(gdsUrl)
                .WithUserName(userName)
                .WithPingPongInterval(pingPongIntervalSeconds)
                .WithServeOnTheSameConnection(serveOnSameConnection)
                .WithLog(log)
                .Build();

            wrapper._client = client;

            return wrapper;
        }

        public void Connect() => _client.Connect();

        public void Close() => _client.Close();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _client.Close(); } catch { /* ignore */ }
        }

        // --------------------------------------------------------------------
        // PUBLIC FEATURE-LEVEL API
        // --------------------------------------------------------------------

        //public void SendEvent2(string operationsStringBlock, Dictionary<string, byte[]> binaryContentsMapping = null,
        //    List<List<Dictionary<int, bool>>> executionPriorityStructure = null, string messageID = null, MessageHeader header = null)
        //{

        /// <summary>
        /// Sends an INSERT / EVENT statement with optional attachments and
        /// returns a Task that completes when the EventAckData arrives
        /// or throws TimeoutException / GdsRequestException.
        /// </summary>
        public async Task<EventAckData> SendEvent2Async(
            string operationsStringBlock,
            TimeSpan timeout,
            Dictionary<string, byte[]>? binaryContentsMapping = null,
            List<List<Dictionary<int, bool>>>? executionPriorityStructure = null, 
            string? messageId = null, 
            MessageHeader? header = null)
        {
            if (operationsStringBlock == null) throw new ArgumentNullException(nameof(operationsStringBlock));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            // TaskCompletionSource for this particular request
            var tcs = new TaskCompletionSource<MessageData>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (null == messageId)
            {
                if (null == header || null == header.MessageId)
                {
                    messageId = Guid.NewGuid().ToString();
                } else
                {
                    messageId = header.MessageId;
                }
            }
            

            try
            {
                // Send the request and obtain the messageId
                _client.SendEvent2(operationsStringBlock, binaryContentsMapping, executionPriorityStructure, messageId, header);
            }
            catch (Exception sendEx)
            {
                // If send fails, we don't add anything to the dictionary
                throw new GdsRequestException("Failed to send event to GDS.", sendEx);
            }

            if (!_pendingMessages.TryAdd(messageId, tcs))
            {
                throw new InvalidOperationException($"Duplicate messageId for event: {messageId}");
            }

            try
            {
                // Create a timeout task
                var timeoutTask = Task.Delay(timeout);

                // Wait for either the EventAck or the timeout
                var completed = await Task.WhenAny(tcs.Task, timeoutTask)
                                          .ConfigureAwait(false);

                // Clean up from the dictionary in all cases
                _pendingMessages.TryRemove(messageId, out _);

                if (completed == timeoutTask)
                {
                    // We timed out before the listener completed the TCS
                    throw new TimeoutException(
                        $"Event ACK did not arrive in time (messageId={messageId}, timeout={timeout}).");
                }

                // Await the actual result (this will rethrow if listener set an exception)
                return (await tcs.Task.ConfigureAwait(false)).AsEventAckData();
            }
            catch
            {
                // Extra safety: ensure it's not left behind
                _pendingMessages.TryRemove(messageId, out _);
                throw;
            }
        }

        /// <summary>
        /// Blocking (synchronous) wrapper around SendEventAsync.
        /// </summary>
        public EventAckData SendEvent2Sync(
            string sql,
            TimeSpan timeout,
            Dictionary<string, byte[]>? binaryContents = null,
            List<List<Dictionary<int, bool>>>? executionPriorityStructure = null,
            string? messageId = null,
            MessageHeader? header = null)
        {
            return SendEvent2Async(sql, timeout, binaryContents, executionPriorityStructure, messageId, header)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Sends a SELECT query and returns a Task that completes when the
        /// QueryRequestAckData arrives or throws TimeoutException / GdsRequestException.
        /// </summary>
        public async Task<QueryRequestAckData> SendQueryAsync(
            string selectStringBlock,
            ConsistencyType consistencyTime,
            TimeSpan timeout,
            int? queryPageSize = null,
            QueryType? queryType = null,
            string? messageId = null,
            MessageHeader? header = null
            )
        {
            if (selectStringBlock == null) throw new ArgumentNullException(nameof(selectStringBlock));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            var tcs = new TaskCompletionSource<MessageData>(
                TaskCreationOptions.RunContinuationsAsynchronously);


            if (null == messageId)
            {
                if (null == header || null == header.MessageId)
                {
                    messageId = Guid.NewGuid().ToString();
                }
                else
                {
                    messageId = header.MessageId;
                }
            }

            try
            {
                _client.SendQueryRequest10(selectStringBlock, consistencyTime, (long)timeout.TotalMilliseconds, queryPageSize, queryType, messageId, header);
            }
            catch (Exception sendEx)
            {
                throw new GdsRequestException("Failed to send query to GDS.", sendEx);
            }

            if (!_pendingMessages.TryAdd(messageId, tcs))
            {
                throw new InvalidOperationException($"Duplicate messageId for query: {messageId}");
            }

            try
            {
                var timeoutTask = Task.Delay(timeout);

                var completed = await Task.WhenAny(tcs.Task, timeoutTask)
                                          .ConfigureAwait(false);

                _pendingMessages.TryRemove(messageId, out _);

                if (completed == timeoutTask)
                {
                    throw new TimeoutException(
                        $"Query ACK did not arrive in time (messageId={messageId}, timeout={timeout}).");
                }

                return (await tcs.Task.ConfigureAwait(false)).AsQueryRequestAck();
            }
            catch
            {
                _pendingMessages.TryRemove(messageId, out _);
                throw;
            }
        }

        /// <summary>
        /// Blocking (synchronous) wrapper around SendQueryAsync.
        /// </summary>
        public QueryRequestAckData SendQuery(
            string querySql,
            ConsistencyType consistency,
            TimeSpan timeout,
            int? queryPageSize = null,
            QueryType? queryType = null,
            string? messageId = null,
            MessageHeader? header = null)
        {
            return SendQueryAsync(querySql, consistency, timeout, queryPageSize, queryType, messageId, header)
                .GetAwaiter()
                .GetResult();
        }

        // --------------------------------------------------------------------
        // LISTENER IMPLEMENTATION – DISPATCH RESPONSES BACK TO TCS
        // --------------------------------------------------------------------

        public override void OnConnectionSuccess(MessageHeader header, ConnectionAckData data)
        {
            _log.Info("Connection established (GdsClientWrapper).");
        }

        public override void OnConnectionFailure(
            Either<Exception, KeyValuePair<MessageHeader, ConnectionAckData>> cause)
        {
            _log.Error($"Connection to GDS failed. Cause: {cause}");

            var ex = new GdsRequestException("GDS connection failure: " + cause);

            // Fail all pending requests and clean up
            foreach (var kv in _pendingMessages)
            {
                kv.Value.TrySetException(ex);
            }

            _pendingMessages.Clear();
        }

        public override void OnDisconnect()
        {
            _log.Info("Disconnected from GDS (GdsClientWrapper).");
        }

        public override void OnEventAck3(MessageHeader header, EventAckData data)
        {
            string messageId = header.MessageId;

            _log.Debug($"OnEventAck3 received for messageId={messageId}, status={data.Status}");

            if (_pendingMessages.TryRemove(messageId, out var tcs))
            {
                tcs.TrySetResult(data);  
            }
            else
            {
                _log.Warn($"Received EventAck3 for unknown or already timed-out messageId={messageId}");
            }
        }

        public override void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
        {
            string messageId = header.MessageId;

            _log.Debug($"OnQueryRequestAck11 received for messageId={messageId}, status={data.Status}");

            if (_pendingMessages.TryRemove(messageId, out var tcs))
            {
                tcs.TrySetResult(data);
            }
            else
            {
                _log.Warn($"Received QueryRequestAck11 for unknown or already timed-out messageId={messageId}");
            }
        }
    }


    /// <summary>
    /// Simple exception type for failed GDS requests.
    /// </summary>
    public class GdsRequestException : Exception
        {
            public GdsRequestException(string message) : base(message) { }
            public GdsRequestException(string message, Exception inner) : base(message, inner) { }
        }
    }
}
