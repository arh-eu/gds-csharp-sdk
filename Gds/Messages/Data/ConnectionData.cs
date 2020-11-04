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

using MessagePack;
using MessagePack.Formatters;
using System;

namespace Gds.Messages.Data
{
    /// <summary>
    /// The Connection type Data part of the Message.
    /// </summary>
    [MessagePackObject]
    public class ConnectionData : MessageData
    {
        [Key(0)]
        private readonly string clusterName;

        [Key(1)]
        private readonly bool serveOnTheSameConnection;

        [Key(2)]
        private readonly int protocolVersionNumber;

        [Key(3)]
        private readonly bool fragmentationSupported;

        [Key(4)]
        private readonly long? fragmentationTransmissionUnit;

        [Key(5)]
        private readonly object[] reservedFields;


        public ConnectionData(bool serveOnTheSameConnection, int protocolVersionNumber, bool fragmentationSupported, long? fragmentationTransmissionUnit, object[] reservedFields)
            : this(null, serveOnTheSameConnection, protocolVersionNumber, fragmentationSupported, fragmentationTransmissionUnit, reservedFields)
        {

        }

        public ConnectionData(bool serveOnTheSameConnection, int protocolVersionNumber, bool fragmentationSupported, long? fragmentationTransmissionUnit)
            : this(null, serveOnTheSameConnection, protocolVersionNumber, fragmentationSupported, fragmentationTransmissionUnit, null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionData"/> class
        /// </summary>
        /// <param name="clusterName">The name of the cluster the GDS instance is in.</param>
        /// <param name="serveOnTheSameConnection">If true, the clients only accepts the response on the same connection the message was sent (on the connection it established).</param>
        /// <param name="protocolVersionNumber">The version number of the protocol, with which the connected client communicates.</param>
        /// <param name="fragmentationSupported">If true, the client indicates that it accepts messages on this connection fragmented too.</param>
        /// <param name="fragmentationTransmissionUnit">If fragmentation is supported, it determines the size of chunks the other party should fragment the data part of the message.</param>
        /// <param name="reservedFields"></param>
        /// 
        public ConnectionData(string clusterName, bool serveOnTheSameConnection, int protocolVersionNumber, bool fragmentationSupported, long? fragmentationTransmissionUnit, object[] reservedFields)
        {
            this.clusterName = clusterName;
            this.serveOnTheSameConnection = serveOnTheSameConnection;
            this.protocolVersionNumber = protocolVersionNumber;
            this.fragmentationSupported = fragmentationSupported;
            this.fragmentationTransmissionUnit = fragmentationTransmissionUnit;
            this.reservedFields = reservedFields;
        }


        /// <summary>
        /// Returns the assigned GDS cluster.
        /// </summary>
        [IgnoreMember]
        public string ClusterName => clusterName;

        /// <summary>
        /// If true, the clients only accepts the response on the same connection the message was sent (on the connection it established).
        /// </summary>
        [IgnoreMember]
        public bool ServeOnTheSameConnection => serveOnTheSameConnection;

        /// <summary>
        /// The version number of the protocol, with which the connected client communicates.
        /// </summary>
        [IgnoreMember]
        public int ProtocolVersionNumber => protocolVersionNumber;

        /// <summary>
        /// If true, the client indicates that it accepts messages on this connection fragmented too.
        /// </summary>
        [IgnoreMember]
        public bool FragmentationSupported => fragmentationSupported;

        /// <summary>
        /// If fragmentation is supported, it determines the size of chunks the other party should fragment the data part of the message.
        /// </summary>
        [IgnoreMember]
        public long? FragmentationTransmissionUnit => fragmentationTransmissionUnit;

        [IgnoreMember]
        public object[] ReservedFields => reservedFields;

        /// <summary>
        /// For a password based authentication.
        /// </summary>
        [IgnoreMember]
        public string Password
        {
            get
            {
                if (reservedFields != null && reservedFields.Length >= 1)
                {
                    return (string)reservedFields[0];
                }
                else
                {
                    return null;
                }

            }
        }

        public override DataType GetDataType()
        {
            return DataType.Connection;
        }

        public override bool IsConnectionData()
        {
            return true;
        }

        public override ConnectionData AsConnectionData()
        {
            return this;
        }
    }


    public class ConnectionDataTypeFormatter : IMessagePackFormatter<ConnectionData>
    {
        public void Serialize(ref MessagePackWriter writer, ConnectionData value, MessagePackSerializerOptions options)
        {
            int length = 4;
            if (value.ClusterName != null) ++length;
            if (value.Password != null) ++length;
            writer.WriteArrayHeader(length);


            if (value.ClusterName != null)
            {
                writer.Write(value.ClusterName);
            }

            writer.Write(value.ServeOnTheSameConnection);
            writer.WriteInt32(value.ProtocolVersionNumber);
            writer.Write(value.FragmentationSupported);
            if (value.FragmentationTransmissionUnit != null)
            {
                writer.WriteInt64((long)value.FragmentationTransmissionUnit);
            }
            else
            {
                writer.WriteNil();
            }


            if (value.Password != null)
            {
                writer.WriteArrayHeader(1);
                writer.Write(value.Password);
            }
        }

        public ConnectionData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.IsNil)
            {
                reader.ReadNil();
                return null;
            }
            int size = reader.ReadArrayHeader();
            if (size < 4 || 6 < size)
            {
                throw new FormatException(string.Format("ConnectionData expected 4, 5 or 6 elements but found {0} instead!", size));
            }

            bool clusterIncluded = false;
            string clusterName = null;
            if (reader.NextMessagePackType == MessagePackType.String)
            {
                clusterName = reader.ReadString();
                clusterIncluded = true;
            }
            else if (reader.IsNil)
            {
                clusterIncluded = true;
                reader.ReadNil();
            }

            bool serveOnSame = reader.ReadBoolean();
            int protocol = reader.ReadInt32();
            bool fragment = reader.ReadBoolean();

            long? fragTransUnit = null;
            if (reader.IsNil)
            {
                reader.ReadNil();
            }
            else
            {
                fragTransUnit = reader.ReadInt64();
            }

            string[] reservedFields = null;
            if ((size == 5 && !clusterIncluded)|| size == 6)
            {
                if (reader.IsNil)
                {
                    reader.ReadNil();
                }
                else
                {
                    int innerSize = reader.ReadArrayHeader();
                    reservedFields = new string[innerSize];
                    for (int i = 0; i < innerSize; ++i)
                    {
                        if (reader.IsNil)
                        {
                            reader.ReadNil();
                        }
                        else
                        {
                            reservedFields[i] = reader.ReadString();
                        }
                    }
                }
            }
            return new ConnectionData(clusterName, serveOnSame, protocol, fragment, fragTransUnit, reservedFields);
        }
    }
}
