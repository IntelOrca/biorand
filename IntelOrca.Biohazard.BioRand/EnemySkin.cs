using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.BioRand
{
    public readonly struct EnemySkin
    {
        private const string OriginalFileName = "original";

        public static EnemySkin Original => new EnemySkin(OriginalFileName);

        public string FileName { get; }
        public string Name { get; }
        public string[] EnemyNames { get; }
        public byte[] EnemyIds { get; }

        public EnemySkin(string fileName)
        {
            FileName = fileName;
            Name = GetNameFromFileName(fileName);
            EnemyNames = new string[0];
            EnemyIds = new byte[0];
        }

        public EnemySkin(string fileName, string[] enemyNames, byte[] enemyIds)
        {
            FileName = fileName;
            Name = GetNameFromFileName(fileName);
            EnemyNames = enemyNames;
            EnemyIds = enemyIds;
        }

        private static string GetNameFromFileName(string fileName)
        {
            var regex = new Regex("([^.$]*)(?:\\$([^.]+))?(?:\\.(.*))?");
            var match = regex.Match(fileName);
            if (!match.Success)
                return fileName;

            var name = match.Groups[1].Value;
            name = name == "npc" ? "NPC" : name.ToTitle();
            if (match.Groups[2].Success)
                name += $", {match.Groups[2].Value.ToTitle()}";
            if (match.Groups[3].Success)
                name += $" ({match.Groups[3].Value.ToUpper()})";
            return name;
        }

        public string ToolTip
        {
            get
            {
                if (IsOriginal)
                {
                    return "Allows the original enemy skins to occasionally override custom skins.";
                }
                if (IsNPC)
                {
                    return "Replaces zombies with random NPCs.";
                }
                return $"Replaces {string.Join(", ", EnemyNames)} with {Name}.";
            }
        }

        public bool IsOriginal => FileName == OriginalFileName;
        public bool IsNPC => FileName.GetBaseName('$') == "npc";
        public override string ToString() => $"{Name} [{string.Join(", ", EnemyNames)}]";
    }
}
