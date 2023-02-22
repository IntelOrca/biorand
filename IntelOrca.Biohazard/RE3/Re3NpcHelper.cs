using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3NpcHelper : INpcHelper
    {
        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.MarvinBranagh:
                    return "marvin";
                case Re3EnemyIds.MikhailViktor:
                    return "mikhail";
                case Re3EnemyIds.NikolaiZinoviev:
                    return "nikolai";
                case Re3EnemyIds.BradVickers:
                case Re3EnemyIds.BradZombie:
                    return "brad";
                case Re3EnemyIds.DarioRosso:
                case Re3EnemyIds.DarioZombie:
                    return "dario";
                case Re3EnemyIds.MurphySeeker:
                    return "murphy";
                case Re3EnemyIds.TyrellPatrick:
                    return "tyrell";
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.CarlosOliveira2:
                    return "carlos";
                case Re3EnemyIds.PromoGirl:
                    return "promogirl";
                case Re3EnemyIds.JillValentine1:
                case Re3EnemyIds.JillValentine2:
                    return "jill.re3";
                default:
                    return null;
            }
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                Re3EnemyIds.MarvinBranagh,
                Re3EnemyIds.NikolaiZinoviev,
                Re3EnemyIds.BradVickers,
                Re3EnemyIds.DarioRosso,
                Re3EnemyIds.MurphySeeker,
                Re3EnemyIds.TyrellPatrick,
                Re3EnemyIds.PromoGirl,
                Re3EnemyIds.CarlosOliveira1,
                Re3EnemyIds.CarlosOliveira2,
                Re3EnemyIds.JillValentine1,
                Re3EnemyIds.JillValentine2
            };
            return defaultIncludeTypes;
        }

        public string GetNpcName(byte type)
        {
            var name = new Bio3ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public string GetPlayerActor(int player)
        {
            return "jill.re3";
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            return new byte[0];
        }

        public bool IsNpc(byte type)
        {
            return type >= Re3EnemyIds.CarlosOliveira1 || type == Re3EnemyIds.MarvinBranagh;
        }

        public bool IsSpareSlot(byte id)
        {
            return false;
        }
    }
}
