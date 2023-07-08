namespace IntelOrca.Biohazard.BioRand
{
    public readonly struct EnemySkin
    {
        public string FileName { get; }
        public string Name { get; }
        public string EnemyName { get; }
        public byte[] EnemyIds { get; }

        public EnemySkin(string fileName, string enemyName, byte[] enemyIds)
        {
            FileName = fileName;
            Name = GetNameFromFileName(fileName);
            EnemyName = enemyName;
            EnemyIds = enemyIds;
        }

        private static string GetNameFromFileName(string fileName)
        {
            var name = fileName.ToLower();
            var d = name.IndexOf('$');
            if (d != -1)
                name = name.Substring(0, d);

            if (name == "npc")
                return "NPC";
            else
                return name.ToActorString();
        }

        public override string ToString() => $"{Name} [{EnemyName}]";
    }
}
