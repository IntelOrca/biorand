using System.Linq;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            var name = new Bio2ConstantTable().GetEnemyName(type);
            return name
                .Remove(0, 6)
                .Replace("_", " ");
        }

        public string[] GetPlayerActors(int player)
        {
            return player == 0 ? new[] { "leon", "ada" } : new[] { "claire", "sherry" };
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                // Re2EnemyIds.ChiefIrons1,
                Re2EnemyIds.AdaWong1,
                // Re2EnemyIds.ChiefIrons2,
                Re2EnemyIds.AdaWong2,
                Re2EnemyIds.BenBertolucci1,
                Re2EnemyIds.SherryWithPendant,
                Re2EnemyIds.BenBertolucci2,
                Re2EnemyIds.AnnetteBirkin1,
                // Re2EnemyIds.RobertKendo,
                Re2EnemyIds.AnnetteBirkin2,
                // Re2EnemyIds.MarvinBranagh,
                Re2EnemyIds.SherryWithClairesJacket,
                Re2EnemyIds.LeonKennedyRpd,
                Re2EnemyIds.ClaireRedfield };

            // Alternative costumes for Leon / Claire cause issues if there are multiple occurances
            // of them in the same cutscene. Only place them in rooms where we can guarantee there is only 1 NPC.
            var npcCount = rdt.Enemies.Count(x => IsNpc(x.Type));
            if (npcCount > 1)
            {
                var problematicTypes = new byte[] { 88, 89, 90 };
                defaultIncludeTypes = defaultIncludeTypes
                    .Except(problematicTypes)
                    .ToArray();
            }

            return defaultIncludeTypes;
        }

        public bool IsNpc(byte type) => type >= Re2EnemyIds.ChiefIrons1 && type != Re2EnemyIds.MayorsDaughter;

        public string? GetActor(byte type)
        {
            switch (type)
            {
                case Re2EnemyIds.AdaWong1:
                case Re2EnemyIds.AdaWong2:
                    return "ada";
                case Re2EnemyIds.ClaireRedfield:
                case Re2EnemyIds.ClaireRedfieldCowGirl:
                case Re2EnemyIds.ClaireRedfieldNoJacket:
                    return "claire";
                case Re2EnemyIds.LeonKennedyBandaged:
                case Re2EnemyIds.LeonKennedyBlackLeather:
                case Re2EnemyIds.LeonKennedyCapTankTop:
                case Re2EnemyIds.LeonKennedyRpd:
                    return "leon";
                case Re2EnemyIds.SherryWithClairesJacket:
                case Re2EnemyIds.SherryWithPendant:
                    return "sherry";
                case Re2EnemyIds.MarvinBranagh:
                    return "marvin";
                case Re2EnemyIds.AnnetteBirkin1:
                case Re2EnemyIds.AnnetteBirkin2:
                    return "annette";
                case Re2EnemyIds.ChiefIrons1:
                case Re2EnemyIds.ChiefIrons2:
                    return "irons";
                case Re2EnemyIds.BenBertolucci1:
                case Re2EnemyIds.BenBertolucci2:
                    return "ben";
                case Re2EnemyIds.RobertKendo:
                    return "kendo";
                default:
                    return null;
            }
        }

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            switch (id)
            {
                case Re2EnemyIds.LeonKennedyRpd:
                    return new byte[] { 0x48, 0x52, 0x54, 0x56, 0x58, 0x5A };
                case Re2EnemyIds.ClaireRedfield:
                    return new byte[] { 0x53, 0x55, 0x57, 0x5B };
                case Re2EnemyIds.SherryWithPendant:
                case Re2EnemyIds.SherryWithClairesJacket:
                    return new byte[] {
                        Re2EnemyIds.SherryWithPendant,
                        Re2EnemyIds.SherryWithClairesJacket
                    };
                case Re2EnemyIds.AdaWong1:
                case Re2EnemyIds.AdaWong2:
                    return new byte[] {
                        Re2EnemyIds.AdaWong1,
                        Re2EnemyIds.AdaWong2
                    };
                case Re2EnemyIds.BenBertolucci1:
                case Re2EnemyIds.BenBertolucci2:
                case Re2EnemyIds.ChiefIrons1:
                case Re2EnemyIds.ChiefIrons2:
                    return new byte[] {
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.BenBertolucci1,
                        Re2EnemyIds.ChiefIrons1,
                        Re2EnemyIds.ChiefIrons2
                    };
                default:
                    return new[] { id };
            }
        }

        public bool IsSpareSlot(byte id)
        {
            switch (id)
            {
                // Leon skins
                case 0x52:
                case 0x54:
                case 0x56:
                case 0x58:
                case 0x5A:

                // Claire skins
                case 0x53:
                case 0x55:
                case 0x57:
                case 0x59:
                case 0x5B:

                case Re2EnemyIds.SherryWithClairesJacket:
                case Re2EnemyIds.AdaWong2:
                case Re2EnemyIds.BenBertolucci2:
                case Re2EnemyIds.ChiefIrons2:
                    return true;
                default:
                    return false;
            }
        }
    }
}
