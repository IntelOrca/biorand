using System.Linq;

namespace IntelOrca.Biohazard.RE2
{
    internal class Re2NpcHelper : INpcHelper
    {
        public string GetNpcName(byte type)
        {
            return ((EnemyType)type).ToString();
        }

        public string GetPlayerActor(int player)
        {
            return player == 0 ? "leon" : "claire";
        }

        public byte[] GetDefaultIncludeTypes(Rdt rdt)
        {
            var defaultIncludeTypes = new byte[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 79, 80, 81, 84, 85, 88, 89, 90 };

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
    }
}
