using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3EnemyHelper : IEnemyHelper
    {
        public void BeginRoom(Rdt rdt)
        {
        }

        public string GetEnemyName(byte type)
        {
            var name = new Bio3ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public int GetEnemyTypeLimit(RandoConfig config, byte type)
        {
            byte[] limit;
            switch (type)
            {
                case Re3EnemyIds.ZombieDog:
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.BS28:
                    limit = new byte[] { 2, 4, 6, 8 };
                    break;
                case Re3EnemyIds.Hunter:
                case Re3EnemyIds.HunterGamma:
                case Re3EnemyIds.Nemesis:
                case Re3EnemyIds.Nemesis3:
                    limit = new byte[] { 2, 3, 4, 6 };
                    break;
                default:
                    limit = new byte[] { 16 };
                    break;
            }
            var index = Math.Min(limit.Length - 1, config.EnemyDifficulty);
            return limit[index];
        }

        public SelectableEnemy[] GetSelectableEnemies() => new[]
        {
            new SelectableEnemy("Arms", "LightGray", Re3EnemyIds.Arm),
            new SelectableEnemy("Crow", "Black", Re3EnemyIds.Crow),
            new SelectableEnemy("Sliding Worm", "LightGray", Re3EnemyIds.SlidingWorm),
            new SelectableEnemy("Spider", "YellowGreen", Re3EnemyIds.Spider),
            new SelectableEnemy("Zombie", "LightGray", _zombieTypes),
            new SelectableEnemy("Hunter", "IndianRed", Re3EnemyIds.Hunter),
            new SelectableEnemy("Brain Sucker", "DarkOliveGreen", Re3EnemyIds.BS23),
            new SelectableEnemy("Zombie Dog", "Black", Re3EnemyIds.ZombieDog),
            new SelectableEnemy("Nemesis", "LightGray", Re3EnemyIds.Nemesis),
        };

        public bool IsEnemy(byte type)
        {
            return type != Re3EnemyIds.MarvinBranagh1 && type < Re3EnemyIds.CarlosOliveira1;
        }

        public bool IsUniqueEnemyType(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.GraveDigger:
                case Re3EnemyIds.HunterGamma:
                case Re3EnemyIds.Nemesis:
                case Re3EnemyIds.Nemesis3:
                    return true;
                default:
                    return false;
            }
        }

        public void SetEnemy(RandoConfig config, Rng rng, SceEmSetOpcode enemy, MapRoomEnemies enemySpec, byte enemyType)
        {
            switch (enemyType)
            {
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieGirl3:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                    if (!enemySpec.KeepState)
                        enemy.State = rng.NextOf<byte>(0, 1, 2, 3, 4, 6);
                    enemy.SoundBank = GetZombieSoundBank(enemyType);
                    break;
                case Re3EnemyIds.ZombieDog:
                    enemy.State = 0;
                    if (config.EnemyDifficulty >= 3)
                    {
                        // %50 of running
                        enemy.State = rng.NextOf<byte>(0, 2);
                    }
                    else if (config.EnemyDifficulty >= 2)
                    {
                        // %25 of running
                        enemy.State = rng.NextOf<byte>(0, 0, 0, 2);
                    }
                    enemy.SoundBank = 32;
                    break;
                case Re3EnemyIds.Crow:
                    enemy.State = 0;
                    enemy.SoundBank = 33;
                    break;
                case Re3EnemyIds.Hunter:
                    enemy.State = 0;
                    enemy.SoundBank = 34;
                    break;
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.BS28:
                case Re3EnemyIds.MiniBrainsucker:
                    enemy.State = 0;
                    enemy.SoundBank = 35;
                    break;
                case Re3EnemyIds.HunterGamma:
                    enemy.State = 0;
                    enemy.SoundBank = 36;
                    break;
                case Re3EnemyIds.Spider:
                    enemy.State = 0;
                    enemy.SoundBank = 37;
                    break;
                case Re3EnemyIds.MiniSpider:
                    enemy.State = 2;
                    enemy.SoundBank = 38;
                    break;
                case Re3EnemyIds.Arm:
                    enemy.State = 0;
                    enemy.SoundBank = 31;
                    break;
                case Re3EnemyIds.SlidingWorm:
                    enemy.State = 0;
                    enemy.SoundBank = 49;
                    break;
                case Re3EnemyIds.Nemesis:
                case Re3EnemyIds.Nemesis3:
                    enemy.State = rng.NextOf<byte>(0, 1);
                    enemy.SoundBank = 54;
                    break;
            }
        }

        public bool ShouldChangeEnemy(RandoConfig config, SceEmSetOpcode enemy)
        {
            switch (enemy.Type)
            {
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieGirl3:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                case Re3EnemyIds.ZombieDog:
                case Re3EnemyIds.Hunter:
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.Spider:
                case Re3EnemyIds.MiniSpider:
                case Re3EnemyIds.MiniBrainsucker:
                case Re3EnemyIds.BS28:
                    return true;
                default:
                    return false;
            }
        }

        public bool SupportsEnemyType(RandoConfig config, Rdt rdt, string difficulty, bool hasEnemyPlacements, byte enemyType)
        {
            // These enemies always work
            switch (enemyType)
            {
                case Re3EnemyIds.Hunter:
                case Re3EnemyIds.BS23:
                case Re3EnemyIds.ZombieDog:
                case Re3EnemyIds.Nemesis:
                    return true;
            }

            // Enemies that can already be in the room will work
            var existingEnemyTypes = rdt.Enemies
                .Select(x => x.Type)
                .Where(IsEnemy)
                .ToArray();

            if (existingEnemyTypes.Contains(enemyType))
                return true;

            return false;
        }

        private static bool IsZombie(byte type)
        {
            return _zombieTypes.Contains(type);
        }

        private static byte GetZombieSoundBank(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.ZombieGirl1:
                case Re3EnemyIds.ZombieGirl2:
                case Re3EnemyIds.ZombieGirl3:
                    return 3;
                case Re3EnemyIds.ZombieGuy1:
                case Re3EnemyIds.ZombieFat:
                case Re3EnemyIds.ZombieRpd1:
                case Re3EnemyIds.ZombieGuy2:
                case Re3EnemyIds.ZombieGuy3:
                case Re3EnemyIds.ZombieGuy4:
                case Re3EnemyIds.ZombieNaked:
                case Re3EnemyIds.ZombieGuy5:
                case Re3EnemyIds.ZombieGuy6:
                case Re3EnemyIds.ZombieLab:
                case Re3EnemyIds.ZombieRpd2:
                case Re3EnemyIds.ZombieGuy7:
                case Re3EnemyIds.ZombieGuy8:
                    return 4;
                // return 1;
                // return 2;
                // return 4;
                // return 5;
                // return 6;
                // return 7;
                // return 8;
                // return 10;
                // return 11;
                // return 13;
                // return 17;
                // return 18;
                // return 19;
                default:
                    return 0;
            }
        }

        public void CreateZombie(byte type, PldFile srcPld, EmdFile srcEmd, string dstPath)
        {
            var tim = srcPld.GetTim(0);

            var targetScale = srcPld.CalculateEmrScale(srcEmd) * 0.85;
            if (type < Re3EnemyIds.ZombieGuy3 || type == Re3EnemyIds.ZombieGuy4)
            {
                srcEmd.SetEmr(0, srcEmd.GetEmr(0).WithSkeleton(srcPld.GetEmr(0)).Scale(targetScale));
                srcEmd.SetEmr(1, srcEmd.GetEmr(1).Scale(targetScale));
                var mesh1 = srcPld.GetMesh(0).ToBuilder();
                while (mesh1.Count > 15)
                {
                    mesh1.RemoveAt(mesh1.Count - 1);
                }
                if (type == Re3EnemyIds.ZombieGuy4)
                {
                    mesh1.Add(mesh1[0]);
                }
                srcEmd.SetMesh(1, mesh1.ToMesh());

                // Copy mesh 1 to mesh 0
                var mesh0 = srcEmd.GetMesh(0).ToBuilder();
                if (mesh0.Count != 0)
                {
                    for (var i = 0; i < mesh0.Count; i++)
                    {
                        mesh0[i] = mesh1[i];
                    }
                    srcEmd.SetMesh(0, mesh0.ToMesh());
                }

                srcEmd.SetMorph(0, srcPld.GetMorph(0));
            }
            else
            {
                srcEmd.SetEmr(0, ConvertToZombieEmr(srcEmd.GetEmr(0), srcPld.GetEmr(0)).Scale(targetScale));
                srcEmd.SetEmr(1, srcEmd.GetEmr(1).Scale(targetScale));
                srcEmd.SetMesh(0, ConvertToZombieMesh(srcPld.GetMesh(0), srcPld.GetEmr(0)));
                srcEmd.SetMesh(1, srcEmd.GetMesh(0));

                var m = srcEmd.GetMesh(0).ToBuilder();
                m.Add(m[1]);
                srcEmd.SetMesh(0, m.ToMesh());
            }

            var copyParts = GetZombieMesh0Parts(type);

            Directory.CreateDirectory(Path.GetDirectoryName(dstPath));
            srcEmd.Save(dstPath);
            tim.Save(Path.ChangeExtension(dstPath, ".tim"));
        }

        private static Emr ConvertToZombieEmr(Emr baseEmr, Emr pldEmr)
        {
            var remap = new byte[] { 0, 1, 2, 3, 5, 6, 8, 9, 10, 12, 13 };

            var e = baseEmr.ToBuilder();
            for (var i = 0; i < remap.Length; i++)
            {
                e.RelativePositions[i] = pldEmr.GetRelativePosition(remap[i]);
            }
            return e.ToEmr();
        }

        private static IModelMesh ConvertToZombieMesh(IModelMesh mesh, Emr emr)
        {
            var b = ((Md2)mesh).ToBuilder();
            while (b.Count > 15)
                b.RemoveAt(b.Count - 1);

            Append(b.Parts[3], Translate(b.Parts[4], emr.GetRelativePosition(4)));
            Append(b.Parts[6], Translate(b.Parts[7], emr.GetRelativePosition(7)));
            Append(b.Parts[10], Translate(b.Parts[11], emr.GetRelativePosition(11)));
            Append(b.Parts[13], Translate(b.Parts[14], emr.GetRelativePosition(14)));

            b.RemoveAt(14);
            b.RemoveAt(11);
            b.RemoveAt(7);
            b.RemoveAt(4);
            return b.ToMesh();
        }

        private static Md2.Builder.Part Append(Md2.Builder.Part target, Md2.Builder.Part source)
        {
            var basePosition = (byte)target.Positions.Count;

            target.Positions.AddRange(source.Positions);
            target.Normals.AddRange(source.Normals);

            var triangles = source.Triangles.ToArray();
            for (var i = 0; i < triangles.Length; i++)
            {
                triangles[i].v0 += basePosition;
                triangles[i].v1 += basePosition;
                triangles[i].v2 += basePosition;
            }

            var quads = source.Quads.ToArray();
            for (var i = 0; i < quads.Length; i++)
            {
                quads[i].v0 += basePosition;
                quads[i].v1 += basePosition;
                quads[i].v2 += basePosition;
                quads[i].v3 += basePosition;
            }

            target.Triangles.AddRange(triangles);
            target.Quads.AddRange(quads);
            return target;
        }

        private static Md2.Builder.Part Translate(Md2.Builder.Part part, Emr.Vector offset)
        {
            for (var i = 0; i < part.Positions.Count; i++)
            {
                var pos = part.Positions[i];
                pos.x += offset.x;
                pos.y += offset.y;
                pos.z += offset.z;
                part.Positions[i] = pos;
            }
            return part;
        }

        private static byte[] GetZombieMesh0Parts(byte type)
        {
            // 0 CHEST, HEAD,
            // 2 LEFT ARM UPPER, LEFT FOREARM, LEFT HAND,
            // 5 RIGHT ARM UPPER, RIGHT FOREARM, RIGHT HAND,
            // 8 WAIST
            // 9 LEFT THIGH, LEFT CALF, LEFT FOOT,
            // 12 RIGHT THIGH, RIGHT CALF, RIGHT FOOT,
            switch (type)
            {
                case Re3EnemyIds.ZombieGuy1:
                    return new byte[] { 0, 2, 5, 8, 9, 10, 12, 13 };
                case Re3EnemyIds.ZombieGirl2:
                    return new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
            }
            return null;
        }

        private static readonly byte[] _zombieTypes = new byte[]
        {
            Re3EnemyIds.ZombieGuy1,
            Re3EnemyIds.ZombieGirl1,
            Re3EnemyIds.ZombieFat,
            Re3EnemyIds.ZombieGirl2,
            Re3EnemyIds.ZombieRpd1,
            Re3EnemyIds.ZombieGuy2,
            Re3EnemyIds.ZombieGuy3,
            Re3EnemyIds.ZombieGuy4,
            Re3EnemyIds.ZombieNaked,
            Re3EnemyIds.ZombieGuy5,
            Re3EnemyIds.ZombieGuy6,
            Re3EnemyIds.ZombieLab,
            Re3EnemyIds.ZombieGirl3,
            Re3EnemyIds.ZombieRpd2,
            Re3EnemyIds.ZombieGuy7,
            Re3EnemyIds.ZombieGuy8,
        };
    }
}
