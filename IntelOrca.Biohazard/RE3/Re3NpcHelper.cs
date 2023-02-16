using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3NpcHelper : INpcHelper
    {
        public string? GetActor(byte type)
        {
            switch (type)
            {
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
                case Re3EnemyIds.CarlosOliveira:
                    return "carlos";
                case Re3EnemyIds.PromoGirl:
                    return "promogirl";
                default:
                    return null;
            }
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                Re3EnemyIds.NikolaiZinoviev,
                Re3EnemyIds.BradVickers,
                Re3EnemyIds.DarioRosso,
                Re3EnemyIds.MurphySeeker,
                Re3EnemyIds.PromoGirl,
                Re3EnemyIds.CarlosOliveira
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
            return "jill";
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            return new byte[0];
        }

        public bool IsNpc(byte type)
        {
            return type > Re3EnemyIds.Nemesis3;
        }

        public bool IsSpareSlot(byte id)
        {
            return false;
        }
    }
}
