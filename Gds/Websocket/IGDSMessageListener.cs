using Gds.Messages.Data;
using Gds.Messages.Header;
using Gds.Utils;
using System;
using System.Collections.Generic;

namespace messages.Gds.Websocket
{
    /// <summary>
    /// Abstract class used as a Message Listener (callback) for the GDS communication.
    /// The methods are not mandatory to be overwritten, but if they are called (that kind of message is received)
    /// without an implementation in the subclass most of them will throw a NotImplementedException.
    /// </summary>
    public abstract class IGDSMessageListener
    {
        /// <summary>
        /// Called upon successfully establishing a connection (with accepted login) towards the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received login data</param>
        public virtual void OnConnectionSuccess(MessageHeader header, ConnectionAckData data) { }

        /// <summary>
        /// Called if the connection (or the login) is unsuccessful.
        /// The error is either caused by an exception or a login failure, this can be retreived from the parameter.
        /// </summary>
        /// <param name="cause">The cause of the failure</param>
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
        /// <param name="header">The received header</param>
        /// <param name="data">The received event ACK</param>
        public virtual void OnEventAck3(MessageHeader header, EventAckData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Request from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received attachment request</param>
        public virtual void OnAttachmentRequest4(MessageHeader header, AttachmentRequestData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Request ACK from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received attachment request ack</param>
        public virtual void OnAttachmentRequestAck5(MessageHeader header, AttachmentRequestAckData data)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Called upon receiving an Attachment Responsefrom the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received attachment response</param>
        public virtual void OnAttachmentResponse6(MessageHeader header, AttachmentResponseData data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Attachment Response ACK from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received attachment response ACK</param>
        public virtual void OnAttachmentResponseAck7(MessageHeader header, AttachmentResponseAckData data)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Called upon receiving an Event Document from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received event document</param>
        public virtual void OnEventDocument8(MessageHeader header, EventDocument data)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Called upon receiving an Event Document ACK from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received event document ACK</param>
        public virtual void OnEventDocumentAck9(MessageHeader header, EventDocumentAck data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called upon receiving an Query Request ACK from the GDS.
        /// </summary>
        /// <param name="header">The received header</param>
        /// <param name="data">The received query request ACK</param>
        public virtual void OnQueryRequestAck11(MessageHeader header, QueryRequestAckData data)
        {
            throw new NotImplementedException();
        }
    }
}
