using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE1
{
    internal class Re1NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            var name = new Bio1ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            return new byte[]
            {
                Re1EnemyIds.ChrisStars,
                Re1EnemyIds.JillStars,
                Re1EnemyIds.BarryStars,
                Re1EnemyIds.RebeccaStars,
                Re1EnemyIds.WeskerStars,
                // Re1EnemyIds.Kenneth1,
                // Re1EnemyIds.Forrest,
                Re1EnemyIds.Richard,
                Re1EnemyIds.Enrico,
                // Re1EnemyIds.Kenneth2,
                // Re1EnemyIds.Barry2,
                // Re1EnemyIds.BarryStars2,
                // Re1EnemyIds.RebeccaStars2,
                // Re1EnemyIds.WeskerStars2,
                // Re1EnemyIds.ChrisJacket,
                // Re1EnemyIds.JillBlackShirt,
                // Re1EnemyIds.ChrisJacket2,
                // Re1EnemyIds.JillRedShirt
            };
        }

        public string[] GetPlayerActors(int player)
        {
            return player == 0 ? new[] { "chris", "rebecca" } : new[] { "jill", "barry" };
        }

        public bool IsNpc(byte type) => type >= Re1EnemyIds.ChrisStars;

        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.ChrisJacket:
                case Re1EnemyIds.ChrisJacket2:
                    return "chris";
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.JillBlackShirt:
                case Re1EnemyIds.JillRedShirt:
                    return "jill";
                case Re1EnemyIds.BarryStars:
                case Re1EnemyIds.Barry2:
                case Re1EnemyIds.BarryStars2:
                case Re1EnemyIds.Barry3:
                    return "barry";
                case Re1EnemyIds.RebeccaStars:
                case Re1EnemyIds.RebeccaStars2:
                    return "rebecca";
                case Re1EnemyIds.WeskerStars:
                case Re1EnemyIds.WeskerStars2:
                    return "wesker";
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Kenneth2:
                    return "kenneth";
                case Re1EnemyIds.Forrest:
                    return "forrest";
                case Re1EnemyIds.Richard:
                    return "richard";
                case Re1EnemyIds.Enrico:
                    return "enrico";
                default:
                    return null;
            }
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            if (id == Re1EnemyIds.ChrisStars)
            {
                return new[]
                {
                    Re1EnemyIds.ChrisStars,
                    Re1EnemyIds.JillStars,
                    Re1EnemyIds.BarryStars,
                    Re1EnemyIds.RebeccaStars,
                    Re1EnemyIds.WeskerStars
                };
            }
            else
            {
                return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.BarryStars:
                case Re1EnemyIds.RebeccaStars:
                case Re1EnemyIds.WeskerStars:
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Forrest:
                case Re1EnemyIds.Richard:
                case Re1EnemyIds.Enrico:
                    return true;
                default:
                    return false;
            }
        }

        public void CreateEmdFile(byte type, string pldPath, string baseEmdPath, string targetEmdPath, FileRepository fileRepository, Rng rng)
        {
            var pldFile = ModelFile.FromFile(pldPath);
            var emdFile = ModelFile.FromFile(baseEmdPath);
            var timFile = pldFile.GetTim(0);

            // First get how tall the new EMD is compared to the old one
            var targetScale = pldFile.CalculateEmrScale(emdFile);
            switch (type)
            {
                case Re1EnemyIds.Kenneth1:
                case Re1EnemyIds.Forrest:
                case Re1EnemyIds.Richard:
                case Re1EnemyIds.Enrico:
                case Re1EnemyIds.Kenneth2:
                case Re1EnemyIds.Barry2:
                    targetScale = 1;
                    break;
            }

            // Now copy over the skeleton and scale the EMR keyframes
            emdFile.SetEmr(0, emdFile.GetEmr(0).WithSkeleton(pldFile.GetEmr(0)).Scale(targetScale));

            // Copy over the mesh (clear any extra parts)
            var builder = ((Tmd)pldFile.GetMesh(0)).ToBuilder();
            if (builder.Parts.Count > 15)
                builder.Parts.RemoveRange(15, builder.Parts.Count - 15);

            // Add clip part (probably unused)
            switch (type)
            {
                case Re1EnemyIds.ChrisStars:
                case Re1EnemyIds.JillStars:
                case Re1EnemyIds.RebeccaStars:
                    builder.Add();
                    break;
            }

            emdFile.SetMesh(0, builder.ToMesh());
            emdFile.SetTim(0, timFile);

            Directory.CreateDirectory(Path.GetDirectoryName(targetEmdPath));
            emdFile.Save(targetEmdPath);
        }
    }
}
