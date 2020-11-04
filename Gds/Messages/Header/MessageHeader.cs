/*
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

using Gds.Messages.Data;

namespace Gds.Messages.Header
{
    /// <summary>
    /// The Header part of the Message.
    /// </summary>
    public class MessageHeader
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class
        /// </summary>
        /// <param name="userName">The name of the user.</param>
        /// <param name="messageId">The identifier of the message, with which the request can be associated.</param>
        /// <param name="createTime">The time of creating the message, epoch timestamp.</param>
        /// <param name="requestTime">The time of the request, epoch timestamp.</param>
        /// <param name="isFragmented">Whether the current message is fragmented or not.</param>
        /// <param name="firstFragment">Whether it's the first fragment of a fragmented message.</param>
        /// <param name="lastFragment">Whether it's the last fragment of a fragmented message.</param>
        /// <param name="offset">The last byte of the original, fragmented message the receiver has yet received or the last byte of the original message that has yet been forwarded.</param>
        /// <param name="fullDataSize">The size of the full data in bytes.</param>
        /// <param name="dataType">It specifies what types of information the data carries.</param>
        public MessageHeader(string userName, string messageId, long createTime, long requestTime, bool isFragmented, bool? firstFragment, bool? lastFragment, int? offset, int? fullDataSize, DataType dataType)
        {
            this.UserName = userName;
            this.MessageId = messageId;
            this.CreateTime = createTime;
            this.RequestTime = requestTime;
            this.IsFragmented = isFragmented;
            this.FirstFragment = firstFragment;
            this.LastFragment = lastFragment;
            this.Offset = offset;
            this.FullDataSize = fullDataSize;
            this.DataType = dataType;
        }

        /// <summary>
        /// The name of the user.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// The identifier of the message, with which the request can be associated.
        /// </summary>
        public string MessageId { get; }

        /// <summary>
        /// The time of creating the message, epoch timestamp.
        /// </summary>
        public long CreateTime { get; }

        /// <summary>
        /// The time of the request, epoch timestamp.
        /// </summary>
        public long RequestTime { get; }

        /// <summary>
        /// Whether the current message is fragmented or not.
        /// </summary>
        public bool IsFragmented { get; }

        /// <summary>
        /// Whether it's the first fragment of a fragmented message.
        /// </summary>
        public bool? FirstFragment { get; }

        /// <summary>
        /// Whether it's the last fragment of a fragmented message.
        /// </summary>
        public bool? LastFragment { get; }

        /// <summary>
        /// The last byte of the original, fragmented message the receiver has yet received or the last byte of the original message that has yet been forwarded.
        /// </summary>
        public int? Offset { get; }

        /// <summary>
        /// The size of the full data in bytes.
        /// </summary>
        public int? FullDataSize { get; }

        /// <summary>
        /// It specifies what types of information the data carries.
        /// </summary>
        public DataType DataType { get; }
    }
}
