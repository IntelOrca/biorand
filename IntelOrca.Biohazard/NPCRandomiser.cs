#define ALWAYS_SWAP_NPC

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IntelOrca.Biohazard
{
    internal class NPCRandomiser
    {
        private static object g_lock = new object();
        private static VoiceInfo[] g_voiceInfo = new VoiceInfo[0];
        private static VoiceInfo[] g_available = new VoiceInfo[0];

        private readonly RandoLogger _logger;
        private readonly RandoConfig _config;
        private readonly string _originalDataPath;
        private readonly string _modPath;
        private readonly GameData _gameData;
        private readonly Map _map;
        private readonly Rng _random;
        private readonly List<VoiceInfo> _pool = new List<VoiceInfo>();
        private readonly HashSet<VoiceSample> _randomized = new HashSet<VoiceSample>();

        public NPCRandomiser(RandoLogger logger, RandoConfig config, string originalDataPath, string modPath, GameData gameData, Map map, Rng random)
        {
            _logger = logger;
            _config = config;
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _random = random;
            LoadVoiceInfo(originalDataPath);
        }

        public void Randomise()
        {
            _logger.WriteHeading("Randomizing Characters, Voices:");
            _pool.AddRange(g_available.Shuffle(_random));
            foreach (var rdt in _gameData.Rdts)
            {
                RandomizeRoom(_random.NextFork(), rdt);
            }
        }

        private void RandomizeRoom(Rng rng, Rdt rdt)
        {
            var playerActor = _config.Player == 0 ? "leon" : "claire";
            var defaultIncludeTypes = new[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 79, 80, 81, 84, 85, 88, 89, 90 };
            if (rng.Next(0, 8) != 0)
            {
                // Make it rare for player to also be an NPC
                defaultIncludeTypes = defaultIncludeTypes
                    .Where(x => GetActor((EnemyType)x) != playerActor)
                    .ToArray();
            }

            // Alternative costumes for Leon / Claire cause issues if there are multiple occurances
            // of them in the same cutscene. Only place them in rooms where we can guarantee there is only 1 NPC.
            var npcCount = rdt.Enemies.Count(x => IsNpc(x.Type));
            if (npcCount > 1)
            {
                var problematicTypes = new[] { 88, 89, 90 };
                defaultIncludeTypes = defaultIncludeTypes
                    .Except(problematicTypes)
                    .ToArray();
            }

            var room = _map.GetRoom(rdt.RdtId);
            if (room == null || room.Npcs == null)
                return;

            var actorToNewActorMap = new Dictionary<string, string>();
            foreach (var npc in room.Npcs)
            {
                if (npc.Player != null && npc.Player != _config.Player)
                    continue;
                if (npc.Scenario != null && npc.Scenario != _config.Scenario)
                    continue;

                var supportedNpcs = npc.IncludeTypes ?? defaultIncludeTypes;
                if (npc.ExcludeTypes != null)
                {
                    supportedNpcs = supportedNpcs.Except(npc.ExcludeTypes).ToArray();
                }
                if (supportedNpcs.Length == 0)
                {
                    continue;
                }
                foreach (var enemyGroup in rdt.Enemies.GroupBy(x => x.Type))
                {
                    if (!IsNpc(enemyGroup.Key))
                        continue;

                    supportedNpcs = supportedNpcs.Shuffle(rng);
                    foreach (var enemy in enemyGroup)
                    {
                        if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                            continue;

#if ALWAYS_SWAP_NPC
                        var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => GetActor((EnemyType)x) != GetActor(enemy.Type));
                        var newEnemyType = (EnemyType)(newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex]);
#else
                        var newEnemyType = (EnemyType)supportedNpcs[0];
#endif
                        var oldActor = GetActor(enemy.Type)!;
                        var newActor = GetActor(newEnemyType)!;
                        actorToNewActorMap[oldActor] = newActor;

                        _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{enemy.Type}] becomes [{newEnemyType}]");
                        enemy.Type = newEnemyType;
                    }
                }
            }

            foreach (var sound in rdt.Sounds)
            {
                var voice = new VoiceSample(_config.Player, rdt.RdtId.Stage + 1, sound.Id);
                var (actor, kind) = GetVoice(voice);
                if (actor != null)
                {
                    if (kind == "radio")
                    {
                        RandomizeVoice(rng, voice, actor, actor, kind);
                    }
                    if ((actor == playerActor && kind != "npc") || kind == "pc")
                    {
                        RandomizeVoice(rng, voice, actor, actor, null);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(rng, voice, actor, newActor, null);
                    }
                    else
                    {
                        RandomizeVoice(rng, voice, actor, actor, null);
                    }
                }
            }
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind)
        {
            if (_randomized.Contains(voice))
                return;

            var randomVoice = GetRandomVoice(rng, newActor, kind);
            if (randomVoice != null)
            {
                SetVoice(voice, randomVoice.Value);
                _randomized.Add(voice);
                _logger.WriteLine($"    {voice} [{actor}] becomes {randomVoice.Value} [{newActor}]");
            }
        }

        private VoiceSample? GetRandomVoice(Rng rng, string actor, string? kind)
        {
            var index = _pool.FindIndex(x => x.Actor == actor && ((kind == null && x.Kind != "radio") || x.Kind == kind));
            if (index == -1)
            {
                var newItems = g_voiceInfo.Where(x => x.Actor == actor).Shuffle(rng).ToArray();
                if (newItems.Length == 0)
                    return null;

                _pool.AddRange(newItems);
                index = _pool.Count - 1;
            }

            var voiceInfo = _pool[index];
            _pool.RemoveAt(index);
            return voiceInfo.Sample;
        }

        private void SetVoice(VoiceSample dst, VoiceSample src)
        {
            var srcPath = GetVoicePath(_originalDataPath, src);
            var dstPath = GetVoicePath(_modPath, dst);
            Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);
            File.Copy(srcPath, dstPath, true);
        }

        private static string GetVoicePath(string basePath, VoiceSample sample)
        {
            return Path.Combine(basePath, "PL" + sample.Player, "Voice", "stage" + sample.Stage, $"v{sample.Id:000}.sap");
        }

        private (string?, string?) GetVoice(VoiceSample sample)
        {
            var voiceInfo = g_voiceInfo.FirstOrDefault(x => x.Sample == sample);
            return (voiceInfo?.Actor, voiceInfo?.Kind);
        }

        private void ConvertSapFiles(string path)
        {
            var wavFiles = Directory.GetFiles(path, "*.wav", SearchOption.AllDirectories);
            var wavLen = wavFiles.GroupBy(x => new FileInfo(x).Length).Where(x => x.Count() > 1).ToArray();

            // var sapFiles = Directory.GetFiles(path, "*.sap", SearchOption.AllDirectories);
            // foreach (var sapFile in sapFiles)
            // {
            //     var wavFile = Path.ChangeExtension(sapFile, ".wav");
            //     var bytes = File.ReadAllBytes(sapFile);
            //     File.WriteAllBytes(wavFile, bytes.Skip(8).ToArray());
            //     File.Delete(sapFile);
            // }
        }

        private static bool IsNpc(EnemyType type) => type >= EnemyType.ChiefIrons1 && type != EnemyType.MayorsDaughter;

        private static string? GetActor(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.AdaWong1:
                case EnemyType.AdaWong2:
                    return "ada";
                case EnemyType.ClaireRedfield:
                case EnemyType.ClaireRedfieldCowGirl:
                case EnemyType.ClaireRedfieldNoJacket:
                    return "claire";
                case EnemyType.LeonKennedyBandaged:
                case EnemyType.LeonKennedyBlackLeather:
                case EnemyType.LeonKennedyCapTankTop:
                case EnemyType.LeonKennedyRpd:
                    return "leon";
                case EnemyType.SherryWithClairesJacket:
                case EnemyType.SherryWithPendant:
                    return "sherry";
                case EnemyType.MarvinBranagh:
                    return "marvin";
                case EnemyType.AnnetteBirkin1:
                case EnemyType.AnnetteBirkin2:
                    return "annette";
                case EnemyType.ChiefIrons1:
                case EnemyType.ChiefIrons2:
                    return "irons";
                case EnemyType.BenBertolucci1:
                case EnemyType.BenBertolucci2:
                    return "ben";
                case EnemyType.RobertKendo:
                    return "kendo";
                default:
                    return null;
            }
        }

        private static void LoadVoiceInfo(string originalDataPath)
        {
            lock (g_lock)
            {
                if (g_voiceInfo.Length == 0 || g_available.Length == 0)
                {
                    g_voiceInfo = LoadVoiceInfoFromJson();
                    g_available = RemoveDuplicateVoices(g_voiceInfo, originalDataPath);
                }
            }
        }

        private static VoiceInfo[] LoadVoiceInfoFromJson()
        {
            var json = Resources.voice;
            var voiceList = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var voiceInfos = new List<VoiceInfo>();
            foreach (var kvp in voiceList!)
            {
                var path = kvp.Key;
                var player = int.Parse(path.Substring(2, 1));
                var stage = int.Parse(path.Substring(15, 1));
                var id = int.Parse(path.Substring(18, 3));
                var sample = new VoiceSample(player, stage, id);
                var actorParts = kvp.Value.Split('_');
                voiceInfos.Add(new VoiceInfo(sample, actorParts[0], actorParts.Length >= 2 ? actorParts[1] : ""));
            }

            return voiceInfos.ToArray();
        }

        private static VoiceInfo[] RemoveDuplicateVoices(VoiceInfo[] voiceInfos, string originalDataPath)
        {
            var distinct = voiceInfos.ToList();
            foreach (var group in voiceInfos.GroupBy(x => GetVoiceSize(originalDataPath, x)))
            {
                if (group.Count() <= 1)
                    continue;

                foreach (var item in group.Skip(1))
                {
                    distinct.RemoveAll(x => x.Sample == item.Sample);
                }
            }
            return distinct.ToArray();
        }

        private static int GetVoiceSize(string basePath, VoiceInfo vi)
        {
            var path = GetVoicePath(basePath, vi.Sample);
            return (int)new FileInfo(path).Length;
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

    [DebuggerDisplay("Player = {Player} Stage = {Stage} Id = {Id}")]
    public struct VoiceSample : IEquatable<VoiceSample>
    {
        public VoiceSample(int player, int stage, int id)
        {
            Player = player;
            Stage = stage;
            Id = id;
        }

        public int Player { get; set; }
        public int Stage { get; set; }
        public int Id { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is VoiceSample sample && Equals(sample);
        }

        public bool Equals(VoiceSample other)
        {
            return Player == other.Player &&
                   Stage == other.Stage &&
                   Id == other.Id;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Player;
            hash = hash * 23 + Stage;
            hash = hash * 23 + Id;
            return hash;
        }

        public static bool operator ==(VoiceSample left, VoiceSample right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoiceSample left, VoiceSample right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"PL{Player}/Voice/stage{Stage}/v{Id:000}.sap";
        }
    }
}
