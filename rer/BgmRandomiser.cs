using System;
using System.IO;
using System.Text.Json;

namespace rer
{
    internal class BgmRandomiser
    {
        private static BgmList? g_bgmList;
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
            _logger.WriteHeading("Shuffling BGM:");
            var bgmList = GetBtmList();
            Swap(bgmList.Creepy!, bgmList.Creepy!.Shuffle(random));
            Swap(bgmList.Calm!, bgmList.Calm!.Shuffle(random));
            Swap(bgmList.Danger!, bgmList.Danger!.Shuffle(random));
            Swap(bgmList.Ambient!, bgmList.Creepy!.Shuffle(random));
            Swap(bgmList.Alarm!, bgmList.Creepy!.Shuffle(random));
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

        private static BgmList GetBtmList()
        {
            if (g_bgmList == null)
            {
                var json = GetBgmJson();
                var bgmList = JsonSerializer.Deserialize<BgmList>(json, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (bgmList == null)
                    throw new Exception();
#if !DEBUG
                g_bgmList = bgmList;
#endif
                return bgmList;
            }
            return g_bgmList;
        }

        private static string GetBgmJson()
        {
#if DEBUG
            return File.ReadAllText(@"M:\git\rer\rer\data\bgm.json");
#else
            return Resources.bgm;
#endif
        }

        public class BgmList
        {
            public string[]? Outside { get; set; }
            public string[]? Creepy { get; set; }
            public string[]? Calm { get; set; }
            public string[]? Danger { get; set; }
            public string[]? Ambient { get; set; }
            public string[]? Alarm { get; set; }
        }
    }
}
