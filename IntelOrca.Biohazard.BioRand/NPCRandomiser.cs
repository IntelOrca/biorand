using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using IntelOrca.Biohazard.RE2;
using IntelOrca.Biohazard.Script.Opcodes;
using NVorbis;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private static readonly object _sync = new object();
        private static ConcurrentDictionary<string, double> _voiceLengthCache = new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private readonly BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly FileRepository _fileRepository;
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
        private readonly string[] _playerActors;
        private readonly string[] _originalPlayerActor;

        private VoiceSample[] _voiceSamples = new VoiceSample[0];
        private List<VoiceSample> _uniqueSamples = new List<VoiceSample>();
        private Dictionary<byte, string> _extraNpcMap = new Dictionary<byte, string>();
        private List<ExternalCharacter> _emds = new List<ExternalCharacter>();

        public HashSet<string> SelectedActors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public NPCRandomiser(
            BioVersion version,
            RandoLogger logger,
            FileRepository fileRepository,
            RandoConfig config,
            string originalDataPath,
            string modPath,
            GameData gameData,
            Map map,
            Rng random,
            INpcHelper npcHelper,
            DataManager dataManager,
            string[]? playerActors)
        {
            _version = version;
            _logger = logger;
            _fileRepository = fileRepository;
            _config = config;
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _npcHelper = npcHelper;
            _dataManager = dataManager;
            _voiceSamples = AddToSelection(version, fileRepository);
            _originalPlayerActor = _npcHelper.GetPlayerActors(config.Player);
            _playerActors = playerActors ?? _originalPlayerActor;

            using (_logger.Progress.BeginTask(config.Player, "Scanning voices"))
            {
                var customSamples = AddCustom(fileRepository);
                _uniqueSamples.AddRange(customSamples);
            }

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

        public VoiceSample[] AddToSelection(BioVersion version, FileRepository fileRepository)
        {
            var voiceJsonPath = _dataManager.GetPath(version, "voice.json");
            var voiceJson = File.ReadAllText(voiceJsonPath);
            var extraSamples = LoadVoiceInfoFromJson(fileRepository, voiceJson);
            _uniqueSamples.AddRange(extraSamples);
            return extraSamples;
        }

        public VoiceSample[] AddCustom(FileRepository fileRepository)
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
                    sample.End = GetVoiceLength(sampleFile, fileRepository);
                    sample.Kind = Path.GetFileNameWithoutExtension(sampleFile) == "3" ? "death" : "hurt";
                    samples.Add(sample);
                }
            }

            var voiceLines = _dataManager
                .GetDirectoriesIn("voice")
                .SelectMany(x =>
                {
                    var actor = Path.GetFileName(x);
                    var sampleFiles = Directory.GetFiles(x);
                    return sampleFiles.Select(y => (Actor: actor, SampleFiles: y));
                })
                .AsParallel()
                .Select(x => ProcessSample(x.Actor, x.SampleFiles, fileRepository))
                .Where(x => x != null)
                .ToArray() as VoiceSample[];

            samples.AddRange(voiceLines);
            return samples.ToArray();
        }

        private VoiceSample? ProcessSample(string actor, string sampleFile, FileRepository fileRepository)
        {
            if (!sampleFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !sampleFile.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            using (_logger.Progress.BeginTask(_config.Player, $"Scanning '{sampleFile}'"))
            {
                var fileName = Path.GetFileName(sampleFile);
                var conditions = GetThingsFromFileName(fileName, '-');
                var conditionsb = new StringBuilder();
                foreach (var condition in conditions)
                {
                    if (condition.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                    {
                        if (conditionsb.Length != 0)
                            conditionsb.Append(" && ");
                        conditionsb.Append('!');
                        conditionsb.Append(condition, 2, condition.Length - 2);
                    }
                    else
                    {
                        if (conditionsb.Length != 0)
                            conditionsb.Append(" || ");
                        conditionsb.Append('@');
                        conditionsb.Append(condition);
                    }
                }

                var sample = new VoiceSample();
                sample.BasePath = Path.GetDirectoryName(sampleFile);
                sample.Path = fileName;
                sample.Actor = actor;
                sample.End = GetVoiceLength(sampleFile, fileRepository);
                sample.Kind = GetThingsFromFileName(fileName, '_').FirstOrDefault();
                sample.Condition = conditionsb.Length == 0 ? null : conditionsb.ToString();
                return sample;
            }
        }

        private static string[] GetThingsFromFileName(string filename, char symbol)
        {
            var filenameEnd = filename.LastIndexOf('.');
            if (filenameEnd == -1)
                filenameEnd = filename.Length;

            var result = new List<string>();
            var start = -1;
            for (int i = 0; i < filenameEnd; i++)
            {
                var c = filename[i];
                if (c == symbol && start == -1)
                {
                    start = i + 1;
                }
                else if (c == '_' || c == '-')
                {
                    if (start != -1)
                    {
                        result.Add(filename.Substring(start, i - start));
                        i--;
                        start = -1;
                    }
                }
            }
            if (start != -1)
            {
                result.Add(filename.Substring(start, filenameEnd - start));
            }
            return result.ToArray();
        }

        public void AddNPC(byte emId, string emPath, string actor)
        {
            _emds.Add(new ExternalCharacter(emId, emPath, actor));
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
                    RandomizeExternalNPC(sharedRng);
                }
            }
            else
            {
                RandomizeExternalNPC(rng);
            }
        }

        private void RandomizeExternalNPC(Rng rng)
        {
            if (_emds.Count == 0)
                return;

            _logger.WriteHeading("Adding additional NPCs:");
            foreach (var g in _emds.GroupBy(x => x.EmId))
            {
                var gSelected = g.Where(x => SelectedActors.Contains(x.Actor)).ToArray();
                if (gSelected.Length == 0)
                    continue;

                var slots = _npcHelper
                    .GetSlots(_config, g.Key)
                    .Except(_extraNpcMap.Keys)
                    .ToArray();
                var spare = slots
                    .Where(x => _npcHelper.IsSpareSlot(x))
                    .ToQueue();
                var notSpare = slots
                    .Where(x => !_npcHelper.IsSpareSlot(x))
                    .Shuffle(rng)
                    .ToQueue();

                // Normal slot, take all selected characters
                var randomCharactersToInclude = gSelected.Shuffle(rng);
                foreach (var rchar in randomCharactersToInclude)
                {
                    if (spare.Count != 0)
                    {
                        var slot = spare.Dequeue();
                        SetEm(slot, rchar);
                    }
                    else if (notSpare.Count != 0)
                    {
                        // 50:50 on whether to use original or new character, unless original is not selected.
                        var slot = notSpare.Dequeue();
                        var originalActor = GetActor(slot, true) ?? "";
                        if (!SelectedActors.Contains(originalActor) ||
                            rng.NextProbability(50))
                        {
                            SetEm(slot, rchar);
                        }
                    }
                }
            }
        }

        void SetEm(byte id, ExternalCharacter ec)
        {
            _extraNpcMap[id] = ec.Actor;
            if (_config.Game == 1)
            {
                var enemyDirectory = Path.Combine(_modPath, "enemy");
                Directory.CreateDirectory(enemyDirectory);
                var dst = Path.Combine(enemyDirectory, $"EM1{id:X3}.EMD");
                File.Copy(ec.EmPath, dst, true);
            }
            else if (_config.Game == 2)
            {
                var emdPath = Path.Combine(_modPath, $"pl{_config.Player}", $"emd{_config.Player}");
                Directory.CreateDirectory(emdPath);

                var srcEmd = ec.EmPath;
                var dstEmd = Path.Combine(emdPath, $"EM{_config.Player}{id:X2}.EMD");
                var srcTim = Path.ChangeExtension(ec.EmPath, ".tim");
                var dstTim = Path.Combine(emdPath, $"EM{_config.Player}{id:X2}.TIM");
                File.Copy(srcEmd, dstEmd, true);
                File.Copy(srcTim, dstTim, true);
            }
            else if (_config.Game == 3)
            {
                var emdPath = Path.Combine(_modPath, "ROOM", "EMD");
                Directory.CreateDirectory(emdPath);

                var srcEmd = ec.EmPath;
                var dstEmd = Path.Combine(emdPath, $"EM{id:X2}.EMD");
                var srcTim = Path.ChangeExtension(ec.EmPath, ".tim");
                var dstTim = Path.Combine(emdPath, $"EM{id:X2}.TIM");
                File.Copy(srcEmd, dstEmd, true);
                File.Copy(srcTim, dstTim, true);
            }
            _logger.WriteLine($"Enemy 0x{id:X2} becomes {ec.Actor}");
        }

        private void RandomizeRooms(Rng rng)
        {
            _logger.WriteHeading("Randomizing Characters, Voices:");
            _uniqueSamples = _uniqueSamples.OrderBy(x => x.SortString).ToList();
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
            if (_extraNpcMap.Count != 0)
            {
                defaultIncludeTypes = defaultIncludeTypes.Concat(_extraNpcMap.Keys).ToArray();
            }
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                defaultIncludeTypes = defaultIncludeTypes
                    .Where(x => GetActor(x) != _playerActors[0])
                    .ToArray();
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
                var actorToNewActorMap = RandomizeCharacters(rdt, npcRng, defaultIncludeTypes, cutscene.ToArray(), offsetToTypeMap, idToTypeMap);
                var pc = cutscene.First().PlayerActor ?? _originalPlayerActor[0]!;
                if (pc == _originalPlayerActor[0] || _config.RandomDoors)
                {
                    actorToNewActorMap[pc] = _playerActors[0]!;
                    pc = _playerActors[0];
                }
                else
                {
                    actorToNewActorMap[pc] = _playerActors[1];
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
                            if (IsSherryActor(oldActor) && !IsSherryActor(newActor))
                            {
                                ScaleEMRs(rdt, enemy.Id, true);
                            }
                            else if (IsSherryActor(newActor) && !IsSherryActor(oldActor))
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

        private static bool IsSherryActor(string? actor)
        {
            var sherry = "sherry";
            if (actor == null)
                return false;

            var fsIndex = actor.IndexOf('.');
            if (fsIndex != -1)
            {
                if (actor.Length - fsIndex + 1 != sherry.Length)
                    return false;
                return actor.StartsWith(sherry, StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(actor, sherry, StringComparison.OrdinalIgnoreCase);
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
                supportedNpcs = supportedNpcs.Distinct().ToArray();

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
                            if (IsSherryActor(actor))
                            {
                                supportedNpcs = supportedNpcs.Where(x => IsSherryActor(GetActor(x))).ToArray();
                            }
                            else
                            {
                                supportedNpcs = supportedNpcs.Where(x => !IsSherryActor(GetActor(x))).ToArray();
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
                        if (oldActor == null)
                            continue;

                        string newActor;
                        if (offsetToTypeMap.TryGetValue(enemy.Offset, out var newEnemyType))
                        {
                            newActor = GetActor(newEnemyType)!;
                        }
                        else
                        {
                            if (npc.Use != null)
                            {
                                var parts = npc.Use.Split(';');
                                var rdtId = RdtId.Parse(parts[0]);
                                var rdtOffset = int.Parse(parts[1].Substring(2), System.Globalization.NumberStyles.HexNumber);
                                newEnemyType = _gameData
                                    .GetRdt(rdtId)!
                                    .Enemies.FirstOrDefault(x => x.Offset == rdtOffset)
                                    .Type;
                            }
                            else
                            {
#if ALWAYS_SWAP_NPC
                                var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => GetActor(x) != oldActor);
                                newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                                newEnemyType = supportedNpcs[0];
#endif
                            }
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
            {
                if (kind == "scream")
                {
                    // Try find a death sound instead
                    return GetRandomVoice(rng, actor, "death", actors, maxLength, true);
                }
                else if (kind != null)
                {
                    // Fallback to any kind of voice
                    return GetRandomVoice(rng, actor, null, actors, maxLength, true);
                }
                else
                {
                    return null;
                }
            }

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
                using (_logger.Progress.BeginTask(_config.Player, $"Writing '{dstPath}'"))
                {
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
                                using (_logger.Progress.BeginTask(_config.Player, $"Reading '{sliceSrcPath}'"))
                                {
                                    if (replacement.Source.SapIndex != null)
                                    {
                                        builder.Append(sliceSrcPath, replacement.Source.SapIndex.Value, replacement.Start, replacement.End);
                                    }
                                    else
                                    {
                                        var stream = _fileRepository.GetStream(sliceSrcPath);
                                        builder.Append(sliceSrcPath, stream, replacement.Start, replacement.End);
                                    }
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
        }

        private void CopySample(string srcPath, string dstPath)
        {
            var srcExtension = Path.GetExtension(srcPath);
            var dstExtension = Path.GetExtension(dstPath);
            if (srcExtension.Equals(dstExtension, StringComparison.OrdinalIgnoreCase))
            {
                _fileRepository.Copy(srcPath, dstPath);
            }
            else
            {
                var builder = new WaveformBuilder();
                builder.Append(srcPath, _fileRepository.GetStream(srcPath));
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

        private static VoiceSample[] LoadVoiceInfoFromJson(FileRepository fileRepository, string json)
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
                sample.BasePath = fileRepository.DataPath;
                sample.Path = kvp.Key;
                if (sample.Path.Contains("#"))
                {
                    var parts = sample.Path.Split('#');
                    sample.Path = parts[0];
                    sample.SapIndex = int.Parse(parts[1]);
                }

                var totalLength = GetVoiceLength(fileRepository.GetDataPath(sample.Path), fileRepository);
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

        private static double GetVoiceLength(string path, FileRepository fileRepository)
        {
            if (_voiceLengthCache.TryGetValue(path, out var result))
            {
                return result;
            }
            result = GetVoiceLengthInner(path, fileRepository);
            _voiceLengthCache.TryAdd(path, result);
            return result;
        }

        private static double GetVoiceLengthInner(string path, FileRepository fileRepository)
        {
            try
            {
                if (path.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    using (var fs = fileRepository.GetStream(path))
                    {
                        fs.Position = 8;
                        var br = new BinaryReader(fs);
                        var magic = br.ReadUInt32();
                        fs.Position -= 4;
                        if (magic == 0x5367674F) // OGG
                        {
                            using (var vorbis = new VorbisReader(new SlicedStream(fs, 8, fs.Length - 8), closeOnDispose: false))
                            {
                                return vorbis.TotalTime.TotalSeconds;
                            }
                        }
                        else
                        {
                            var decoder = new MSADPCMDecoder();
                            return decoder.GetLength(fs);
                        }
                    }
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
                    using (var fs = fileRepository.GetStream(path))
                    {
                        var decoder = new MSADPCMDecoder();
                        return decoder.GetLength(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new BioRandUserException($"Unable to process '{path}'. {ex.Message}");
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
        public int Player { get; set; }
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

        public string SortString => $"{BasePath}/{PathWithSapIndex}:{Start}-{End}";

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
                var orResult = false;
                var orConditions = singleCondition.Replace("||", "|").Split('|');
                foreach (var orCondition in orConditions)
                {
                    var result = true;
                    var sc = orCondition.Trim();
                    if (sc.StartsWith("!"))
                    {
                        var cc = sc.Substring(1);
                        if (otherActors.Contains(cc))
                        {
                            result = false;
                        }
                    }
                    else if (sc.StartsWith("@"))
                    {
                        var cc = sc.Substring(1);
                        if (!otherActors.Contains(cc))
                        {
                            result = false;
                        }
                    }
                    if (result)
                    {
                        orResult = true;
                        break;
                    }
                }
                if (!orResult)
                    return false;
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

    [DebuggerDisplay("{Actor}")]
    public struct ExternalCharacter
    {
        public byte EmId { get; }
        public string EmPath { get; }
        public string Actor { get; }

        public ExternalCharacter(byte emId, string emPath, string actor)
        {
            EmId = emId;
            EmPath = emPath;
            Actor = actor;
        }
    }
}
