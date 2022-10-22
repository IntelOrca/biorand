using System;
using System.IO;
using System.Text.Json;

namespace rer
{
    internal class BgmRandomiser
    {
        private readonly RandoLogger _logger;

        public string GamePath { get; }
        public string RngPath { get; }

        public BgmRandomiser(RandoLogger logger, string gamePath, string rngPath)
        {
            _logger = logger;
            GamePath = gamePath;
            RngPath = rngPath;
        }

        public void Randomise(Rng random)
        {
            var json = File.ReadAllText(@"M:\git\rer\rer\data\bgm.json");
            var bgmList = JsonSerializer.Deserialize<BgmList>(json, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (bgmList == null)
                throw new Exception();

            _logger.WriteHeading("Shuffling BGM:");
            Swap(bgmList.Creepy!, bgmList.Creepy!.Shuffle(random));
            Swap(bgmList.Calm!, bgmList.Calm!.Shuffle(random));
            Swap(bgmList.Danger!, bgmList.Danger!.Shuffle(random));
            Swap(bgmList.Ambient!, bgmList.Creepy!.Shuffle(random));
        }

        private void Swap(string[] dstList, string[] srcList)
        {
            var srcDir = Path.Combine(GamePath, @"Common\Sound\BGM");
            var dstDir = Path.Combine(RngPath, @"Common\Sound\BGM");
            Directory.CreateDirectory(dstDir);
            for (int i = 0; i < dstList.Length; i++)
            {
                var src = Path.Combine(srcDir, srcList[i] + ".sap");
                var dst = Path.Combine(dstDir, dstList[i] + ".sap");
                File.Copy(src, dst, true);

                _logger.WriteLine($"Setting {dstList[i]} to {srcList[i]}");
            }
        }

        private static string FixSubFilename(string s)
        {
            if (s.StartsWith("SUB", StringComparison.OrdinalIgnoreCase))
                return "SUB_" + s.Substring(3);
            return s;
        }

        public class BgmList
        {
            public string[]? Outside { get; set; }
            public string[]? Creepy { get; set; }
            public string[]? Calm { get; set; }
            public string[]? Danger { get; set; }
            public string[]? Ambient { get; set; }
        }
    }
}
