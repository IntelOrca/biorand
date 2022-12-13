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
        public const string TagClown = "clown";
        public const string TagCreepy = "creepy";
        public const string TagDanger = "danger";
        public const string TagInstrument = "instrument";
        public const string TagOutside = "outside";
        public const string TagResults = "results";
        public const string TagSafe = "safe";
        public const string TagInstrumentBad = "instrument_bad";
        public const string TagInstrumentProgress = "instrument_progress";
        public const string TagInstrumentGood = "instrument_good";

        private readonly RandoLogger _logger;
        private readonly BgmList _srcBgmList = new BgmList();
        private readonly string _bgmJson;
        private readonly string _bgmDirectory;
        private readonly bool _isWav;
        private readonly Rng _rng;
        private readonly DataManager _dataManager;

        public float ImportVolume { get; set; } = 1.0f;

        public BgmRandomiser(RandoLogger logger, string bgmDirectory, string bgmJson, bool isWav, Rng rng, DataManager dataManager)
        {
            _logger = logger;
            _bgmJson = bgmJson;
            _bgmDirectory = bgmDirectory;
            _isWav = isWav;
            _rng = rng;
            _dataManager = dataManager;
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

            var tags = new[] { TagCreepy, TagCalm, TagDanger, TagInstrumentBad, TagInstrumentProgress, TagInstrumentGood };
            foreach (var tag in tags)
            {
                Shuffle(bgmList, tag, tag);
            }
            Shuffle(bgmList, TagAmbient, TagCreepy);
            Shuffle(bgmList, TagAlarm, TagCreepy);

            RandomizeTheme(bgmList, TagCreepy);
            RandomizeTheme(bgmList, TagDanger);
            RandomizeTheme(bgmList, TagResults);
            RandomizeTheme(bgmList, TagInstrumentBad, TagClown);
            RandomizeTheme(bgmList, TagSafe);
            if (_rng.Next(0, 4) == 0)
                RandomizeTheme(bgmList, TagBasement, TagClown);
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
                var dstName = dstList[i];
                if (dstName.StartsWith("!"))
                    continue;
                if (dstName.StartsWith("*"))
                    dstName = dstName.Substring(1);

                var src = srcList[i];
                var dst = Path.Combine(dstDir, dstName + extension);
                CopyMusicTrack(src, dst);

                _logger.WriteLine($"Setting {dstName} to {srcList[i]}");
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
                var decoder = new ADPCMDecoder();
                decoder.Volume = ImportVolume;
                decoder.Convert(src, dst);
            }
        }

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

        private void RandomizeTheme(BgmList bgmList, string tag) => RandomizeTheme(bgmList, tag, tag);

        private void RandomizeTheme(BgmList bgmList, string tag, string srcTag)
        {
            try
            {
                var files = _dataManager.GetBgmFiles(srcTag).Shuffle(_rng);
                var samples = bgmList.GetList(tag);
                var shuffledSamples = samples
                    .Where(x => !x.StartsWith("!"))
                    .Shuffle(_rng)
                    .ToArray();
                var min = Math.Min(shuffledSamples.Length, files.Length);
                for (int i = 0; i < min; i++)
                {
                    var resource = File.ReadAllBytes(files[i]);
                    var dst = shuffledSamples[i];
                    if (dst.StartsWith("*"))
                        dst = dst.Substring(1);
                    RandomizeFromOwnMusic(dst, files[i], resource);
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
                        if (ImportVolume == 1)
                        {
                            for (int i = 0; i < readSamples; i++)
                            {
                                var value = (short)(readBuffer[i] * short.MaxValue);
                                bw.Write(value);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < readSamples; i++)
                            {
                                var value = (short)(readBuffer[i] * short.MaxValue * ImportVolume);
                                bw.Write(value);
                            }
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
                    foreach (var item in kvp.Value)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            list.Add(item);
                        }
                    }
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
                        var name = list[i];
                        if (name.Length > 0)
                        {
                            if (name[0] == '*')
                            {
                                list[i] = "";
                            }
                            else
                            {
                                if (name[0] == '!')
                                    name = name.Substring(1);
                                list[i] = Path.Combine(bgmSubDirectory, name) + extension;
                            }
                        }
                    }
                }
            }
        }
    }
}
