using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IntelOrca.Biohazard
{
    internal class BgmRandomiser
    {
        public const string TagBasement = "basement";
        public const string TagCalm = "calm";
        public const string TagClown = "clown";
        public const string TagCreepy = "creepy";
        public const string TagCountdown = "countdown";
        public const string TagDanger = "danger";
        public const string TagInstrument = "instrument";
        public const string TagOutside = "outside";
        public const string TagResults = "results";
        public const string TagSafe = "safe";
        public const string TagInstrumentBad = "instrument_bad";
        public const string TagInstrumentProgress = "instrument_progress";
        public const string TagInstrumentGood = "instrument_good";

        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly BgmList _srcBgmList = new BgmList();
        private readonly string _bgmJson;
        private readonly string _bgmDirectory;
        private readonly bool _isWav;
        private readonly Rng _rng;
        private readonly DataManager _dataManager;

        public float ImportVolume { get; set; } = 1.0f;

        public BgmRandomiser(RandoLogger logger, RandoConfig config, string bgmDirectory, string bgmJson, bool isWav, Rng rng, DataManager dataManager)
        {
            _logger = logger;
            _config = config;
            _bgmJson = bgmJson;
            _bgmDirectory = bgmDirectory;
            _isWav = isWav;
            _rng = rng;
            _dataManager = dataManager;
        }

        public void AddToSelection(string bgmJson, string bgmSubDirectory, string extension)
        {
            var bgmList = GetBgmList(bgmJson);
            bgmList.MakeFullPath(bgmSubDirectory, extension);
            _srcBgmList.Union(bgmList);
        }

        public void AddCutomMusicToSelection(RandoConfig config)
        {
            var bgmList = new BgmList();
            var directories = _dataManager.GetDirectoriesIn("bgm");
            foreach (var dir in directories)
            {
                bool good = config.IncludeBGMOther;
                if (!good)
                {
                    var d = Path.GetFileName(dir);
                    if (d == "re1" && config.IncludeBGMRE1)
                        good = true;
                    if (d == "re2" && config.IncludeBGMRE2)
                        good = true;
                    // if (d == "re3" && config.IncludeBGMRE3)
                    //     good = true;
                    // if (d == "re4" && config.IncludeBGMRE4)
                    //     good = true;
                }
                if (good)
                    AddCustomMusicToSelection(bgmList, dir);
            }
            _srcBgmList.Union(bgmList);
        }

        private void AddCustomMusicToSelection(BgmList bgmList, string directory)
        {
            var tags = _dataManager.GetDirectoriesIn(directory);
            foreach (var tagPath in tags)
            {
                var tag = Path.GetFileName(tagPath);
                var files = Directory.GetFiles(tagPath)
                    .Where(x => WaveformBuilder.IsSupportedExtension(x))
                    .ToArray();
                foreach (var file in files)
                {
                    bgmList.Add(tag, file);
                }
            }
        }

        public void Randomise()
        {
            _logger.WriteHeading("Shuffling BGM:");
            var bgmList = GetBgmList(_bgmJson);

            // Ensure these are randomized, but otherwise give custom priority
            Shuffle(bgmList, TagBasement, TagCreepy);
            Shuffle(bgmList, TagCountdown, TagCreepy);
            Shuffle(bgmList, TagSafe, TagCalm);
            Shuffle(bgmList, TagResults, TagCalm);

            // Shuffle tracks in each tag
            foreach (var tag in bgmList.Tags)
            {
                // Countdown only really works in a normal run
                if (tag == TagCountdown && _config.RandomDoors)
                    continue;

                Shuffle(bgmList, tag, tag);
            }

            // Clown music
            Shuffle(bgmList, TagInstrumentBad, TagClown, true);
            if (_rng.Next(0, 4) == 0)
            {
                Shuffle(bgmList, TagBasement, TagClown, true);
            }
        }

        private void Shuffle(BgmList bgmList, string dstTag, string srcTag, bool overlay = false)
        {
            var dstList = bgmList
                .GetList(dstTag)
                .ToArray();
            var srcList = _srcBgmList
                .GetList(srcTag)
                .ToEndlessBag(_rng);

            if (srcList.Count == 0)
                return;

            if (overlay)
            {
                dstList = dstList
                    .Shuffle(_rng)
                    .Take(srcList.Count)
                    .ToArray();
            }

            var extension = _isWav ? ".wav" : ".sap";
            var dstDir = _bgmDirectory;
            Directory.CreateDirectory(dstDir);
            for (int i = 0; i < dstList.Length; i++)
            {
                var dstName = dstList[i];
                if (dstName.StartsWith("!"))
                    continue;
                if (dstName.StartsWith("*"))
                    dstName = dstName.Substring(1);

                var src = srcList.Next();
                if (Path.GetFileNameWithoutExtension(src) == dstName)
                {
                    src = srcList.Next();
                    if (Path.GetFileNameWithoutExtension(src) == dstName)
                    {
                        continue;
                    }
                }

                var dst = Path.Combine(dstDir, dstName + extension);
                SetMusicTrack(src, dst);

                _logger.WriteLine($"Setting {dstName} to {src}");
            }
        }

        private void SetMusicTrack(string src, string dst)
        {
            var srcExtension = Path.GetExtension(src);
            var dstExtension = Path.GetExtension(dst);
            if (string.Equals(srcExtension, dstExtension, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(src, dst, true);
            }
            else
            {
                var builder = new WaveformBuilder();
                builder.Volume = ImportVolume;
                if (_isWav)
                {
                    // RE1 can't handle very large .wav files, limit tracks to 3 minutes
                    builder.Append(src, 0, 2.5 * 60);
                }
                else
                {
                    builder.Append(src);
                }
                builder.Save(dst);
            }
        }

        private BgmList GetBgmList(string bgmJson)
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

        private class BgmList
        {
            private Dictionary<string, List<string>> _samples = new Dictionary<string, List<string>>();

            public string[] Tags => _samples.Keys.ToArray();

            public BgmList()
            {
            }

            public BgmList(Dictionary<string, string[]> samples)
            {
                _samples = samples
                    .ToDictionary(x => x.Key, x => x.Value.ToList());
            }

            public void Add(string tag, string path)
            {
                var list = GetList(tag);
                list.Add(path);
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
