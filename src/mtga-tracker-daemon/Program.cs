using System;
using System.Runtime.InteropServices;

namespace MTGATrackerDaemon
{

    class Program
    {
        public const string BaseUrl = "http://localhost:";
        
        public const int DefaultPort = 37486;

        static void Main(string[] args)
        {
            int port = DefaultPort;

            int i = 0;
            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "-p":
                        if (++i < args.Length)
                        {
                            try
                            {
                                port = int.Parse(args[i]);
                            } 
                            catch (Exception)
                            {
                                Console.WriteLine("Port number format incorrect");
                                return;
                            }
                        }
                        else
                        {
                            DisplayUsageMessages();
                            return;
                        }
                        break;
                }
                i++;
            }

            HttpServer server = new HttpServer();
            server.Start(BaseUrl + port + "/");
        }

        private static void DisplayUsageMessages()
        {
            string usageMessage;
            string exampleMessage;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                usageMessage = "Usage: ./mtga-tracker-daemon.exe [-p PORT]";
                exampleMessage = "Example: ./mtga-tracker-daemon.exe -p 9000";
            }
            else 
            {
                usageMessage = "Usage: mtga-tracker-daemon [-p PORT]";
                exampleMessage = "Example: mtga-tracker-daemon -p 9000";
            }

            Console.WriteLine(usageMessage);
            Console.WriteLine(exampleMessage);
        }
    }
}
