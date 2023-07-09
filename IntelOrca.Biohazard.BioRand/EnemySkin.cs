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
            var regex = new Regex("([^.]*)(?:\\.(.*))?");
            var match = regex.Match(fileName);
            if (!match.Success)
                return fileName;

            var baseName = match.Groups[1].Value;
            baseName = baseName == "npc" ? "NPC" : baseName.ToTitle();
            if (match.Groups[2].Success)
                return $"{baseName} ({match.Groups[2].Value.ToTitle()})";
            return baseName;
        }

        public string ToolTip
        {
            get
            {
                if (FileName == OriginalFileName)
                {
                    return "Allows the original enemy skins to occasionally override custom skins.";
                }
                if (FileName.GetBaseName() == "npc")
                {
                    return "Replaces zombies with random NPCs.";
                }
                return $"Replaces {string.Join(", ", EnemyNames)} with {Name}.";
            }
        }

        public override string ToString() => $"{Name} [{string.Join(", ", EnemyNames)}]";
    }
}
