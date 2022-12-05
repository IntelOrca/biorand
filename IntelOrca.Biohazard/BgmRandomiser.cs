using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class BgmRandomiser
    {
#if !DEBUG
        private static BgmList? g_bgmList;
#endif
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

            RandomizeBasementTheme(random);
            RandomizeSaveTheme(random);
            RandomizeCreepyTheme(random, bgmList);
            RandomizeDangerTheme(random, bgmList);
            RandomizeResultsTheme(random, bgmList);
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
#if !DEBUG
            if (g_bgmList != null)
                return g_bgmList;
#endif

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

        public void RandomizeSaveTheme(Rng rng)
        {
            try
            {
                var names = new[] { "SAVE_RE0", "SAVE_RE1", "SAVE_RE3", "SAVE_RE4A", "SAVE_RE4B", "SAVE_CODE" };
                var resources = new[]
                {
                    Resources.bgm_re0,
                    Resources.bgm_re1,
                    Resources.bgm_re3,
                    Resources.bgm_re4a,
                    Resources.bgm_re4b,
                    Resources.bgm_code
                };
                var index = rng.Next(0, resources.Length);
                RandomizeFromOwnMusic("main0C", names[index], resources[index]);
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        public void RandomizeBasementTheme(Rng rng)
        {
            try
            {
                if (rng.Next(0, 4) == 0)
                {
                    RandomizeFromOwnMusic("main03", "clown", Resources.bgm_clown);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        public void RandomizeCreepyTheme(Rng rng, BgmList bgmList)
        {
            try
            {
                var resources = new[]
                {
                    Resources.bgm_re4_creepy_0,
                    Resources.bgm_re4_creepy_1,
                    Resources.bgm_re4_creepy_2,
                    Resources.bgm_re4_creepy_3
                };
                var creepy = bgmList.Creepy!
                    .Where(x => x.StartsWith("main", StringComparison.OrdinalIgnoreCase))
                    .Shuffle(rng)
                    .ToArray();
                for (int i = 0; i < resources.Length; i++)
                {
                    RandomizeFromOwnMusic(creepy[i], $"re4_creepy_{i}", resources[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        public void RandomizeDangerTheme(Rng rng, BgmList bgmList)
        {
            try
            {
                var resources = new[]
                {
                    Resources.bgm_re4_danger_0,
                    Resources.bgm_re4_danger_1,
                    Resources.bgm_re4_danger_2
                };
                var danger = bgmList.Danger!.Shuffle(rng).ToArray();
                for (int i = 0; i < resources.Length; i++)
                {
                    RandomizeFromOwnMusic(danger[i], $"re4_danger_{i}", resources[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        public void RandomizeResultsTheme(Rng rng, BgmList bgmList)
        {
            try
            {
                RandomizeFromOwnMusic("main2a", "re4_results", Resources.bgm_re4_results);
            }
            catch (Exception ex)
            {
                _logger.WriteException(ex);
            }
        }

        public void RandomizeFromOwnMusic(string fileName, string name, byte[] resource)
        {
            var ms = new MemoryStream(resource);
            using (var vorbis = new VorbisReader(ms))
            {
                var dst = Path.Combine(RngPath, @"Common\Sound\BGM", fileName + ".sap");
                using (var fs = new FileStream(dst, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    bw.Write((ulong)1);

                    // Write WAV header
                    var headerOffset = fs.Position;
                    bw.WriteASCII("RIFF");
                    bw.Write((uint)0);
                    bw.WriteASCII("WAVE");
                    bw.WriteASCII("fmt ");
                    bw.Write((uint)16);
                    bw.Write((ushort)1);
                    bw.Write((ushort)vorbis.Channels);
                    bw.Write((uint)vorbis.SampleRate);
                    bw.Write((uint)(vorbis.SampleRate * 16 * vorbis.Channels) / 8);
                    bw.Write((ushort)((16 * vorbis.Channels) / 8));
                    bw.Write((ushort)16);
                    bw.WriteASCII("data");
                    bw.Write((uint)0);

                    var dataOffset = fs.Position;

                    // Stream samples from ogg
                    int readSamples;
                    var readBuffer = new float[vorbis.Channels * vorbis.SampleRate / 8];
                    while ((readSamples = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        for (int i = 0; i < readSamples; i++)
                        {
                            var value = (short)(readBuffer[i] * short.MaxValue);
                            bw.Write(value);
                        }
                    }

                    // Fill in chunk lengths
                    var dataLength = fs.Length - dataOffset;
                    fs.Position = dataOffset - 4;
                    bw.Write((uint)dataLength);
                    fs.Position = headerOffset + 4;
                    bw.Write((uint)(fs.Length - 8));
                }
            }
            _logger.WriteLine($"Setting {fileName} to {name}");
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
