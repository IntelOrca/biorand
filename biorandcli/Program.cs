using System;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand.Cli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            var reProcess = ReProcess.Find();
            if (reProcess == null)
            {
                Console.Error.WriteLine("RE 2 process not found");
                return;
            }

            var apClient = new ApClient(reProcess, "localhost", 38281);
            await apClient.ConnectAsync();
            await apClient.Login("IntelOrca");
            await apClient.RunAsync();
        }
    }
}
