using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.Extensions;
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
        private Dictionary<byte, (short, short)> _npcHeight = new Dictionary<byte, (short, short)>();
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
            if (_config.Game == 2)
            {
                var externals = _emds
                    .Where(x => SelectedActors.Contains(x.Actor))
                    .ToEndlessBag(_rng);
                for (var i = 0; i < 256; i++)
                {
                    if (!_npcHelper.IsSpareSlot((byte)i))
                        continue;

                    var ec = externals.Next();
                    SetEm((byte)i, ec);
                }
            }
            else
            {
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
        }

        void SetEm(byte id, ExternalCharacter ec)
        {
            if (_config.Game == 1)
            {
                var enemyDirectory = Path.Combine(_modPath, "enemy");
                Directory.CreateDirectory(enemyDirectory);
                var dst = Path.Combine(enemyDirectory, $"EM1{id:X3}.EMD");
                File.Copy(ec.EmPath, dst, true);
            }
            else if (_config.Game == 2)
            {
                var emdFileName = Path.Combine($"pl{_config.Player}", $"emd{_config.Player}", $"em{_config.Player}{id:X2}.emd");
                var originalEmdPath = _fileRepository.GetDataPath(emdFileName);
                var targetEmdPath = _fileRepository.GetModPath(emdFileName);
                if (!File.Exists(originalEmdPath))
                    return;

                CreateEmdFile(id, ec.EmPath, originalEmdPath, targetEmdPath);
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
            _extraNpcMap[id] = ec.Actor;
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
                        var scale = GetEmrScale(enemy.Type, newType);
                        if (scale != 1)
                        {
                            ScaleEMRs(rdt, enemy.Id, scale);
                        }
                    }
                    enemy.Type = newType;
                }
            }
        }

        private double GetEmrScale(byte oldType, byte newType)
        {
            var (oldTypeOrig, _) = GetNpcHeight(oldType);
            var (_, newTypeNew) = GetNpcHeight(newType);
            var scale = Extensions2.GetScale(oldTypeOrig, newTypeNew);
            return scale;
        }

        private (short, short) GetNpcHeight(byte type)
        {
            if (!_npcHeight.TryGetValue(type, out var height))
            {
                var player = _config.Player;
                var emdFileName = $"pl{player}/emd{player}/em{player}{type:X2}.emd";
                var originalEmdPath = _fileRepository.GetDataPath(emdFileName);
                var moddedEmdPath = _fileRepository.GetModPath(emdFileName);
                var originalEmdFile = new EmdFile(BioVersion.Biohazard2, originalEmdPath);
                var originalHeight = originalEmdFile.GetHeight();
                var moddedHeight = originalHeight;
                if (File.Exists(moddedEmdPath))
                {
                    var moddedEmdFile = new EmdFile(BioVersion.Biohazard2, moddedEmdPath);
                    moddedHeight = moddedEmdFile.GetHeight();
                }
                height = (originalHeight, moddedHeight);
                _npcHeight[type] = height;
            }
            return height;
        }

        private void ScaleEMRs(Rdt rdt, byte id, double scale)
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
                Re2Randomiser.ScaleEmrY(_logger, rdt, flags, scale);
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

        private void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath)
        {
            var pldFile = ModelFile.FromFile(pldPath);
            var emdFile = ModelFile.FromFile(baseEmdPath);
            var timFile = pldFile.GetTim(0);
            var plwFile = null as ModelFile;

            if (timFile.Width != 128 * 4)
            {
                throw new BioRandUserException($"{pldPath} does not conform to the PLD texture standard.");
            }

            var weapon = GetSuitableWeaponForNPC(type).Random(_rng);
            var plwPath = GetPlwPath(pldPath, weapon);
            if (plwPath != null)
            {
                plwFile = ModelFile.FromFile(plwPath);
                var plwTim = plwFile.GetTim(0);
                timFile = timFile.WithWeaponTexture(plwTim, 1);
                timFile = timFile.WithWeaponTexture(plwTim, 3);
            }

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);

            // Now copy over the skeleton and scale the EMR keyframes
            emdFile.SetEmr(0, emdFile.GetEmr(0).WithSkeleton(pldFile.GetEmr(0)).Scale(targetScale));
            emdFile.SetEmr(1, emdFile.GetEmr(1).Scale(targetScale));

            // Copy over the mesh (clear any extra parts)
            var builder = ((Md1)pldFile.GetMesh(0)).ToBuilder();
            var hairParts = builder.Parts.Skip(15).ToArray();
            if (builder.Parts.Count > 15)
                builder.Parts.RemoveRange(15, builder.Parts.Count - 15);

            // Add extra meshes
            var weaponMesh = builder.Parts[11];
            if (plwFile != null)
            {
                weaponMesh = ((Md1)plwFile.GetMesh(0)).ToBuilder().Parts[0];
            }

            if (type == Re2EnemyIds.ZombieBrad)
            {
                var zombieParts = new[] { 10, 0 };
                foreach (var zp in zombieParts)
                    builder.Parts.Add(builder.Parts[zp]);
            }
            else if (type == Re2EnemyIds.RobertKendo)
            {
                builder.Parts[11] = weaponMesh;
            }
            else if (type == Re2EnemyIds.MarvinBranagh)
            {
                var zombieParts = new[] { 13, 0, 8, 12, 14, 9, 10, 11, 11 };
                foreach (var zp in zombieParts)
                    builder.Parts.Add(builder.Parts[zp]);
                builder.Parts[builder.Parts.Count - 1] = weaponMesh;
            }
            else if (type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2)
            {
                builder.Add(weaponMesh);
                builder.Add();
            }
            else if (type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2 ||
                     type == Re2EnemyIds.SherryWithPendant || type == Re2EnemyIds.SherryWithClairesJacket)
            {
                builder.Add();
            }
            else if (type == Re2EnemyIds.AnnetteBirkin1 || type == Re2EnemyIds.AnnetteBirkin2)
            {
                builder.Add(weaponMesh);
            }
            else if (type == Re2EnemyIds.ClaireRedfield ||
                     type == Re2EnemyIds.ClaireRedfieldNoJacket ||
                     type == Re2EnemyIds.ClaireRedfieldCowGirl)
            {
                for (var i = 0; i < 4; i++)
                {
                    if (i < hairParts.Length)
                    {
                        builder.Add(hairParts[i]);
                    }
                }
                while (builder.Count < 15 + 4)
                {
                    builder.Add();
                }
            }

            emdFile.SetMesh(0, builder.ToMesh());

            // Marvin
            if (type == Re2EnemyIds.MarvinBranagh)
            {
                emdFile.SetMesh(0, emdFile.GetMesh(0).EditMeshTextures(m =>
                {
                    if (m.PartIndex >= 15 && m.PartIndex != 23)
                    {
                        m.Page += 2;
                    }
                    else if (m.Page == 0)
                    {
                        m.Page += 2;
                    }
                }));
            }

            // Ben and Irons need have morphing info that needs zeroing
            if (type == Re2EnemyIds.ChiefIrons1 ||
                type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2)
            {
                var morph = emdFile.GetMorph(0).ToBuilder();

                // Copy skeleton from EMR
                var emr = emdFile.GetEmr(0);
                var skel = Enumerable.Range(0, 15).Select(x => emr.GetRelativePosition(x)).ToArray();
                for (var i = 0; i < morph.Skeletons.Count; i++)
                {
                    morph.Skeletons[i] = skel;
                }

                // Copy positions from chest mesh to morph group 0
                var positionData = ((Md1)emdFile.GetMesh(0))
                    .ToBuilder().Parts[0].Positions
                    .Select(p => new Emr.Vector(p.x, p.y, p.z))
                    .ToArray();
                for (var i = 0; i < morph.Groups[0].Positions.Count; i++)
                {
                    morph.Groups[0].Positions[i] = positionData;
                }

                // Morph group 1 can just be zeros
                for (var i = 0; i < morph.Groups[1].Positions.Count; i++)
                {
                    morph.Groups[1].Positions[i] = new Emr.Vector[1];
                }

                emdFile.SetMorph(0, morph.ToMorphData());
            }

            // Ben and Irons need to have their chest on the right texture page
            if (type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2 ||
                type == Re2EnemyIds.BenBertolucci1 || type == Re2EnemyIds.BenBertolucci2)
            {
                var pageIndex = type == Re2EnemyIds.ChiefIrons1 || type == Re2EnemyIds.ChiefIrons2 ? 0 : 1;
                var mesh = (Md1)emdFile.GetMesh(0);
                if (EnsureChestOnPage(ref mesh, pageIndex))
                {
                    mesh = (Md1)mesh.EditMeshTextures(m =>
                    {
                        if (m.PartIndex == 0 || m.PartIndex == 9 || m.PartIndex == 12)
                        {
                            m.Page = pageIndex ^ 1;
                        }
                    });
                    timFile.SwapPages(0, 1);
                    emdFile.SetMesh(0, mesh);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetEmdPath));
            emdFile.Save(targetEmdPath);
            timFile.Save(Path.ChangeExtension(targetEmdPath, ".tim"));
        }

        private string? GetPlwPath(string pldPath, byte weapon)
        {
            var player = 0;
            var fileName = Path.GetFileNameWithoutExtension(pldPath);
            if (fileName.Equals("pl01", StringComparison.OrdinalIgnoreCase))
            {
                player = 1;
            }

            var plwFileName = $"PL{player:00}W{weapon:X2}.PLW";
            var pldDirectory = Path.GetDirectoryName(pldPath);
            var customPlwPath = Path.Combine(pldDirectory, plwFileName);
            if (File.Exists(customPlwPath))
            {
                return customPlwPath;
            }

            var originalPlwPath = _fileRepository.GetDataPath($"pl{player}/pld/{plwFileName}");
            if (File.Exists(originalPlwPath))
            {
                return originalPlwPath;
            }

            return null;
        }

        private static byte[] GetSuitableWeaponForNPC(byte npc)
        {
            return npc switch
            {
                Re2EnemyIds.MarvinBranagh => new[]
                {
                    Re2ItemIds.HandgunLeon,
                    Re2ItemIds.Magnum,
                    Re2ItemIds.ColtSAA,
                    Re2ItemIds.Shotgun,
                    Re2ItemIds.Bowgun,
                    Re2ItemIds.GrenadeLauncherExplosive,
                    Re2ItemIds.Sparkshot,
                    Re2ItemIds.SMG,
                    Re2ItemIds.Flamethrower,
                    Re2ItemIds.RocketLauncher,
                },
                Re2EnemyIds.RobertKendo => new[]
                {
                    Re2ItemIds.Shotgun,
                    Re2ItemIds.Bowgun,
                    Re2ItemIds.GrenadeLauncherExplosive,
                    Re2ItemIds.Sparkshot,
                    Re2ItemIds.SMG,
                    Re2ItemIds.Flamethrower,
                    Re2ItemIds.RocketLauncher,
                },
                _ => new[]
                {
                    Re2ItemIds.HandgunLeon,
                    Re2ItemIds.Magnum,
                    Re2ItemIds.ColtSAA
                },
            };
        }

        private bool EnsureChestOnPage(ref Md1 mesh, int page)
        {
            var builder = mesh.ToBuilder();
            var part0 = builder.Parts[0];
            if (part0.TriangleTextures.Count > 0)
            {
                if ((part0.TriangleTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            else if (part0.QuadTextures.Count > 0)
            {
                if ((part0.QuadTextures[0].page & 0x0F) == page)
                {
                    return false;
                }
            }
            mesh = (Md1)mesh.SwapPages(0, 1);
            return true;
        }

        [DebuggerDisplay("{Actor}")]
        private readonly struct ExternalCharacter
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
