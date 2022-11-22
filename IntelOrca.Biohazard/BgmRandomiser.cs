using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class BgmRandomiser
    {
        public const string TagAlarm = "alarm";
        public const string TagAmbient = "ambient";
        public const string TagBasement = "basement";
        public const string TagCalm = "calm";
        public const string TagCreepy = "creepy";
        public const string TagDanger = "danger";
        public const string TagInstrument = "instrument";
        public const string TagOutside = "outside";
        public const string TagResults = "results";
        public const string TagSafe = "safe";

        private readonly RandoLogger _logger;
        private readonly Rng _rng;

        public string GamePath { get; }
        public string ModPath { get; }
        public string BgmSubDirectory { get; }
        public string BgmJson { get; }

        public bool IsWav { get; set; }

        public BgmRandomiser(RandoLogger logger, string gamePath, string modPath, string bgmSubDirectory, string bgmJson, Rng rng)
        {
            _logger = logger;
            _rng = rng;
            GamePath = gamePath;
            ModPath = modPath;
            BgmSubDirectory = bgmSubDirectory;
            BgmJson = bgmJson;
        }

        public void Randomise(Rng random)
        {
            _logger.WriteHeading("Shuffling BGM:");
            var bgmList = GetBtmList();
            Shuffle(bgmList, TagCreepy, TagCreepy);
            Shuffle(bgmList, TagCalm, TagCalm);
            Shuffle(bgmList, TagDanger, TagDanger);
            Shuffle(bgmList, TagAmbient, TagCreepy);
            Shuffle(bgmList, TagAlarm, TagCreepy);

            RandomizeBasementTheme(bgmList);
            RandomizeSaveTheme(bgmList);
            RandomizeCreepyTheme(bgmList);
            RandomizeDangerTheme(bgmList);
            RandomizeResultsTheme(bgmList);
        }

        private void Shuffle(BgmList bgmList, string dstTag, string srcTag)
        {
            var list = bgmList.GetList(dstTag);
            var shuffled = bgmList.GetList(srcTag).Shuffle(_rng);
            Swap(list, shuffled);
        }

        private void Swap(string[] dstList, string[] srcList)
        {
            var extension = IsWav ? ".wav" : ".sap";
            var srcDir = Path.Combine(GamePath, BgmSubDirectory);
            var dstDir = Path.Combine(ModPath, BgmSubDirectory);
            Directory.CreateDirectory(dstDir);
            for (int i = 0; i < dstList.Length; i++)
            {
                var src = Path.Combine(srcDir, srcList[i] + extension);
                var dst = Path.Combine(dstDir, dstList[i] + extension);
                File.Copy(src, dst, true);

                _logger.WriteLine($"Setting {dstList[i]} to {srcList[i]}");
            }
        }

        private BgmList GetBtmList()
        {
            var json = BgmJson;
            var dict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
            if (dict == null)
                throw new Exception();
            return new BgmList(dict);
        }

        private void RandomizeSaveTheme(BgmList bgmList)
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
            RandomizeTheme(bgmList, TagSafe, resources, names);
        }

        private void RandomizeBasementTheme(BgmList bgmList)
        {
            if (_rng.Next(0, 4) == 0)
            {
                RandomizeTheme(bgmList, TagBasement, new[] { Resources.bgm_clown });
            }
        }

        private void RandomizeCreepyTheme(BgmList bgmList)
        {
            RandomizeTheme(bgmList, TagCreepy, new[]
            {
                Resources.bgm_re4_creepy_0,
                Resources.bgm_re4_creepy_1,
                Resources.bgm_re4_creepy_2,
                Resources.bgm_re4_creepy_3
            });
        }

        private void RandomizeDangerTheme(BgmList bgmList)
        {
            RandomizeTheme(bgmList, TagDanger, new[]
            {
                Resources.bgm_re4_danger_0,
                Resources.bgm_re4_danger_1,
                Resources.bgm_re4_danger_2
            });
        }

        private void RandomizeResultsTheme(BgmList bgmList)
        {
            RandomizeTheme(bgmList, TagResults, new[]
            {
                Resources.bgm_re4_results
            });
        }

        private void RandomizeTheme(BgmList bgmList, string tag, byte[][] resources, string[]? names = null)
        {
            try
            {
                resources = resources.Shuffle(_rng);
                var samples = bgmList.GetList(tag);
                var shuffledSamples = samples
                    .Shuffle(_rng)
                    .ToArray();
                var min = Math.Min(shuffledSamples.Length, resources.Length);
                for (int i = 0; i < min; i++)
                {
                    RandomizeFromOwnMusic(shuffledSamples[i], names == null ? $"re4_{tag}_{i}" : names[i], resources[i]);
                }
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
                var extension = IsWav ? ".wav" : ".sap";
                var dst = Path.Combine(BgmSubDirectory, fileName + extension);
                using (var fs = new FileStream(dst, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    if (!IsWav)
                    {
                        bw.Write((ulong)1);
                    }

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

        private class BgmList
        {
            private Dictionary<string, string[]> _samples = new Dictionary<string, string[]>();

            public BgmList(Dictionary<string, string[]> samples)
            {
                _samples = samples;
            }

            public string[] GetList(string tag)
            {
                if (_samples.TryGetValue(tag, out var list))
                {
                    return list;
                }
                return new string[0];
            }
        }
    }
}
