#define ALWAYS_SWAP_NPC

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly string _originalDataPath;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _random;
        private readonly List<VoiceSample> _pool = new List<VoiceSample>();
        private readonly HashSet<VoiceSample> _randomized = new HashSet<VoiceSample>();
        private readonly INpcHelper _npcHelper;

        private VoiceSample[] _voiceSamples = new VoiceSample[0];
        private VoiceSample[] _uniqueSamples = new VoiceSample[0];

        public NPCRandomiser(RandoLogger logger, RandoConfig config, string originalDataPath, string modPath, GameData gameData, Map map, Rng random, INpcHelper npcHelper)
        {
            _logger = logger;
            _config = config;
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _random = random;
            _npcHelper = npcHelper;
            LoadVoiceInfo(originalDataPath);
        }

        public void Randomise()
        {
            _logger.WriteHeading("Randomizing Characters, Voices:");
            _pool.AddRange(_uniqueSamples.Shuffle(_random));
            foreach (var rdt in _gameData.Rdts)
            {
                RandomizeRoom(_random.NextFork(), rdt);
            }

            SetVoices();
        }

        private void RandomizeRoom(Rng rng, Rdt rdt)
        {
            var npcRng = rng.NextFork();
            var voiceRng = rng.NextFork();

            var playerActor = _npcHelper.GetPlayerActor(_config.Player);
            var defaultIncludeTypes = _npcHelper.GetDefaultIncludeTypes(rdt);
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                defaultIncludeTypes = defaultIncludeTypes
                    .Where(x => _npcHelper.GetActor(x) != playerActor)
                    .ToArray();
            }

            var room = _map.GetRoom(rdt.RdtId);
            var npcs = room?.Npcs;
            if (npcs == null)
                npcs = new[] { new MapRoomNpcs() };

            var offsetToTypeMap = new Dictionary<int, byte>();
            foreach (var cutscene in npcs.GroupBy(x => x.Cutscene))
            {
                var pc = cutscene.First().PlayerActor ?? playerActor;
                var actorToNewActorMap  = RandomizeCharacters(rdt, npcRng, defaultIncludeTypes, cutscene.ToArray(), offsetToTypeMap);
                RandomizeVoices(rdt, voiceRng, pc, cutscene.Key, actorToNewActorMap);
                if (actorToNewActorMap.Count != 0)
                {
                    _logger.WriteLine($"  cutscene #{cutscene.Key} contains {pc}, {string.Join(", ", actorToNewActorMap.Values)}");
                }
            }

            foreach (var enemy in rdt.Enemies)
            {
                if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newType))
                {
                    enemy.Type = newType;
                }
            }
        }

        private Dictionary<string, string> RandomizeCharacters(Rdt rdt, Rng rng, byte[] defaultIncludeTypes, MapRoomNpcs[] npcs, Dictionary<int, byte> offsetToTypeMap)
        {
            var actorToNewActorMap = new Dictionary<string, string>();
            foreach (var npc in npcs)
            {
                if (npc.Player != null && npc.Player != _config.Player)
                    continue;
                if (npc.Scenario != null && npc.Scenario != _config.Scenario)
                    continue;

                var supportedNpcs = npc.IncludeTypes?.Select(x => (byte)x).ToArray() ?? defaultIncludeTypes;
                if (npc.ExcludeTypes != null)
                {
                    supportedNpcs = supportedNpcs.Except(npc.ExcludeTypes.Select(x => (byte)x)).ToArray();
                }
                if (supportedNpcs.Length == 0)
                {
                    continue;
                }
                foreach (var enemyGroup in rdt.Enemies.GroupBy(x => x.Type))
                {
                    if (!_npcHelper.IsNpc(enemyGroup.Key))
                        continue;

                    supportedNpcs = supportedNpcs.Shuffle(rng);
                    {
                        // Avoid using the same actor again, unless we run out
                        var noDuplicatesSupportedNpcs = supportedNpcs
                            .Where(x => !actorToNewActorMap.Values.Contains(_npcHelper.GetActor(x)))
                            .ToArray();
                        if (noDuplicatesSupportedNpcs.Length != 0)
                        {
                            supportedNpcs = noDuplicatesSupportedNpcs;
                        }
                    }
                    foreach (var enemy in enemyGroup)
                    {
                        if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                            continue;

                        var oldActor = _npcHelper.GetActor(enemy.Type)!;
                        if (!offsetToTypeMap.TryGetValue(enemy.Offset, out var newEnemyType))
                        {
#if ALWAYS_SWAP_NPC
                            var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => _npcHelper.GetActor(x) != _npcHelper.GetActor(enemy.Type));
                            newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                            newEnemyType = supportedNpcs[0];
#endif
                            _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{_npcHelper.GetNpcName(enemy.Type)}] becomes [{_npcHelper.GetNpcName(newEnemyType)}]");
                            offsetToTypeMap[enemy.Offset] = newEnemyType;
                        }
                        actorToNewActorMap[oldActor] = _npcHelper.GetActor(newEnemyType)!;
                    }
                }
            }
            return actorToNewActorMap;
        }

        private void RandomizeVoices(Rdt rdt, Rng rng, string playerActor, int cutscene, Dictionary<string, string> actorToNewActorMap)
        {
            var actors = actorToNewActorMap.Values.Append(playerActor).ToArray();
            foreach (var sample in _voiceSamples)
            {
                if (sample.Player == _config.Player &&
                    sample.Cutscene == cutscene &&
                    rdt.RdtId.ToString() == sample.Rdt)
                {
                    var actor = sample.Actor!;
                    var kind = sample.Kind;
                    if (kind == "radio")
                    {
                        RandomizeVoice(rng, sample, actor, actor, sample.Kind, actors);
                    }
                    if ((actor == playerActor && kind != "npc") || kind == "pc")
                    {
                        RandomizeVoice(rng, sample, actor, actor, null, actors);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(rng, sample, actor, newActor, null, actors);
                    }
                    else
                    {
                        RandomizeVoice(rng, sample, actor, actor, null, actors);
                    }
                }
            }
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind, string[] actors)
        {
            if (_randomized.Contains(voice))
                return;

            var randomVoice = GetRandomVoice(rng, newActor, kind, actors, voice.MaxLength);
            if (randomVoice != null)
            {
                voice.Replacement = randomVoice;
                _randomized.Add(voice);

                var logKindSource = kind == null ? "" : $",{kind}";
                var logKindTarget = randomVoice.Source.Kind == null ? "" : $",{randomVoice.Source.Kind}";
                var logClip = randomVoice.IsClipped ? $" ({randomVoice.Start:0.00}-{randomVoice.End:0.00})" : "";
                _logger.WriteLine($"    {voice.Path} [{actor}{logKindSource}] becomes {randomVoice.Path} [{newActor}{logKindTarget}]{logClip}");
            }
        }

        private VoiceSampleReplacement? GetRandomVoice(Rng rng, string actor, string? kind, string[] actors, double maxLength, bool refillPool = true)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var sample = _pool[i];
                var result = CheckSample(sample, actor, kind, actors, maxLength);
                if (result == SampleCheckResult.Bad)
                    continue;

                _pool.RemoveAt(i);
                if (result == SampleCheckResult.Clip)
                    return new VoiceSampleReplacement(sample, sample.NameClipped!.Start, sample.NameClipped!.End);
                else
                    return new VoiceSampleReplacement(sample, sample.Start, sample.End);
            }

            if (!refillPool)
                return null;

            var newItems = _uniqueSamples
                .Where(x => x.Actor == actor)
                .Shuffle(rng);
            _pool.AddRange(newItems);
            return GetRandomVoice(rng, actor, kind, actors, maxLength, refillPool: false);
        }

        private SampleCheckResult CheckSample(VoiceSample sample, string actor, string? kind, string[] actors, double maxLength)
        {
            if (maxLength != 0 && sample.Length > maxLength)
                return SampleCheckResult.Bad;
            if (sample.Actor != actor)
                return SampleCheckResult.Bad;
            if (sample.Kind != kind && (kind == "radio" || sample.Kind == "radio"))
                return SampleCheckResult.Bad;
            if (!sample.CheckConditions(actors))
                return sample.NameClipped != null ? SampleCheckResult.Clip : SampleCheckResult.Bad;
            return SampleCheckResult.Good;
        }

        private enum SampleCheckResult { Bad, Good, Clip }

        private void SetVoices()
        {
            var groups = _voiceSamples.GroupBy(x => x.Path);
            foreach (var group in groups)
            {
                var sampleOrder = group.OrderBy(x => x.Start).ToArray();
                if (sampleOrder.All(x => x.Replacement == null))
                    continue;

                var firstSample = sampleOrder[0];
                var dstPath = GetVoicePath(_modPath, firstSample);
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

                if (sampleOrder.Length == 1 && firstSample.Replacement?.IsClipped == false)
                {
                    var srcPath = GetVoicePath(_originalDataPath, firstSample.Replacement.Source);
                    File.Copy(srcPath, dstPath, true);
                }
                else
                {
                    var builder = new WaveformBuilder();
                    foreach (var sample in sampleOrder)
                    {
                        var replacement = sample.Replacement;
                        if (replacement != null)
                        {
                            var sliceSrcPath = GetVoicePath(_originalDataPath, replacement.Source);
                            builder.Append(sliceSrcPath, replacement.Start, replacement.End);
                            builder.AppendSilence(sample.Length - replacement.Length);
                        }
                        else
                        {
                            builder.AppendSilence(sample.Length);
                        }
                    }
                    builder.Save(dstPath);
                }
            }
        }

        private static string GetVoicePath(string basePath, VoiceSample sample)
        {
            return Path.Combine(basePath, sample.Path);
        }

        private void LoadVoiceInfo(string originalDataPath)
        {
            _voiceSamples = LoadVoiceInfoFromJson();
            _uniqueSamples = _voiceSamples;
            // _uniqueSamples = RemoveDuplicateVoices(_voiceSamples, originalDataPath);
        }

        private VoiceSample[] LoadVoiceInfoFromJson()
        {
            var json = _npcHelper.GetVoiceJson();
            var voiceList = JsonSerializer.Deserialize<Dictionary<string, VoiceSample>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var samples = new List<VoiceSample>();
            foreach (var kvp in voiceList!)
            {
                var sample = kvp.Value;
                sample.Path = kvp.Key;
                var totalLength = GetVoiceLength(Path.Combine(_originalDataPath, sample.Path));
                if (sample.Actors != null)
                {
                    var start = 0.0;
                    foreach (var sub in sample.Actors)
                    {
                        var limited = true;
                        var end = sub.Split;
                        if (end == 0)
                        {
                            end = totalLength;
                            limited = false;
                        }
                        var slice = sample.CreateSlice(sub, start, end);
                        slice.Cutscene = sample.Cutscene;
                        slice.Limited = limited;
                        start = sub.Split;
                        samples.Add(slice);
                    }
                }
                else
                {
                    if (sample.End == 0)
                    {
                        sample.End = totalLength;
                        if (sample.Start == 0)
                        {
                            sample.Vanilla = true;
                        }
                    }
                    samples.Add(sample);
                }
            }

            return samples.ToArray();
        }

        private static VoiceSample[] RemoveDuplicateVoices(VoiceSample[] samples, string originalDataPath)
        {
            var distinct = new List<VoiceSample>();
            foreach (var group in samples.GroupBy(x => GetVoiceSize(originalDataPath, x)))
            {
                var pathToKeep = group.First().Path;
                foreach (var item in group)
                {
                    if (item.Path == pathToKeep)
                    {
                        distinct.Add(item);
                    }
                }
            }
            return distinct.ToArray();
        }

        private static int GetVoiceSize(string basePath, VoiceSample sample)
        {
            var path = GetVoicePath(basePath, sample);
            return (int)new FileInfo(path).Length;
        }

        private static double GetVoiceLength(string path)
        {
            if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
            {
                var decoder = new ADPCMDecoder();
                return decoder.GetLength(path);
            }
            else
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var br = new BinaryReader(fs);
                    var header = br.ReadStruct<WaveHeader>();
                    return header.nDataLength / (double)header.nAvgBytesPerSec;
                }
            }
        }
    }

    [DebuggerDisplay("[{Actor}] {Sample}")]
    internal class VoiceInfo
    {
        public VoiceSample Sample { get; set; }
        public string Actor { get; set; }
        public string Kind { get; set; }

        public VoiceInfo(VoiceSample sample, string actor, string kind)
        {
            Sample = sample;
            Actor = actor;
            Kind = kind;
        }
    }

    [DebuggerDisplay("Actor = {Actor} RdtId = {Rdt} Path = {Path}")]
    public class VoiceSample
    {
        public string? Path { get; set; }
        public string? Actor { get; set; }
        public string? Kind { get; set; }
        public string? Rdt { get; set; }
        public int? Player { get; set; }
        public int Cutscene { get; set; }

        public double Start { get; set; }
        public double End { get; set; }
        public double Length => End - Start;

        public string? Condition { get; set; }

        public double MaxLength { get; set; }
        public bool Vanilla { get; set; }
        public bool Limited { get; set; }
        public VoiceSampleSplit[]? Actors { get; set; }
        public VoiceSampleNameClip? NameClipped { get; set; }

        public VoiceSampleReplacement? Replacement { get; set; }

        public VoiceSample CreateSlice(VoiceSampleSplit sub, double start, double end)
        {
            return new VoiceSample()
            {
                Path = Path,
                Actor = sub.Actor,
                Rdt = Rdt,
                Player = Player,
                Start = start,
                End = end,
                MaxLength = end - start,
                Condition = sub.Condition,
                NameClipped = sub.NameClipped
            };
        }

        public bool CheckConditions(string[] otherActors)
        {
            if (Condition == null)
                return true;

            var conditions = Condition.Replace("&&", "&").Split('&');
            foreach (var singleCondition in conditions)
            {
                var sc = singleCondition.Trim();
                if (sc.StartsWith("!"))
                {
                    var cc = sc.Substring(1);
                    if (otherActors.Contains(cc))
                    {
                        return false;
                    }
                }
                else if (sc.StartsWith("@"))
                {
                    var cc = sc.Substring(1);
                    if (!otherActors.Contains(cc))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public class VoiceSampleSplit
    {
        public string? Actor { get; set; }
        public double Split { get; set; }
        public string? Condition { get; set; }
        public VoiceSampleNameClip? NameClipped { get; set; }
    }

    public class VoiceSampleNameClip
    {
        public double Start { get; set; }
        public double End { get; set; }
    }

    public class VoiceSampleReplacement
    {
        public VoiceSample Source { get; }
        public double Start { get; }
        public double End { get; }

        public string Path => Source.Path!;
        public double Length => End - Start;
        public bool IsClipped => Start != Source.Start || End != Source.End || !Source.Vanilla;

        public VoiceSampleReplacement(VoiceSample source, double start, double end)
        {
            Source = source;
            Start = start;
            End = end;
            if (end == 0)
                End = Source.End;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WaveHeader
    {
        public uint nRiffMagic;
        public uint nRiffLength;
        public uint nWaveMagic;
        public uint nFormatMagic;
        public uint nFormatLength;
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public uint wDataMagic;
        public uint nDataLength;
    }
}
