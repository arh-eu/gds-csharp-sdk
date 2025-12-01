using log4net;
using log4net.Config;
using messages.Gds.Websocket;

namespace GDSNetSdk.InsertQueryDemo
{
    internal class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        private const string DefaultPrefix = "DEMO"; // 4-character prefix for generated IDs

        static void Main(string[] args)
        {
            // Configure log4net from config file
            XmlConfigurator.Configure(new FileInfo("log4net.config"));

            if (args.Length < 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  NetSdk.InsertQueryDemo <gdsUrl> <userName> <sourceId> <serve on the same connection (true|false)>");
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  NetSdk.InsertQueryDemo ws://127.0.0.1:8888/gate demoUser DEMO_SOURCE");
                return;
            }

            string gdsUrl = args[0];
            string userName = args[1];
            string sourceId = args[2];
            if (!bool.TryParse(args[3], out bool serveOnTheSameConnection))
            {
                Console.WriteLine("Invalid serve on the same connection. Please provide a bool (true|false).");
                return;
            }

            Logger.Info("Starting NetSdk.InsertQueryDemo sample...");
            Logger.Info($"GDS URL                      : {gdsUrl}");
            Logger.Info($"User name                    : {userName}");
            Logger.Info($"Source id                    : {sourceId}");
            Logger.Info($"Serve on the same connection : {serveOnTheSameConnection}");

            var idGenerator = new IdGenerator(DefaultPrefix);
            var listener = new ListenerImpl(Logger, sourceId, idGenerator);

            AsyncGDSClient client = AsyncGDSClient.GetBuilder()
                .WithListener(listener)
                .WithURI(gdsUrl)
                .WithUserName(userName)
                .WithPingPongInterval(30)
                .WithLog(Logger)
                .WithServeOnTheSameConnection(serveOnTheSameConnection)
                .Build();

            listener.AttachClient(client);

            Logger.Info("Connecting to GDS...");
            client.Connect();

            Logger.Info("Press ENTER to close the client DEMO application...");
            Console.ReadLine();

            Logger.Info("Closing client...");
            client.Close();
        }
    }
}
