using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace rer
{
    internal class NPCRandomiser
    {
        private string _originalDataPath;
        private string _modPath;
        private GameData _gameData;
        private Map _map;
        private Random _random;
        private VoiceInfo[] _voiceInfo;
        private List<VoiceInfo> _pool = new List<VoiceInfo>();

        public NPCRandomiser(string originalDataPath, string modPath, GameData gameData, Map map, Random random)
        {
            _originalDataPath = originalDataPath;
            _modPath = modPath;
            _gameData = gameData;
            _map = map;
            _random = random;
            _voiceInfo = LoadVoiceInfo(originalDataPath);
        }

        private static VoiceInfo[] LoadVoiceInfo(string originalDataPath)
        {
            var json = File.ReadAllText(@"M:\git\rer\rer\data\voice.json");
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
                voiceInfos.Add(new VoiceInfo()
                {
                    Sample = sample,
                    Actor = kvp.Value
                });
            }

            // Remove duplicate samples
            foreach (var group in voiceInfos.GroupBy(x => GetVoiceSize(originalDataPath, x)))
            {
                if (group.Count() <= 1)
                    continue;

                foreach (var item in group.Skip(1))
                {
                    voiceInfos.RemoveAll(x => x.Sample == item.Sample);
                }
            }

            return voiceInfos.ToArray();
        }

        private static int GetVoiceSize(string basePath, VoiceInfo vi)
        {
            var path = GetVoicePath(basePath, vi.Sample);
            return (int)new FileInfo(path).Length;
        }

        public void Randomise()
        {
            ConvertSapFiles(@"M:\temp\re2converted\voice");

            _pool.AddRange(_voiceInfo.Shuffle(_random));

            Console.WriteLine($"Randomising NPCs:");
            foreach (var rdt in _gameData.Rdts)
            {
                var room = _map.GetRoom(rdt.RdtId);
                if (room == null || room.SupportedNpcs == null || room.SupportedNpcs.Length == 0)
                    continue;

                var currentNpcs = rdt.Enemies.Where(x => IsNpc(x.Type)).Select(x => x.Type).ToArray();
                var currentActors = currentNpcs.Select(x => GetActor(x)).ToArray();
                var npcs = room.SupportedNpcs.Shuffle(_random);
                foreach (var enemy in rdt.Enemies)
                {
                    if (IsNpc(enemy.Type))
                    {
                        var currentNpcIndex = Array.IndexOf(currentNpcs, enemy.Type);
                        var newNpcType = (EnemyType)npcs[currentNpcIndex % npcs.Length];
                        Console.WriteLine($"    {rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{enemy.Type}] becomes [{newNpcType}]");
                        enemy.Type = newNpcType;
                    }
                }

                foreach (var sound in rdt.Sounds)
                {
                    var actor = GetVoice(rdt.RdtId.Stage + 1, sound.Id);
                    if (actor != null)
                    {
                        var voice = new VoiceSample(1, rdt.RdtId.Stage + 1, sound.Id);
                        var currentNpcIndex = Array.IndexOf(currentActors, actor);
                        if (currentNpcIndex != -1)
                        {
                            var newNpcType = (EnemyType)npcs[currentNpcIndex % npcs.Length];
                            var newActor = GetActor(newNpcType);
                            var randomVoice = GetRandomVoice(newActor);
                            if (randomVoice != null)
                            {
                                SetVoice(voice, randomVoice.Value);
                                Console.WriteLine($"    {voice} [{actor}] becomes {randomVoice.Value} [{newActor}]");
                            }
                        }
                    }
                }
            }
        }

        private VoiceSample? GetRandomVoice(string actor)
        {
            var index = _pool.FindIndex(x => x.Actor == actor);
            if (index == -1)
            {
                var newItems = _voiceInfo.Where(x => x.Actor == actor).Shuffle(_random).ToArray();
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

        private string? GetVoice(int stage, int id)
        {
            var voiceInfo = _voiceInfo.FirstOrDefault(x => x.Sample.Stage == stage && x.Sample.Id == id);
            return voiceInfo?.Actor;
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

        private static bool IsNpc(EnemyType type) => type >= EnemyType.ChiefIrons1;

        private static string GetActor(EnemyType type)
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
    }

    [DebuggerDisplay("[{Actor}] {Sample}")]
    internal class VoiceInfo
    {
        public VoiceSample Sample { get; set; }
        public string? Actor { get; set; }
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
            return HashCode.Combine(Player, Stage, Id);
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
