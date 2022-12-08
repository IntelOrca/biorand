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

        private VoiceSample[] voiceInfo = new VoiceSample[0];
        private VoiceSample[] available = new VoiceSample[0];

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
            _pool.AddRange(available.Shuffle(_random));
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
            var npcs = room?.Npcs;
            if (npcs == null)
                npcs = new[] { new MapRoomNpcs() };

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

            var actors = actorToNewActorMap.Values.Append(playerActor).ToArray();
            foreach (var sample in available)
            {
                if (sample.Player == _config.Player && rdt.RdtId.ToString() == sample.Rdt)
                {
                    if (sample.NoReplace)
                    {
                        continue;
                    }

                    var actor = sample.Actor!;
                    var kind = sample.Kind;
                    if (kind == "radio")
                    {
                        RandomizeVoice(voiceRng, sample, actor, actor, sample.Kind, actors);
                    }
                    if ((actor == playerActor && kind != "npc") || kind == "pc")
                    {
                        RandomizeVoice(voiceRng, sample, actor, actor, null, actors);
                    }
                    else if (actorToNewActorMap.TryGetValue(actor, out var newActor))
                    {
                        RandomizeVoice(voiceRng, sample, actor, newActor, null, actors);
                    }
                    else
                    {
                        RandomizeVoice(voiceRng, sample, actor, actor, null, actors);
                    }
                }
            }
        }

        private void RandomizeVoice(Rng rng, VoiceSample voice, string actor, string newActor, string? kind, string[] actors)
        {
            if (_randomized.Contains(voice))
                return;

            var randomVoice = GetRandomVoice(rng, newActor, kind, actors);
            if (randomVoice != null)
            {
                SetVoice(voice, randomVoice);
                _randomized.Add(voice);
                _logger.WriteLine($"    {voice.Path} [{actor}] becomes {randomVoice.Path} [{newActor}]");
            }
        }

        private VoiceSample? GetRandomVoice(Rng rng, string actor, string? kind, string[] actors)
        {
            var index = _pool.FindIndex(x => x.Actor == actor && ((kind == null && x.Kind != "radio") || x.Kind == kind) && x.CheckConditions(actors));
            if (index == -1)
            {
                var newItems = voiceInfo
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
                Stream inputStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read);
                Stream wavStream;
                if (srcPath.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                {
                    inputStream.Position = 8;
                    wavStream = new MemoryStream();
                    var decoder = new ADPCMDecoder();
                    decoder.Convert(inputStream, wavStream);
                    wavStream.Position = 0;
                }
                else
                {
                    wavStream = inputStream;
                }

                // Assume 8-bit
                WaveHeader header;
                var pcm = new byte[0];
                using (wavStream)
                {
                    var br = new BinaryReader(wavStream);
                    header = br.ReadStruct<WaveHeader>();

                    // Move to start
                    var startOffset = ((int)(src.Start * header.nAvgBytesPerSec) / header.nBlockAlign) * header.nBlockAlign;
                    var endOffset = src.End == 0 ?
                        (int)wavStream.Length :
                        ((int)(src.End * header.nAvgBytesPerSec) / header.nBlockAlign) * header.nBlockAlign;
                    var length = endOffset - startOffset;
                    wavStream.Position += startOffset;
                    pcm = br.ReadBytes(length);
                }

                // Create new file
                header.nRiffLength = (uint)(pcm.Length + 44 - 8);
                header.nDataLength = (uint)pcm.Length;
                using (var fs = new FileStream(dstPath, FileMode.Create))
                {
                    var bw = new BinaryWriter(fs);
                    if (dstPath.EndsWith(".sap", StringComparison.OrdinalIgnoreCase))
                    {
                        bw.Write((ulong)1);
                    }
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

        private void LoadVoiceInfo(string originalDataPath)
        {
            if (voiceInfo.Length == 0 || available.Length == 0)
            {
                voiceInfo = LoadVoiceInfoFromJson();
                available = voiceInfo;
                // available = RemoveDuplicateVoices(voiceInfo, originalDataPath);
            }
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
                if (sample.Actors != null)
                {
                    var start = 0.0;
                    var noReplace = false;
                    foreach (var sub in sample.Actors)
                    {
                        var slice = sample.CreateSlice(sub.Actor!, start, sub.Split);
                        slice.NoReplace = noReplace;
                        start = sub.Split;
                        samples.Add(slice);
                        noReplace = true;
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

    [DebuggerDisplay("Actor = {Actor} RdtId = {Rdt} Path = {Path}")]
    public class VoiceSample
    {
        public string? Path { get; set; }
        public string? Actor { get; set; }
        public string? Kind { get; set; }
        public string? Rdt { get; set; }
        public int? Player { get; set; }

        public double Start { get; set; }
        public double End { get; set; }

        public string? Condition { get; set; }

        public bool NoReplace { get; set; }
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
