namespace messages.Gds.Websocket
{
    /// <summary>
    /// Class holding possible state values.
    /// This is a class instead of an enum to make the values useable with Interlocked.CompareExchange(..) 
    /// </summary>
    public sealed class ConnectionState
    {
        private ConnectionState() { }

        /// The client is instantiated but connect() was not yet called.
        public const int NOT_CONNECTED = 0;

        ///The connect() method was called on the client, the underlying channels are being created
        public const int INITIALIZING = 1;

        ///Channels successfully initialized, trying to establish the TCP/WebSocket Connection
        public const int CONNECTING = 2;

        /// The WebSocket connection (and TLS) is successfully established
        public const int CONNECTED = 3;

        /// The login message was successfully sent to the GDS
        public const int LOGGING_IN = 4;

        /// The login was successful. Client is ready to use
        public const int LOGGED_IN = 5;

        /// The connection was closed after a successful login (from either the client or the GDS side).
        public const int DISCONNECTED = 6;

        /// Error happened during the initialization of the client.
        public const int FAILED = 7;

        /// <summary>
        /// Returns a text representation for the given value
        /// </summary>
        /// <param name="value">The status code</param>
        /// <returns>The string version of it</returns>
        public static string AsText(int value)
        {
            return value switch
            {
                0 => "NOT_CONNECTED",
                1 => "INITIALIZING",
                2 => "CONNECTING",
                3 => "CONNECTED",
                4 => "LOGGING_IN",
                5 => "LOGGED_IN",
                6 => "DISCONNECTED",
                7 => "FAILED",
                _ => "",
            };
        }
    }
}
