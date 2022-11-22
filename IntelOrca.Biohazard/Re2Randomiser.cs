using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntelOrca.Biohazard
{
    public class Re2Randomiser : BaseRandomiser
    {
        protected override string GetPlayerName(int player) => player == 0 ? "Leon" : "Claire";

        public override bool ValidateGamePath(string path)
        {
            return Directory.Exists(Path.Combine(path, "data", "pl0")) ||
                Directory.Exists(Path.Combine(path, "pl0"));
        }

        protected override string GetDataPath(string installPath)
        {
            var originalDataPath = Path.Combine(installPath, "data");
            if (!Directory.Exists(originalDataPath))
            {
                originalDataPath = installPath;
            }
            return originalDataPath;
        }

        protected override string[] GetRdtPaths(string dataPath, int player)
        {
            var rdtPaths = new List<string>();
            var files = Directory.GetFiles(Path.Combine(dataPath, @$"Pl{player}\Rdt"));
            foreach (var file in files)
            {
                // Check the file is an RDT file
                var fileName = Path.GetFileName(file);
                if (!fileName.StartsWith("ROOM", System.StringComparison.OrdinalIgnoreCase) ||
                    !fileName.EndsWith(".RDT", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ignore RDTs that are not part of the main game
                if (!char.IsDigit(fileName[4]))
                    continue;

                rdtPaths.Add(file);
            }
            return rdtPaths.ToArray();
        }

        protected override Dictionary<RdtId, ulong> GetRdtChecksums(int player)
        {
            var checksumsForEachPlayer = JsonSerializer.Deserialize<Dictionary<string, ulong>[]>(Resources.checksum)!;
            var checksums = checksumsForEachPlayer[player];
            return checksums.ToDictionary(x => RdtId.Parse(x.Key), x => x.Value);
        }

        protected override void Generate(RandoConfig config, string installPath, string modPath)
        {
            var po = new ParallelOptions();
#if DEBUG
            po.MaxDegreeOfParallelism = 1;
#endif
            if (config.GameVariant == 0)
            {
                // Leon A / Claire B
                Parallel.Invoke(po,
                    () => base.GenerateRdts(config.WithPlayerScenario(0, 0), installPath, modPath),
                    () => base.GenerateRdts(config.WithPlayerScenario(1, 1), installPath, modPath));
            }
            else
            {
                // Leon B / Claire A
                Parallel.Invoke(po,
                    () => base.GenerateRdts(config.WithPlayerScenario(0, 1), installPath, modPath),
                    () => base.GenerateRdts(config.WithPlayerScenario(1, 0), installPath, modPath));
            }

            if (config.RandomBgm)
            {
                using var logger = new RandoLogger(Path.Combine(modPath, $"log_bgm.txt"));
                logger.WriteHeading("Resident Evil Randomizer");
                logger.WriteLine($"Seed: {config}");

                var bgmRandomiser = new BgmRandomiser(logger, installPath, modPath, @"Common\Sound\BGM", GetBgmJson(), new Rng(config.Seed));
            }

            RandoBgCreator.Save(config, modPath);
        }

        protected override string GetJsonMap()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\rdt.json");
            var jsonMap = File.ReadAllText(jsonPath);
#else
            var jsonMap = Resources.rdt;
#endif
            return jsonMap;
        }

        private static string GetBgmJson()
        {
#if DEBUG
            var jsonPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                @"..\..\..\..\IntelOrca.BioHazard\data\bgm.json");
            return File.ReadAllText(jsonPath);
#else
            return Resources.bgm;
#endif
        }
    }
}
