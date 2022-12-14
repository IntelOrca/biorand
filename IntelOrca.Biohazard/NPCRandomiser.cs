#define ALWAYS_SWAP_NPC

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private readonly BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly string _originalDataPath;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly List<VoiceSample> _pool = new List<VoiceSample>();
        private readonly HashSet<VoiceSample> _randomized = new HashSet<VoiceSample>();
        private readonly INpcHelper _npcHelper;
        private readonly DataManager _dataManager;

        private VoiceSample[] _voiceSamples = new VoiceSample[0];
        private List<VoiceSample> _uniqueSamples = new List<VoiceSample>();
        private string? _playerActor;
        private string? _originalPlayerActor;
        private Dictionary<byte, string> _extraNpcMap = new Dictionary<byte, string>();

        private List<ExternalCharacter> _plds = new List<ExternalCharacter>();
        private List<ExternalCharacter> _emds = new List<ExternalCharacter>();

        public NPCRandomiser(BioVersion version, RandoLogger logger, RandoConfig config, string originalDataPath, string modPath, GameData gameData, Map map, Rng random, INpcHelper npcHelper, DataManager dataManager)
        {
            _version = version;
            _logger = logger;
            _config = config;
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _npcHelper = npcHelper;
            _dataManager = dataManager;
            _voiceSamples = AddToSelection(version, originalDataPath);
            _originalPlayerActor = _npcHelper.GetPlayerActor(config.Player);
            _playerActor = _originalPlayerActor;

            var customSamples = AddCustom();
            _uniqueSamples.AddRange(customSamples);
        }

        public VoiceSample[] AddToSelection(BioVersion version, string originalDataPath)
        {
            var voiceJsonPath = _dataManager.GetPath(version, "voice.json");
            var voiceJson = File.ReadAllText(voiceJsonPath);
            var extraSamples = LoadVoiceInfoFromJson(originalDataPath, voiceJson);
            _uniqueSamples.AddRange(extraSamples);
            return extraSamples;
        }

        public VoiceSample[] AddCustom()
        {
            var samples = new List<VoiceSample>();
            foreach (var actorPath in _dataManager.GetDirectoriesIn("voice"))
            {
                var actor = Path.GetFileName(actorPath);
                var sampleFiles = Directory.GetFiles(actorPath);
                foreach (var sampleFile in sampleFiles)
                {
                    if (!sampleFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                        !sampleFile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var sample = new VoiceSample();
                    sample.BasePath = Path.GetDirectoryName(sampleFile);
                    sample.Path = Path.GetFileName(sampleFile);
                    sample.Actor = actor;
                    sample.End = GetVoiceLength(sampleFile);
                    samples.Add(sample);
                }
            }
            return samples.ToArray();
        }

        public void AddPC(bool isFemale, string actor, string pldPath, string facePath)
        {
            _plds.Add(new ExternalCharacter(isFemale, actor, pldPath, facePath));
        }

        public void AddNPC(bool isFemale, string actor, string emdPath, string timPath)
        {
            _emds.Add(new ExternalCharacter(isFemale, actor, emdPath, timPath));
        }

        public void Randomise()
        {
            RandomizePlayer(_rng.NextFork());
            RandomizeExternalNPCs(_rng.NextFork());
            RandomizeRooms(_rng.NextFork());
            SetVoices();
        }

        private void RandomizePlayer(Rng rng)
        {
            if (_plds.Count == 0)
                return;

            var pld = _plds.Where(x => x.Actor != _originalPlayerActor).Shuffle(rng).First();
            _playerActor = pld.Actor;

            _logger.WriteHeading("Randomizing Player:");
            _logger.WriteLine($"{_originalPlayerActor} becomes {_playerActor}");

            var plcPathA = Path.Combine(_modPath, $"pl{_config.Player}", "pld", $"pl{_config.Player:X2}.pld");
            var plcPathB = Path.Combine(_modPath, $"pl{_config.Player}", "pld", $"pl{_config.Player + 4:X2}.pld"); // Alt. character (in later half of the game)
            Directory.CreateDirectory(Path.GetDirectoryName(plcPathA));
            File.Copy(pld.ModelPath, plcPathA, true);
            File.Copy(pld.ModelPath, plcPathB, true);

            var facePath = Path.Combine(_modPath, $"common", "data", $"st{_config.Player}_jp.tim");
            Directory.CreateDirectory(Path.GetDirectoryName(facePath));
            File.Copy(pld.TexturePath, facePath, true);

            var allHurtFiles = _dataManager.GetHurtFiles(pld.Actor)
                .Where(x => x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var hurtFiles = new string[4];
            foreach (var hurtFile in allHurtFiles)
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(hurtFile), out var i))
                {
                    if (i < hurtFiles.Length)
                    {
                        hurtFiles[i] = hurtFile;
                    }
                }
            }
            if (hurtFiles.All(x => x != null))
            {
                var corePath = Path.Combine(_modPath, "common", "sound", "core", $"core{_config.Player:X2}.sap");
                Directory.CreateDirectory(Path.GetDirectoryName(corePath));
                for (int i = 0; i < hurtFiles.Length; i++)
                {
                    var waveformBuilder = new WaveformBuilder();
                    waveformBuilder.Append(hurtFiles[i]);
                    if (i == 0)
                        waveformBuilder.Save(corePath, 0x0F);
                    else
                        waveformBuilder.SaveAppend(corePath);
                }
            }
        }

        private void RandomizeExternalNPCs(Rng rng)
        {
            if (_emds.Count == 0)
                return;

            _logger.WriteHeading("Adding additional NPCs:");
            var availableSlotsLeon = new byte[] { 0x52, 0x54, 0x56, 0x58, 0x5A };
            var availableSlotsClaire = new byte[] { 0x53, 0x55, 0x57, 0x59, 0x5B };

            RandomizeExternalNPCs(_emds.Where(x => !x.IsFemale && x.Actor != _playerActor).Shuffle(rng), availableSlotsLeon);
            RandomizeExternalNPCs(_emds.Where(x => x.IsFemale && x.Actor != _playerActor).Shuffle(rng), availableSlotsClaire);
        }

        private void RandomizeExternalNPCs(ExternalCharacter[] emds, byte[] availableSlots)
        {
            var maxEmds = Math.Min(availableSlots.Length, emds.Length);
            for (int i = 0; i < maxEmds; i++)
            {
                var emd = emds[i];
                var enemyType = availableSlots[i];
                _extraNpcMap[enemyType] = emd.Actor;

                var emd0Path = Path.Combine(_modPath, $"pl{_config.Player}", $"emd{_config.Player}");
                var emdPath = Path.Combine(emd0Path, $"em{_config.Player}{enemyType:X2}.emd");
                var timPath = Path.ChangeExtension(emdPath, ".tim");
                Directory.CreateDirectory(Path.GetDirectoryName(emdPath));
                File.Copy(emd.ModelPath, emdPath, true);
                File.Copy(emd.TexturePath, timPath, true);

                _logger.WriteLine($"Enemy 0x{enemyType:X2} becomes {emd.Actor}");
            }
        }

        private void RandomizeRooms(Rng rng)
        {
            _logger.WriteHeading("Randomizing Characters, Voices:");
            _pool.AddRange(_uniqueSamples.Shuffle(rng.NextFork()));
            foreach (var rdt in _gameData.Rdts)
            {
                RandomizeRoom(rng.NextFork(), rdt);
            }
        }

        private void RandomizeRoom(Rng rng, Rdt rdt)
        {
            var npcRng = rng.NextFork();
            var voiceRng = rng.NextFork();

            var defaultIncludeTypes = _npcHelper.GetDefaultIncludeTypes(rdt);
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                defaultIncludeTypes = defaultIncludeTypes
                    .Where(x => GetActor(x) != _playerActor)
                    .ToArray();
            }
            if (_extraNpcMap.Count != 0)
            {
                defaultIncludeTypes = defaultIncludeTypes.Concat(_extraNpcMap.Keys).ToArray();
            }

            var room = _map.GetRoom(rdt.RdtId);
            var npcs = room?.Npcs;
            if (npcs == null)
                npcs = new[] { new MapRoomNpcs() };

            var offsetToTypeMap = new Dictionary<int, byte>();
            foreach (var cutscene in npcs.GroupBy(x => x.Cutscene))
            {
                var actorToNewActorMap  = RandomizeCharacters(rdt, npcRng, defaultIncludeTypes, cutscene.ToArray(), offsetToTypeMap);
                var pc = cutscene.First().PlayerActor ?? _originalPlayerActor!;
                if (pc == _originalPlayerActor)
                {
                    actorToNewActorMap[pc] = _playerActor!;
                    pc = _playerActor;
                }
                else
                {
                    actorToNewActorMap[pc] = pc;
                }
                RandomizeVoices(rdt, voiceRng, cutscene.Key, actorToNewActorMap);
                if (actorToNewActorMap.Count != 1)
                {
                    _logger.WriteLine($"  cutscene #{cutscene.Key} contains {string.Join(", ", actorToNewActorMap.Values)}");
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
                            .Where(x => !actorToNewActorMap.Values.Contains(GetActor(x)))
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

                        var oldActor = GetActor(enemy.Type)!;
                        if (!offsetToTypeMap.TryGetValue(enemy.Offset, out var newEnemyType))
                        {
#if ALWAYS_SWAP_NPC
                            var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => GetActor(x) != GetActor(enemy.Type));
                            newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                            newEnemyType = supportedNpcs[0];
#endif
                            _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{_npcHelper.GetNpcName(enemy.Type)}] becomes [{_npcHelper.GetNpcName(newEnemyType)}]");
                            offsetToTypeMap[enemy.Offset] = newEnemyType;
                        }
                        actorToNewActorMap[oldActor] = GetActor(newEnemyType)!;
                    }
                }
            }
            return actorToNewActorMap;
        }

        private void RandomizeVoices(Rdt rdt, Rng rng, int cutscene, Dictionary<string, string> actorToNewActorMap)
        {
            var actors = actorToNewActorMap.Values.ToArray();
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

        private string? GetActor(byte enemyType)
        {
            if (_extraNpcMap.TryGetValue(enemyType, out var actor))
                return actor;
            return _npcHelper.GetActor(enemyType);
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
                    var srcPath = GetVoicePath(firstSample.Replacement.Source);
                    CopySample(srcPath, dstPath);
                }
                else
                {
                    var builder = new WaveformBuilder();
                    foreach (var sample in sampleOrder)
                    {
                        var replacement = sample.Replacement;
                        if (replacement != null)
                        {
                            var sliceSrcPath = GetVoicePath(replacement.Source);
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

        private static void CopySample(string srcPath, string dstPath)
        {
            var srcExtension = Path.GetExtension(srcPath);
            var dstExtension = Path.GetExtension(dstPath);
            if (srcExtension.Equals(dstExtension, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(srcPath, dstPath, true);
            }
            else
            {
                var builder = new WaveformBuilder();
                builder.Append(srcPath);
                builder.Save(dstPath);
            }
        }

        private static string GetVoicePath(VoiceSample sample)
        {
            return Path.Combine(sample.BasePath, sample.Path);
        }

        private static string GetVoicePath(string basePath, VoiceSample sample)
        {
            return Path.Combine(basePath, sample.Path);
        }

        private static VoiceSample[] LoadVoiceInfoFromJson(string originalDataPath, string json)
        {
            var voiceList = JsonSerializer.Deserialize<Dictionary<string, VoiceSample>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var samples = new List<VoiceSample>();
            foreach (var kvp in voiceList!)
            {
                var sample = kvp.Value;
                sample.BasePath = originalDataPath;
                sample.Path = kvp.Key;
                var totalLength = GetVoiceLength(Path.Combine(originalDataPath, sample.Path));
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
            else if (path.EndsWith(".ogg"))
            {
                using (var vorbis = new VorbisReader(path))
                {
                    return vorbis.TotalTime.TotalSeconds;
                }
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
        public string? BasePath { get; set; }
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
            var nameClip = sub.NameClipped;
            if (nameClip != null)
            {
                if (nameClip.End == 0)
                    nameClip.End = end;
                if (nameClip.Start == 0)
                    nameClip.Start = start;
            }

            return new VoiceSample()
            {
                BasePath = BasePath,
                Path = Path,
                Actor = sub.Actor,
                Rdt = Rdt,
                Player = Player,
                Start = start,
                End = end,
                MaxLength = end - start,
                Condition = sub.Condition,
                NameClipped = nameClip
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

    [DebuggerDisplay("{Actor}")]
    public struct ExternalCharacter
    {
        public bool IsFemale { get; }
        public string Actor { get; }
        public string ModelPath { get; }
        public string TexturePath { get; }

        public ExternalCharacter(bool isFemale, string actor, string modelPath, string texturePath)
        {
            IsFemale = isFemale;
            Actor = actor;
            ModelPath = modelPath;
            TexturePath = texturePath;
        }
    }
}
