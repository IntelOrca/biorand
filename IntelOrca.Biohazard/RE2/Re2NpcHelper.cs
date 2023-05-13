using System.Linq;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            return ((EnemyType)type).ToString();
        }

        public string[] GetPlayerActors(int player)
        {
            return player == 0 ? new[] { "leon", "ada" } : new[] { "claire", "sherry" };
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] {
                // (byte)EnemyType.ChiefIrons1,
                (byte)EnemyType.AdaWong1,
                // (byte)EnemyType.ChiefIrons2,
                (byte)EnemyType.AdaWong2,
                (byte)EnemyType.BenBertolucci1,
                (byte)EnemyType.SherryWithPendant,
                (byte)EnemyType.BenBertolucci2,
                (byte)EnemyType.AnnetteBirkin1,
                // (byte)EnemyType.RobertKendo,
                (byte)EnemyType.AnnetteBirkin2,
                // (byte)EnemyType.MarvinBranagh,
                (byte)EnemyType.SherryWithClairesJacket,
                (byte)EnemyType.LeonKennedyRpd,
                (byte)EnemyType.ClaireRedfield };

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

        public bool IsNpc(byte type) => type >= (byte)EnemyType.ChiefIrons1 && type != (byte)EnemyType.MayorsDaughter;

        public string? GetActor(byte type)
        {
            switch ((EnemyType)type)
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

        public byte[] GetSlots(RandoConfig config, byte id)
        {
            switch ((EnemyType)id)
            {
                case EnemyType.LeonKennedyRpd:
                    return new byte[] { 0x48, 0x52, 0x54, 0x56, 0x58, 0x5A };
                case EnemyType.ClaireRedfield:
                    return new byte[] { 0x53, 0x55, 0x57, 0x5B };
                case EnemyType.SherryWithPendant:
                case EnemyType.SherryWithClairesJacket:
                    return new byte[] {
                        (byte)EnemyType.SherryWithPendant,
                        (byte)EnemyType.SherryWithClairesJacket
                    };
                case EnemyType.AdaWong1:
                case EnemyType.AdaWong2:
                    return new byte[] {
                        (byte)EnemyType.AdaWong1,
                        (byte)EnemyType.AdaWong2
                    };
                case EnemyType.BenBertolucci1:
                case EnemyType.BenBertolucci2:
                case EnemyType.ChiefIrons1:
                case EnemyType.ChiefIrons2:
                    return new byte[] {
                        (byte)EnemyType.BenBertolucci1,
                        (byte)EnemyType.BenBertolucci1,
                        (byte)EnemyType.ChiefIrons1,
                        (byte)EnemyType.ChiefIrons2
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

                case (byte)EnemyType.SherryWithClairesJacket:
                case (byte)EnemyType.AdaWong2:
                case (byte)EnemyType.BenBertolucci2:
                case (byte)EnemyType.ChiefIrons2:
                    return true;
                default:
                    return false;
            }
        }
    }
}
