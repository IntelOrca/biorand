using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IntelOrca.Biohazard.BioRand.Network
{
    public static class Program
    {
        public static async Task Main()
        {
            using ILoggerFactory loggerFactory =
                LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                    }));
            var logger = loggerFactory.CreateLogger("biorandd");
            using (var server = new BioRandServer(logger))
            {
                server.Listen(new IPEndPoint(IPAddress.Any, BioRandServer.DefaultPort));
                var end = false;
                System.Console.CancelKeyPress += (s, e) =>
                {
                    end = true;
                };
                while (!end)
                {
                    await Task.Delay(10);
                }
            }
        }
    }
}
