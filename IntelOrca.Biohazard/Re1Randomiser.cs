using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace IntelOrca.Biohazard
{
    public class Re1Randomiser : BaseRandomiser
    {
        protected override string GetPlayerName(int player) => player == 0 ? "Chris" : "Jill";

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "JPN", "STAGE1")) ||
                Directory.Exists(Path.Combine(path, "STAGE1"));
        }

        protected override string GetDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "JPN");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = installPath;
            }
            return originalDataPath;
        }

        protected override string[] GetRdtPaths(string dataPath, int player)
        {
            var rdtPaths = new List<string>();
            for (int stage = 1; stage <= 7; stage++)
            {
                var files = Directory.GetFiles(Path.Combine(dataPath, @$"STAGE{stage}"));
                foreach (var file in files)
                {
                    // Check the file is an RDT file
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!char.IsDigit(fileName[7]))
                        continue;

                    var pl = fileName[7] - '0';
                    if (pl != player)
                        continue;

                    rdtPaths.Add(file);
                }
            }
            return rdtPaths.ToArray();
        }

        protected override void Generate(RandoConfig config, string installPath, string modPath)
        {
            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var bgmRandomiser = new BgmRandomiser(logger, installPath, modPath, @"sound", GetBgmJson(), new Rng(config.Seed));
                bgmRandomiser.IsWav = true;
            }
        }

        protected override string GetJsonMap()
        {
            throw new NotImplementedException();
        }

        private static string GetBgmJson()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\re1\re1_bgm.json");
            return File.ReadAllText(jsonPath);
#else
            return Resources.re1_bgm;
#endif
        }
    }
}
