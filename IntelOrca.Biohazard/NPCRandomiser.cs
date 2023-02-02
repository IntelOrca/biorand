// #define ALWAYS_SWAP_NPC

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using IntelOrca.Biohazard.RE1;
using IntelOrca.Biohazard.RE2;
using IntelOrca.Biohazard.Script.Opcodes;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private static readonly object _sync = new object();

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
        private List<ExternalCharacter1> _emds1 = new List<ExternalCharacter1>();
        private List<ExternalCharacter2> _emds2 = new List<ExternalCharacter2>();

        public HashSet<string> SelectedActors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public NPCRandomiser(
            BioVersion version,
            RandoLogger logger,
            RandoConfig config,
            string originalDataPath,
            string modPath,
            GameData gameData,
            Map map,
            Rng random,
            INpcHelper npcHelper,
            DataManager dataManager,
            string? playerActor)
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
            _playerActor = playerActor ?? _originalPlayerActor;

            var customSamples = AddCustom();
            _uniqueSamples.AddRange(customSamples);

            FixRooms();
        }

        private void FixRooms()
        {
            // For RE 2, room 216 and 301 need the partner removed to prevent cutscene softlock
            DetachPartner(new RdtId(1, 0x16));
            DetachPartner(new RdtId(2, 0x01));
        }

        private void DetachPartner(RdtId rdtId)
        {
            var rdt = _gameData.GetRdt(rdtId);
            if (rdt != null && rdt.Version == BioVersion.Biohazard2)
            {
                rdt.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x22, new byte[] { 0x01, 0x03, 0x00 }));
            }
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
            foreach (var actorPath in _dataManager.GetDirectoriesIn("hurt"))
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
                    sample.Kind = Path.GetFileNameWithoutExtension(sampleFile) == "3" ? "death" : "hurt";
                    samples.Add(sample);
                }
            }

            return _dataManager
                .GetDirectoriesIn("voice")
                .SelectMany(x =>
                {
                    var actor = Path.GetFileName(x);
                    var sampleFiles = Directory.GetFiles(x);
                    return sampleFiles.Select(y => (Actor: actor, SampleFiles: y));
                })
                .AsParallel()
                .Select(x => ProcessSample(x.Actor, x.SampleFiles))
                .Where(x => x != null)
                .ToArray()!;
        }

        private VoiceSample? ProcessSample(string actor, string sampleFile)
        {
            if (!sampleFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !sampleFile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fileName = Path.GetFileName(sampleFile);
            var condition = GetThingFromFileName(fileName, '-');
            if (condition != null)
            {
                if (condition.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                {
                    condition = "!" + condition.Substring(2);
                }
                else
                {
                    condition = "@" + condition;
                }
            }

            var sample = new VoiceSample();
            sample.BasePath = Path.GetDirectoryName(sampleFile);
            sample.Path = fileName;
            sample.Actor = actor;
            sample.End = GetVoiceLength(sampleFile);
            sample.Kind = GetThingFromFileName(fileName, '_');
            sample.Condition = condition;
            return sample;
        }

        private static string? GetThingFromFileName(string filename, char symbol)
        {
            var end = filename.LastIndexOf('.');
            if (end == -1)
                end = filename.Length;

            var start = -1;
            for (int i = 0; i < end; i++)
            {
                var c = filename[i];
                if (c == symbol)
                {
                    start = i + 1;
                }
                else if (c == '_' || c == '-')
                {
                    if (start != -1)
                    {
                        end = i;
                        break;
                    }
                }
            }
            if (start == -1)
                return null;
            var result = filename.Substring(start, end - start);
            return result == "" ? null : result;
        }

        public void AddNPC1(byte emId, string emPath, string actor)
        {
            _emds1.Add(new ExternalCharacter1(emId, emPath, actor));
        }

        public void AddNPC2(bool isFemale, string actor, string emdPath, string timPath)
        {
            _emds2.Add(new ExternalCharacter2(isFemale, actor, emdPath, timPath));
        }

        private bool ActorHasVoiceSamples(string actor)
        {
            return _uniqueSamples.Any(x => x.Actor == actor);
        }

        public void Randomise()
        {
            RandomizeExternalNPCs(_rng.NextFork());
            RandomizeRooms(_rng.NextFork());
            SetVoices();
        }

        private void RandomizeExternalNPCs(Rng rng)
        {
            if (_config.Game == 1)
            {
                lock (_sync)
                {
                    // RE 1 shared EMD files, so must have a shared RNG
                    var sharedRng = new Rng(_config.Seed);
                    RandomizeExternalNPCsRE1(sharedRng);
                }
            }
            else
            {
                RandomizeExternalNPCsRE2(rng);
            }
        }

        private void RandomizeExternalNPCsRE1(Rng rng)
        {
            if (_emds1.Count == 0)
                return;

            var enemyDirectory = Path.Combine(_modPath, "enemy");
            Directory.CreateDirectory(enemyDirectory);

            _logger.WriteHeading("Adding additional NPCs:");
            var normalSlots = new Queue<byte>(new byte[] {
                Re1EnemyIds.ChrisStars,
                Re1EnemyIds.JillStars,
                Re1EnemyIds.BarryStars,
                Re1EnemyIds.RebeccaStars,
                Re1EnemyIds.WeskerStars
            }.Shuffle(_rng));

            foreach (var g in _emds1.GroupBy(x => x.EmId))
            {
                var gSelected = g.Where(x => SelectedActors.Contains(x.Actor)).ToArray();
                if (gSelected.Length == 0)
                    continue;

                if (g.Key == 0x20)
                {
                    // Normal slot, take all selected characters
                    var randomCharactersToInclude = gSelected.Shuffle(_rng);
                    foreach (var rchar in randomCharactersToInclude)
                    {
                        // 50:50 on whether to use original or new character, unless original is not selected.
                        var slot = normalSlots.Dequeue();
                        var originalActor = GetActor(slot, true) ?? "";
                        if (!SelectedActors.Contains(originalActor) ||
                            _rng.NextProbability(50))
                        {
                            SetEm(slot, rchar);
                        }
                    }
                }
                else
                {
                    // Special slot, only take one of the selected characters
                    var emd = rng.NextOf(gSelected);
                    SetEm(emd.EmId, emd);
                }
            }

            void SetEm(byte id, ExternalCharacter1 ec)
            {
                _extraNpcMap[id] = ec.Actor;

                var dst = Path.Combine(enemyDirectory, $"EM1{id:X3}.EMD");
                File.Copy(ec.EmPath, dst, true);

                _logger.WriteLine($"Enemy 0x{id:X2} becomes {ec.Actor}");
            }
        }

        private void RandomizeExternalNPCsRE2(Rng rng)
        {
            if (_emds2.Count == 0)
                return;

            _logger.WriteHeading("Adding additional NPCs:");
            var availableSlotsLeon = new byte[] { 0x48, 0x52, 0x54, 0x56, 0x58, 0x5A };
            var availableSlotsClaire = new byte[] { 0x53, 0x55, 0x57, 0x59, 0x5B };

            var emds = _emds2
                .Where(x => x.Actor != _playerActor)
                .Where(x => SelectedActors.Contains(x.Actor))
                .Where(x => ActorHasVoiceSamples(x.Actor))
                .ToArray();

            RandomizeExternalNPCs(emds.Where(x => !x.IsFemale).Shuffle(rng), availableSlotsLeon);
            RandomizeExternalNPCs(emds.Where(x => x.IsFemale).Shuffle(rng), availableSlotsClaire);
        }

        private void RandomizeExternalNPCs(ExternalCharacter2[] emds, byte[] availableSlots)
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

            var defaultIncludeTypes = _npcHelper.GetDefaultIncludeTypes(rdt)
                .Shuffle(rng)
                .DistinctBy(x => _npcHelper.GetActor(x))
                .ToArray();
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
            npcs = npcs
                .Where(x => (x.Player == null || x.Player == _config.Player) &&
                            (x.Scenario == null || x.Scenario == _config.Scenario) &&
                            (x.DoorRando == null || x.DoorRando == _config.RandomDoors))
                .ToArray();

            var offsetToTypeMap = new Dictionary<int, byte>();
            var idToTypeMap = new Dictionary<byte, byte>();
            foreach (var cutscene in npcs.GroupBy(x => x.Cutscene))
            {
                var actorToNewActorMap  = RandomizeCharacters(rdt, npcRng, defaultIncludeTypes, cutscene.ToArray(), offsetToTypeMap, idToTypeMap);
                var pc = cutscene.First().PlayerActor ?? _originalPlayerActor!;
                if (pc == _originalPlayerActor || _config.RandomDoors)
                {
                    actorToNewActorMap[pc] = _playerActor!;
                    pc = _playerActor;
                }
                else
                {
                    actorToNewActorMap[pc] = pc;
                }
                RandomizeVoices(rdt, voiceRng, cutscene.Key, pc!, actorToNewActorMap);
                if (actorToNewActorMap.Count != 1)
                {
                    _logger.WriteLine($"  cutscene #{cutscene.Key} contains {string.Join(", ", actorToNewActorMap.Values)}");
                }
            }

            foreach (var enemy in rdt.Enemies)
            {
                if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newType))
                {
                    var npc = npcs.FirstOrDefault(x => x.IncludeOffsets == null || x.IncludeOffsets.Contains(enemy.Offset));
                    if (npc == null || npc.EmrScale != false)
                    {
                        var oldActor = GetActor(enemy.Type, originalOnly: true);
                        var newActor = GetActor(newType);
                        if (oldActor != newActor)
                        {
                            if (oldActor == "sherry")
                            {
                                ScaleEMRs(rdt, enemy.Id, true);
                            }
                            else if (newActor == "sherry")
                            {
                                ScaleEMRs(rdt, enemy.Id, false);
                            }
                        }
                    }
                    enemy.Type = newType;
                }
            }
        }

        private void ScaleEMRs(Rdt rdt, byte id, bool inverse)
        {
            EmrFlags flags = 0;
            switch (id)
            {
                case 0:
                    flags = EmrFlags.Entity0;
                    break;
                case 1:
                    flags = EmrFlags.Entity1;
                    break;
                case 255:
                    flags = EmrFlags.Partner;
                    break;
                default:
                    break;
            }
            if (flags != 0)
            {
                Re2Randomiser.ScaleEmrY(_logger, rdt, flags, inverse);
            }
        }

        private Dictionary<string, string> RandomizeCharacters(Rdt rdt, Rng rng, byte[] defaultIncludeTypes, MapRoomNpcs[] npcs, Dictionary<int, byte> offsetToTypeMap, Dictionary<byte, byte> idToTypeMap)
        {
            var actorToNewActorMap = new Dictionary<string, string>();
            foreach (var npc in npcs)
            {
                if (npc.Player != null && npc.Player != _config.Player)
                    continue;
                if (npc.Scenario != null && npc.Scenario != _config.Scenario)
                    continue;
                if (npc.DoorRando != null && npc.DoorRando != _config.RandomDoors)
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
                
                var selectedNpcs = supportedNpcs
                    .Where(x => SelectedActors.Contains(GetActor(x) ?? ""))
                    .ToArray();
                if (selectedNpcs.Length != 0)
                {
                    supportedNpcs = selectedNpcs;
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

                    // HACK A specific enemy ID must always be Sherry, or never be Sherry otherwise there will be an EMR conflict
                    foreach (var enemyId in enemyGroup.Select(x => x.Id).Distinct())
                    {
                        if (idToTypeMap.TryGetValue(enemyId, out var alreadyType))
                        {
                            var actor = GetActor(alreadyType);
                            if (actor == "sherry")
                            {
                                supportedNpcs = supportedNpcs.Where(x => GetActor(x) == "sherry").ToArray();
                            }
                            else
                            {
                                supportedNpcs = supportedNpcs.Where(x => GetActor(x) != "sherry").ToArray();
                            }
                        }
                    }
                    if (supportedNpcs.Length == 0)
                    {
                        continue;
                    }

                    foreach (var enemy in enemyGroup)
                    {
                        if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                            continue;

                        var oldActor = GetActor(enemy.Type, originalOnly: true)!;
                        string newActor;
                        if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newEnemyType))
                        {
                            newActor = GetActor(newEnemyType)!;
                        }
                        else
                        {
#if ALWAYS_SWAP_NPC
                            var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => GetActor(x) != oldActor);
                            newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                            newEnemyType = supportedNpcs[0];
#endif
                            newActor = GetActor(newEnemyType)!;
                            _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{_npcHelper.GetNpcName(enemy.Type)}] becomes [{_npcHelper.GetNpcName(newEnemyType)} ({newActor})]");
                            offsetToTypeMap[enemy.Offset] = newEnemyType;
                            idToTypeMap[enemy.Id] = newEnemyType;
                        }
                        actorToNewActorMap[oldActor] = newActor;
                    }
                }
            }
            return actorToNewActorMap;
        }

        private void RandomizeVoices(Rdt rdt, Rng rng, int cutscene, string pc, Dictionary<string, string> actorToNewActorMap)
        {
            string? radioActor = null;
            var actors = actorToNewActorMap.Values.ToArray();
            foreach (var sample in _voiceSamples)
            {
                if (sample.Player == _config.Player &&
                    sample.Cutscene == cutscene &&
                    sample.IsPlayedIn(rdt.RdtId))
                {
                    var actor = sample.Actor!;
                    var kind = sample.Kind;
                    if (kind == "radio")
                    {
                        if (radioActor == null)
                        {
                            radioActor = GetRandomRadioActor(_rng, pc) ?? actor;
                        }
                        RandomizeVoice(rng, sample, actor, radioActor, sample.Kind, actors);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(rng, sample, actor, newActor, kind, actors);
                    }
                    else
                    {
                        RandomizeVoice(rng, sample, actor, actor, kind, actors);
                    }
                }
            }
        }

        private string? GetRandomRadioActor(Rng rng, string pc)
        {
            var actors = _uniqueSamples
                .Where(x => x.Kind == "radio" && x.Actor != pc)
                .Select(x => x.Actor)
                .Distinct()
                .Shuffle(rng);
            return actors.FirstOrDefault();
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind, string[] actors)
        {
            if (_randomized.Contains(voice))
                return;

            actors = actors.Select(TrimActorGame).ToArray();
            var randomVoice = GetRandomVoice(rng, newActor, kind, actors, voice.MaxLength);
            if (randomVoice != null)
            {
                voice.Replacement = randomVoice;
                _randomized.Add(voice);

                var logKindSource = kind == null ? "" : $",{kind}";
                var logKindTarget = randomVoice.Source.Kind == null ? "" : $",{randomVoice.Source.Kind}";
                var logClip = randomVoice.IsClipped ? $" ({randomVoice.Start:0.00}-{randomVoice.End:0.00})" : "";
                _logger.WriteLine($"    {voice.PathWithSapIndex} [{actor}{logKindSource}] becomes {randomVoice.PathWithSapIndex} [{newActor}{logKindTarget}]{logClip}");
            }
        }

        private static string TrimActorGame(string actor)
        {
            var fsIndex = actor.IndexOf('.');
            if (fsIndex == -1)
                return actor;

            return actor.Substring(0, fsIndex);
        }

        private string? GetActor(byte enemyType, bool originalOnly = false)
        {
            if (!originalOnly && _extraNpcMap.TryGetValue(enemyType, out var actor))
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

            // Do not add lines that are already in the pool.
            // Otherwise we can end up with many duplicates in a row
            var poolHash = _pool.ToHashSet();
            var newItems = _uniqueSamples
                .Where(x => x.Actor == actor)
                .Where(x => !poolHash.Contains(x))
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
            if (sample.Kind != kind)
                return SampleCheckResult.Bad;
            if (!sample.CheckConditions(actors))
                return sample.NameClipped != null ? SampleCheckResult.Clip : SampleCheckResult.Bad;
            return SampleCheckResult.Good;
        }

        private enum SampleCheckResult { Bad, Good, Clip }

        private void SetVoices()
        {
            var groups = _voiceSamples.GroupBy(x => (x.Path, x.SapIndex));
            foreach (var group in groups)
            {
                var sampleOrder = group.OrderBy(x => x.Start).ToArray();
                if (sampleOrder.All(x => x.Replacement == null))
                    continue;

                var firstSample = sampleOrder[0];
                var dstPath = GetVoicePath(_modPath, firstSample);
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

                if (sampleOrder.Length == 1 && firstSample.Replacement?.IsClipped == false && firstSample.SapIndex == null && firstSample.Replacement.Source.SapIndex == null)
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
                            if (replacement.Source.SapIndex != null)
                            {
                                builder.Append(sliceSrcPath, replacement.Source.SapIndex.Value, replacement.Start, replacement.End);
                            }
                            else
                            {
                                builder.Append(sliceSrcPath, replacement.Start, replacement.End);
                            }
                            builder.AppendSilence(sample.Length - replacement.Length);
                        }
                        else
                        {
                            builder.AppendSilence(sample.Length);
                        }
                    }

                    if (firstSample.SapIndex != null)
                    {
                        // Copy existing sap file first, then modify the embedded .wavs
                        if (!File.Exists(dstPath))
                        {
                            File.Copy(GetVoicePath(firstSample), dstPath, true);
                        }
                        builder.SaveAt(dstPath, firstSample.SapIndex.Value);
                    }
                    else
                    {
                        builder.Save(dstPath);
                    }
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
                if (sample.Path.Contains("#"))
                {
                    var parts = sample.Path.Split('#');
                    sample.Path = parts[0];
                    sample.SapIndex = int.Parse(parts[1]);
                }

                var totalLength = GetVoiceLength(Path.Combine(originalDataPath, sample.Path));
                if (sample.Strict)
                    sample.MaxLength = totalLength;
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
        private RdtId[]? _cachedRdtIds;

        public string? BasePath { get; set; }
        public string? Path { get; set; }
        public int? SapIndex { get; set; }
        public string? Actor { get; set; }
        public string? Kind { get; set; }
        public string? Rdt { get; set; }
        public string[]? Rdts { get; set; }
        public int? Player { get; set; }
        public int Cutscene { get; set; }
        public bool Strict { get; set; }

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

        public string? PathWithSapIndex => SapIndex == null ? Path : $"{Path}#{SapIndex}";

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
                Rdts = Rdts,
                Player = Player,
                Start = start,
                End = end,
                MaxLength = end - start,
                Condition = sub.Condition,
                Kind = sub.Kind,
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

        public bool IsPlayedIn(RdtId rdtId)
        {
            if (_cachedRdtIds == null)
            {
                if (Rdts != null)
                {
                    _cachedRdtIds = Rdts.Select(RdtId.Parse).ToArray();
                }
                else if (Rdt != null)
                {
                    _cachedRdtIds = new[] { RdtId.Parse(Rdt) };
                }
                else
                {
                    _cachedRdtIds = new RdtId[0];
                }
            }
            return _cachedRdtIds.Contains(rdtId);
        }
    }

    public class VoiceSampleSplit
    {
        public string? Actor { get; set; }
        public double Split { get; set; }
        public string? Condition { get; set; }
        public string? Kind { get; set; }
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

        public string PathWithSapIndex => Source.PathWithSapIndex!;
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
    public struct ExternalCharacter1
    {
        public byte EmId { get; }
        public string EmPath { get; }
        public string Actor { get; }

        public ExternalCharacter1(byte emId, string emPath, string actor)
        {
            EmId = emId;
            EmPath = emPath;
            Actor = actor;
        }
    }

    [DebuggerDisplay("{Actor}")]
    public struct ExternalCharacter2
    {
        public bool IsFemale { get; }
        public string Actor { get; }
        public string ModelPath { get; }
        public string TexturePath { get; }

        public ExternalCharacter2(bool isFemale, string actor, string modelPath, string texturePath)
        {
            IsFemale = isFemale;
            Actor = actor;
            ModelPath = modelPath;
            TexturePath = texturePath;
        }
    }
}
