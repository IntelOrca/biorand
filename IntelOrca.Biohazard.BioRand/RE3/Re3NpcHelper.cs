using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE3
{
    internal class Re3NpcHelper : INpcHelper
    {
        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re3EnemyIds.MarvinBranagh1:
                case Re3EnemyIds.MarvinBranagh2:
                    return "marvin";
                case Re3EnemyIds.MikhailViktor:
                    return "mikhail";
                case Re3EnemyIds.NikolaiZinoviev:
                case Re3EnemyIds.NikolaiDead:
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
                case Re3EnemyIds.ChiefIrons:
                    return "irons";
                default:
                    return null;
            }
        }

        public string GetNpcName(byte type)
        {
            var name = new Bio3ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public string[] GetPlayerActors(int player)
        {
            return new[] { "jill.re3", "carlos" };
        }

        public bool IsNpc(byte type)
        {
            return type >= Re3EnemyIds.CarlosOliveira1 || type == Re3EnemyIds.MarvinBranagh1;
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                Re3EnemyIds.MarvinBranagh1,
                Re3EnemyIds.MarvinBranagh2,
                Re3EnemyIds.NikolaiZinoviev,
                Re3EnemyIds.BradVickers,
                Re3EnemyIds.TyrellPatrick,
                Re3EnemyIds.PromoGirl,
                Re3EnemyIds.CarlosOliveira1,
                Re3EnemyIds.CarlosOliveira2,
                Re3EnemyIds.JillValentine1,
                Re3EnemyIds.JillValentine2,
                Re3EnemyIds.ChiefIrons
            };
            return defaultIncludeTypes;
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            switch (id)
            {
                case Re3EnemyIds.CarlosOliveira1:
                    return new byte[] {
                        Re3EnemyIds.MarvinBranagh1,
                        Re3EnemyIds.CarlosOliveira1,
                        Re3EnemyIds.NikolaiZinoviev,
                        Re3EnemyIds.BradVickers,
                        Re3EnemyIds.DarioRosso,
                        Re3EnemyIds.MurphySeeker,
                        Re3EnemyIds.TyrellPatrick,
                        Re3EnemyIds.MarvinBranagh2,
                        Re3EnemyIds.BradZombie,
                        Re3EnemyIds.DarioZombie,
                        Re3EnemyIds.PromoGirl,
                        Re3EnemyIds.NikolaiDead,
                        Re3EnemyIds.ChiefIrons
                    };
            default:
                    return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                // Includes any NPC that does not have 15 parts
                // case Re3EnemyIds.MarvinBranagh1:
                case Re3EnemyIds.CarlosOliveira1:
                case Re3EnemyIds.DarioRosso:
                case Re3EnemyIds.MurphySeeker:
                case Re3EnemyIds.MarvinBranagh2:
                case Re3EnemyIds.BradZombie:
                case Re3EnemyIds.DarioZombie:
                case Re3EnemyIds.PromoGirl:
                case Re3EnemyIds.JillValentine2:
                case Re3EnemyIds.ChiefIrons:
                    return true;
                default:
                    return false;
            }
        }
    }
}
