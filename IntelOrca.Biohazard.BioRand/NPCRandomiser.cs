using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.RE2;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private static readonly object _sync = new object();

        private readonly BioVersion _version;
        private readonly RandoLogger _logger;
        private readonly FileRepository _fileRepository;
        private readonly RandoConfig _config;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _rng;
        private readonly INpcHelper _npcHelper;
        private readonly DataManager _dataManager;
        private readonly string[] _playerActors;
        private readonly string[] _originalPlayerActor;

        private Dictionary<byte, string> _extraNpcMap = new Dictionary<byte, string>();
        private List<ExternalCharacter> _emds = new List<ExternalCharacter>();

        private VoiceRandomiser? _voiceRandomiser;

        public HashSet<string> SelectedActors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public NPCRandomiser(
            BioVersion version,
            RandoLogger logger,
            FileRepository fileRepository,
            RandoConfig config,
            string modPath,
            GameData gameData,
            Map map,
            Rng random,
            INpcHelper npcHelper,
            DataManager dataManager,
            string[]? playerActors,
            VoiceRandomiser voiceRandomiser)
        {
            _version = version;
            _logger = logger;
            _fileRepository = fileRepository;
            _config = config;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _rng = random;
            _npcHelper = npcHelper;
            _dataManager = dataManager;
            _originalPlayerActor = _npcHelper.GetPlayerActors(config.Player);
            _playerActors = playerActors ?? _originalPlayerActor;
            _voiceRandomiser = voiceRandomiser;

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

        public void Randomise()
        {
            RandomizeExternalNPCs(_rng.NextFork());
            RandomizeRooms(_rng.NextFork());
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
            var emdSet = true;
            while (emdSet)
            {
                emdSet = false;
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
                            emdSet = true;
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
                                emdSet = true;
                            }
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
            _logger.WriteHeading("Randomizing Characters:");
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

                _voiceRandomiser?.SetRoomVoiceMap(rdt.RdtId, cutscene.Key, pc!, actorToNewActorMap);
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
                            if (oldActor.IsSherryActor() && !newActor.IsSherryActor())
                            {
                                ScaleEMRs(rdt, enemy.Id, true);
                            }
                            else if (newActor.IsSherryActor() && !oldActor.IsSherryActor())
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
                            if (actor.IsSherryActor())
                            {
                                supportedNpcs = supportedNpcs.Where(x => GetActor(x).IsSherryActor()).ToArray();
                            }
                            else
                            {
                                supportedNpcs = supportedNpcs.Where(x => !GetActor(x).IsSherryActor()).ToArray();
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

        private string? GetActor(byte enemyType, bool originalOnly = false)
        {
            if (!originalOnly && _extraNpcMap.TryGetValue(enemyType, out var actor))
                return actor;
            return _npcHelper.GetActor(enemyType);
        }

        [DebuggerDisplay("{Actor}")]
        private struct ExternalCharacter
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
}
