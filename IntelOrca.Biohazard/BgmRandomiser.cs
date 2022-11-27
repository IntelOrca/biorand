using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NVorbis;

namespace IntelOrca.Biohazard
{
    public class BgmRandomiser
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
        private readonly BgmList _srcBgmList = new BgmList();
        private readonly string _bgmJson;
        private readonly string _bgmDirectory;
        private readonly bool _isWav;
        private readonly Rng _rng;

        public BgmRandomiser(RandoLogger logger, string bgmDirectory, string bgmJson, bool isWav, Rng rng)
        {
            _logger = logger;
            _bgmJson = bgmJson;
            _bgmDirectory = bgmDirectory;
            _isWav = isWav;
            _rng = rng;
        }

        public void AddToSelection(string bgmJson, string bgmSubDirectory, string extension)
        {
            var bgmList = GetBtmList(bgmJson);
            bgmList.MakeFullPath(bgmSubDirectory, extension);
            _srcBgmList.Union(bgmList);
        }

        public void Randomise()
        {
            _logger.WriteHeading("Shuffling BGM:");
            var bgmList = GetBtmList(_bgmJson);
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
            var dstList = bgmList.GetList(dstTag);
            var srcList = _srcBgmList
                .GetList(srcTag)
                .Shuffle(_rng);
            var extension = _isWav ? ".wav" : ".sap";
            var dstDir = _bgmDirectory;
            Directory.CreateDirectory(dstDir);
            for (int i = 0; i < dstList.Count; i++)
            {
                var src = srcList[i];
                var dst = Path.Combine(dstDir, dstList[i] + extension);
                CopyMusicTrack(src, dst);

                _logger.WriteLine($"Setting {dstList[i]} to {srcList[i]}");
            }
        }

        private void CopyMusicTrack(string src, string dst)
        {
            var srcExtension = Path.GetExtension(src);
            var dstExtension = Path.GetExtension(dst);
            if (string.Equals(srcExtension, dstExtension, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(src, dst, true);
            }
            else if (srcExtension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                var srcBytes = File.ReadAllBytes(src);
                using (var fs = new FileStream(dst, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    bw.Write((ulong)1);
                    bw.Write(srcBytes);
                }
            }
            else if (srcExtension.Equals(".sap", StringComparison.OrdinalIgnoreCase))
            {
#if USE_FFMPEG
                RunFFMPEG(src, dst);
#else
                var srcBytes = File.ReadAllBytes(src);
                File.WriteAllBytes(dst, srcBytes.Skip(8).ToArray());
#endif
            }
        }

#if USE_FFMPEG
        private bool RunFFMPEG(string src, string dst)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var srcBytes = File.ReadAllBytes(src);
                File.WriteAllBytes(tempFile, srcBytes.Skip(8).ToArray());

                var ffmpegPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "ffmpeg.exe");
                var psi = new ProcessStartInfo()
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{tempFile}\" -acodec pcm_s16le \"{dst}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }
#endif

        private BgmList GetBtmList(string bgmJson)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(bgmJson, new JsonSerializerOptions()
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

        private void RandomizeFromOwnMusic(string fileName, string name, byte[] resource)
        {
            var ms = new MemoryStream(resource);
            using (var vorbis = new VorbisReader(ms))
            {
                var extension = _isWav ? ".wav" : ".sap";
                var dst = Path.Combine(_bgmDirectory, fileName + extension);
                using (var fs = new FileStream(dst, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    if (!_isWav)
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
            private Dictionary<string, List<string>> _samples = new Dictionary<string, List<string>>();

            public BgmList()
            {
            }

            public BgmList(Dictionary<string, string[]> samples)
            {
                _samples = samples
                    .ToDictionary(x => x.Key, x => x.Value.ToList());
            }

            public void Union(BgmList other)
            {
                foreach (var kvp in other._samples)
                {
                    var list = GetList(kvp.Key);
                    list.AddRange(kvp.Value);
                }
            }

            public List<string> GetList(string tag)
            {
                if (_samples.TryGetValue(tag, out var list))
                {
                    return list;
                }

                list = new List<string>();
                _samples.Add(tag, list);
                return list;
            }

            public void MakeFullPath(string bgmSubDirectory, string extension)
            {
                foreach (var kvp in _samples)
                {
                    var list = kvp.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        list[i] = Path.Combine(bgmSubDirectory, list[i]) + extension;
                    }
                }
            }
        }
    }
}
