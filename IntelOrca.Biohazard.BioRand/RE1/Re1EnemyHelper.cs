using System;
using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    internal class Re1EnemyHelper : IEnemyHelper
    {
        private static readonly byte[] _zombieTypes = new byte[]
        {
            Re1EnemyIds.Zombie,
            Re1EnemyIds.ZombieNaked,
            Re1EnemyIds.ZombieResearcher
        };

        public string GetEnemyName(byte type)
        {
            var name = new Bio1ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public bool SupportsEnemyType(RandoConfig config, RandomizedRdt rdt, bool hasEnemyPlacements, byte enemyType)
        {
            if (enemyType == Re1EnemyIds.Yawn1)
            {
                return rdt.RdtId == new RdtId(1, 0x10);
            }
            else if (enemyType == Re1EnemyIds.Yawn2)
            {
                return rdt.RdtId == new RdtId(6, 0x0C);
            }
            return true;
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch (enemy.Type)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.WebSpinner:
                case Re1EnemyIds.Crow:
                case Re1EnemyIds.Hunter:
                case Re1EnemyIds.Wasp:
                case Re1EnemyIds.Chimera:
                case Re1EnemyIds.Snake:
                case Re1EnemyIds.Neptune:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Plant42Vines:
                case Re1EnemyIds.Yawn2:
                    return true;
                default:
                    return false;
            }
        }

        public void BeginRoom(RandomizedRdt rdt)
        {
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
            var originalState = enemy.State;
            switch (enemyType)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.WebSpinner:
                case Re1EnemyIds.BlackTiger:
                case Re1EnemyIds.Crow:
                case Re1EnemyIds.Hunter:
                case Re1EnemyIds.Wasp:
                case Re1EnemyIds.Chimera:
                case Re1EnemyIds.Snake:
                    enemy.State = 0;
                    break;
                case Re1EnemyIds.Neptune:
                    if (enemySpec.KeepState)
                        enemy.State = 0; // HACK make sure Attic Yawn gets big shark (small shark gets stuck)
                    else
                        enemy.State = (byte)rng.NextOf(0, 2);
                    break;
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Yawn2:
                case Re1EnemyIds.Plant42Vines:
                    enemy.State = 0;
                    break;
            }
            if (enemySpec.KeepState)
                enemy.State = (byte)(enemy.State | (originalState & 0x80));
        }

        public bool IsEnemy(byte type)
        {
            return type <= Re1EnemyIds.Yawn2;
        }

        public bool IsUniqueEnemyType(byte type)
        {
            switch (type)
            {
                case Re1EnemyIds.BlackTiger:
                case Re1EnemyIds.Plant42:
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Yawn2:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Tyrant2:
                    return true;
                default:
                    return false;
            }
        }

        public int GetEnemyTypeLimit(RandoConfig config, int difficulty, byte type)
        {
            byte[] limit;
            switch (type)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieResearcher:
                case Re1EnemyIds.ZombieNaked:
                    limit = new byte[] { 4, 6, 8, 12 };
                    break;
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.Chimera:
                case Re1EnemyIds.Neptune:
                    limit = new byte[] { 2, 3, 5, 8 };
                    break;
                case Re1EnemyIds.Hunter:
                    limit = new byte[] { 2, 3, 4, 6 };
                    break;
                case Re1EnemyIds.Tyrant1:
                    limit = new byte[] { 1, 2, 3, 4 };
                    break;
                case Re1EnemyIds.BlackTiger:
                    limit = new byte[] { 1, 1, 2, 2 };
                    break;
                case Re1EnemyIds.Yawn1:
                case Re1EnemyIds.Yawn2:
                    limit = new byte[] { 1 };
                    break;
                default:
                    limit = new byte[] { 16 };
                    break;
            }
            var index = Math.Min(limit.Length - 1, difficulty);
            return limit[index];
        }

        public byte[] GetReservedEnemyIds() => new byte[] { 14, 108 };

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Wasp", "Yellow", new[] { Re1EnemyIds.Wasp }),
            new SelectableEnemy("Crow", "Black", new[] { Re1EnemyIds.Crow }),
            new SelectableEnemy("Adder", "DarkOliveGreen", new[] { Re1EnemyIds.Snake }),
            new SelectableEnemy("Web Spinner", "YellowGreen", new[] { Re1EnemyIds.WebSpinner }),
            new SelectableEnemy("Zombie", "LightGray", _zombieTypes),
            new SelectableEnemy("Chimera", "Gray", new[] { Re1EnemyIds.Chimera }),
            new SelectableEnemy("Hunter", "IndianRed", new[] { Re1EnemyIds.Hunter }),
            new SelectableEnemy("Cerberus", "Black", new[] { Re1EnemyIds.Cerberus }),
            new SelectableEnemy("Neptune", "Purple", new[] { Re1EnemyIds.Neptune }),
            new SelectableEnemy("Tyrant", "DarkGray", new[] { Re1EnemyIds.Tyrant1 }),
            new SelectableEnemy("Yawn", "DarkOliveGreen", new[] { Re1EnemyIds.Yawn1, Re1EnemyIds.Yawn2 }),
            new SelectableEnemy("Black Tiger", "Black", new[] { Re1EnemyIds.BlackTiger }),
        };

        public byte[] GetRequiredEsps(byte enemyType)
        {
            switch (enemyType)
            {
                case Re1EnemyIds.Zombie:
                case Re1EnemyIds.ZombieNaked:
                case Re1EnemyIds.ZombieResearcher:
                    return new byte[] { 0x03, 0x04 };
                case Re1EnemyIds.Cerberus:
                case Re1EnemyIds.Snake:
                    return new byte[] { 0x04 };
                case Re1EnemyIds.WebSpinner:
                case Re1EnemyIds.BlackTiger:
                    return new byte[] { 0x1E };
                case Re1EnemyIds.Crow:
                    return new byte[] { 0x1C };
                case Re1EnemyIds.Hunter:
                    return new byte[] { 0x03, 0x04 };
                case Re1EnemyIds.Wasp:
                case Re1EnemyIds.Tyrant1:
                case Re1EnemyIds.Yawn1:
                default:
                    return new byte[0];
                case Re1EnemyIds.Chimera:
                    return new byte[] { 0x03, 0x04, 0x1D, 0x1F };
                case Re1EnemyIds.Neptune:
                    return new byte[] { 0x17, 0x25 };
                case Re1EnemyIds.Plant42Vines:
                    return new byte[] { 0x14, 0x1E };
            }
        }

        public void CreateZombie(byte type, EmdFile srcPld, EmdFile srcEmd, string dstPath)
        {
            var remap = new byte[] { 2, 0, 1, 12, 13, 14, 9, 10, 11, 6, 7, 8, 3, 4, 5 };
            var srcPart = new byte[] {
                255, 0, 1,
                1, 3, 4,
                1, 6, 7,
                0, 9, 10,
                0, 12, 13 };
            var targetScale = srcPld.CalculateEmrScale(srcEmd);

            var emrBuilder = srcEmd.GetEmr(1).ToBuilder();
            var pldEmr = srcPld.GetEmr(0);
            for (var i = 0; i < remap.Length; i++)
            {
                var pos = pldEmr.GetFinalPosition(remap[i]);
                if (srcPart[i] == 255)
                {
                    pos.x = 0;
                    pos.y = (short)(pos.y * targetScale);
                    pos.z = 0;
                }
                else
                {
                    var srcPos = pldEmr.GetFinalPosition(remap[srcPart[i]]);
                    pos.x -= srcPos.x;
                    pos.y -= srcPos.y;
                    pos.z -= srcPos.z;
                }
                if (i == 4 || i == 5)
                    pos = new Emr.Vector(pos.x, pos.z, (short)-pos.y);
                if (i == 7 || i == 8)
                    pos = new Emr.Vector((short)pos.x, (short)-pos.z, (short)pos.y);
                emrBuilder.RelativePositions[i] = pos;
            }
            srcEmd.SetEmr(1, emrBuilder.ToEmr().Scale(0.88 * targetScale));

            srcEmd.SetMesh(0, srcPld.GetMesh(0));
            var mesh = ((Tmd)srcEmd.GetMesh(0)).ToBuilder();
            while (mesh.Count > 15)
            {
                mesh.RemoveAt(mesh.Count - 1);
            }
            var copy = mesh.Parts.ToArray();
            for (var i = 0; i < remap.Length; i++)
            {
                mesh.Parts[i] = copy[remap[i]];
            }
            for (var j = 3; j < 6; j++)
            {
                for (var i = 0; i < mesh.Parts[j].Positions.Count; i++)
                {
                    var pos = mesh.Parts[j].Positions[i];
                    mesh.Parts[j].Positions[i] = new Tmd.Vector((short)pos.x, (short)pos.z, (short)-pos.y);
                }
            }
            for (var j = 6; j < 9; j++)
            {
                for (var i = 0; i < mesh.Parts[j].Positions.Count; i++)
                {
                    var pos = mesh.Parts[j].Positions[i];
                    mesh.Parts[j].Positions[i] = new Tmd.Vector((short)pos.x, (short)-pos.z, (short)pos.y);
                }
            }
            srcEmd.SetMesh(0, mesh.ToMesh());

            srcEmd.SetTim(0, srcPld.GetTim(0));

            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
            srcEmd.Save(dstPath);
        }
    }
}
