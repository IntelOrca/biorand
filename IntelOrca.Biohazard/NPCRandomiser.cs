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
        private static object g_lock = new object();
        private static VoiceSample[] g_voiceInfo = new VoiceSample[0];
        private static VoiceSample[] g_available = new VoiceSample[0];

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
            _pool.AddRange(g_available.Shuffle(_random));
            foreach (var rdt in _gameData.Rdts)
            {
                RandomizeRoom(_random.NextFork(), rdt);
            }
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
                    if (!_npcHelper.IsNpc(enemyGroup.Key))
                        continue;

                    supportedNpcs = supportedNpcs.Shuffle(npcRng);
                    foreach (var enemy in enemyGroup)
                    {
                        if (npc.IncludeOffsets != null && !npc.IncludeOffsets.Contains(enemy.Offset))
                            continue;

#if ALWAYS_SWAP_NPC
                        var newEnemyTypeIndex = Array.FindIndex(supportedNpcs, x => _npcHelper.GetActor(x) != _npcHelper.GetActor(enemy.Type));
                        var newEnemyType = newEnemyTypeIndex == -1 ? supportedNpcs[0] : supportedNpcs[newEnemyTypeIndex];
#else
                        var newEnemyType = supportedNpcs[0];
#endif
                        var oldActor = _npcHelper.GetActor(enemy.Type)!;
                        var newActor = _npcHelper.GetActor(newEnemyType)!;
                        actorToNewActorMap[oldActor] = newActor;

                        _logger.WriteLine($"{rdt.RdtId}:{enemy.Id} (0x{enemy.Offset:X}) [{_npcHelper.GetNpcName(enemy.Type)}] becomes [{_npcHelper.GetNpcName(newEnemyType)}]");
                        enemy.Type = (byte)newEnemyType;
                    }
                }
            }

            foreach (var sample in g_available)
            {
                if (sample.Player == _config.Player && rdt.RdtId.ToString() == sample.Rdt)
                {
                    if (sample.Start != 0)
                    {
                        continue;
                    }

                    var actor = sample.Actor!;
                    if (actor == playerActor)
                    {
                        RandomizeVoice(voiceRng, sample, playerActor, playerActor, null);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(voiceRng, sample, actor, newActor, null);
                    }
                }
            }

            // foreach (var sound in rdt.Sounds)
            // {
            //     var voice = new VoiceSample(_config.Player, rdt.RdtId.Stage + 1, sound.Id);
            //     var (actor, kind) = GetVoice(voice);
            //     if (actor != null)
            //     {
            //         if (kind == "radio")
            //         {
            //             RandomizeVoice(rng, voice, actor, actor, kind);
            //         }
            //         if ((actor == playerActor && kind != "npc") || kind == "pc")
            //         {
            //             RandomizeVoice(rng, voice, actor, actor, null);
            //         }
            //         else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
            //         {
            //             RandomizeVoice(rng, voice, actor, newActor, null);
            //         }
            //         else
            //         {
            //             RandomizeVoice(rng, voice, actor, actor, null);
            //         }
            //     }
            // }
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind)
        {
            if (_randomized.Contains(voice))
                return;

            var randomVoice = GetRandomVoice(rng, newActor, kind);
            if (randomVoice != null)
            {
                SetVoice(voice, randomVoice);
                _randomized.Add(voice);
                _logger.WriteLine($"    {voice.Path} [{actor}] becomes {randomVoice.Path} [{newActor}]");
            }
        }

        private VoiceSample? GetRandomVoice(Rng rng, string actor, string? kind)
        {
            var index = _pool.FindIndex(x => x.Actor == actor && ((kind == null && x.Kind != "radio") || x.Kind == kind));
            if (index == -1)
            {
                var newItems = g_voiceInfo
                    .Where(x => x.Actor == actor)
                    .Shuffle(rng)
                    .ToArray();
                if (newItems.Length == 0)
                    return null;

                _pool.AddRange(newItems);
                index = _pool.Count - 1;
            }

            var sample = _pool[index];
            _pool.RemoveAt(index);
            return sample;
        }

        private void SetVoice(VoiceSample dst, VoiceSample src)
        {
            var srcPath = GetVoicePath(_originalDataPath, src);
            var dstPath = GetVoicePath(_modPath, dst);
            Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);
            if (src.Start == 0 && src.End == 0)
            {
                File.Copy(srcPath, dstPath, true);
            }
            else
            {
                // Assume 8-bit
                WaveHeader header;
                var pcm = new byte[0];
                using (var fs = new FileStream(srcPath, FileMode.Open, FileAccess.Read))
                {
                    var br = new BinaryReader(fs);
                    header = br.ReadStruct<WaveHeader>();

                    // Move to start
                    var startOffset = ((int)(src.Start * header.nAvgBytesPerSec) / header.nBlockAlign) * header.nBlockAlign;
                    var endOffset = src.End == 0 ?
                        (int)fs.Length :
                        ((int)(src.End * header.nAvgBytesPerSec) / header.nBlockAlign) * header.nBlockAlign;
                    var length = endOffset - startOffset;
                    fs.Position += startOffset;
                    pcm = br.ReadBytes(length);
                }

                // Create new file
                header.nRiffLength = (uint)(pcm.Length + 44 - 8);
                header.nDataLength = (uint)pcm.Length;
                using (var fs = new FileStream(dstPath, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    bw.Write(header);
                    bw.Write(pcm);
                }
            }
        }

        private static string GetVoicePath(string basePath, VoiceSample sample)
        {
            return Path.Combine(basePath, sample.Path);
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

        private static VoiceSample[] LoadVoiceInfoFromJson()
        {
            var json = Resources.re1_voice;
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
                if (sample.Actors != null)
                {
                    var start = 0.0;
                    foreach (var sub in sample.Actors)
                    {
                        var slice = sample.CreateSlice(sub.Actor!, start, sub.Split);
                        start = sub.Split;
                        samples.Add(slice);
                    }
                }
                else
                {
                    samples.Add(sample);
                }
            }

            return samples.ToArray();
        }

        private static VoiceSample[] RemoveDuplicateVoices(VoiceSample[] samples, string originalDataPath)
        {
            var distinct = samples.ToList();
            foreach (var group in samples.GroupBy(x => GetVoiceSize(originalDataPath, x)))
            {
                if (group.Count() <= 1)
                    continue;

                foreach (var item in group.Skip(1))
                {
                    distinct.RemoveAll(x => x.Start == 0 && x.End == 0 && x.Path == item.Path);
                }
            }
            return distinct.ToArray();
        }

        private static int GetVoiceSize(string basePath, VoiceSample sample)
        {
            var path = GetVoicePath(basePath, sample);
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

    [DebuggerDisplay("Actor = {Actor} RdtId = {RdtId} Path = {Path}")]
    public class VoiceSample
    {
        public string? Path { get; set; }
        public string? Actor { get; set; }
        public string? Kind { get; set; }
        public string? Rdt { get; set; }
        public int? Player { get; set; }

        public double Start { get; set; }
        public double End { get; set; }

        public VoiceSampleSplit[]? Actors { get; set; }

        public VoiceSample CreateSlice(string actor, double start, double end)
        {
            return new VoiceSample()
            {
                Path = Path,
                Actor = actor,
                Rdt = Rdt,
                Player = Player,
                Start = start,
                End = end
            };
        }
    }

    public class VoiceSampleSplit
    {
        public string? Actor { get; set; }
        public double Split { get; set; }
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
