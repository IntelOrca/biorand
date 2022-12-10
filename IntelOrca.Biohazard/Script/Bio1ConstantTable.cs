namespace IntelOrca.Biohazard.Script
{
    internal class Bio1ConstantTable : IConstantTable
    {
        public string GetEnemyName(byte kind)
        {
            if (kind >= g_enemyNames.Length)
                return "ENEMY_UNKNOWN";
            return $"ENEMY_" + g_enemyNames[kind]
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "")
                .ToUpperInvariant();
        }

        public string GetItemName(byte kind)
        {
            if (kind >= g_itemNames.Length)
                return "ITEM_UNKNOWN";
            return $"ITEM_" + g_itemNames[kind]
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "")
                .ToUpperInvariant();
        }

        private string[] g_enemyNames = new string[]
        {
            "Zombie (Groundskeeper)",
            "Zombie (Naked)",
            "Cerberus",
            "Spider (Brown)",
            "Spider (Black)",
            "Crow",
            "Hunter",
            "Bee",
            "Plant 42",
            "Chimera",
            "Snake",
            "Neptune",
            "Tyrant 1",
            "Yawn 1",
            "Plant42 (roots)",
            "Fountain Plant",
            "Tyrant 2",
            "Zombie (Researcher)",
            "Yawn 2",
            "Cobweb",
            "Computer Hands (left)",
            "Computer Hands (right)",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Chris (Stars)",
            "Jill (Stars)",
            "Barry (Stars)",
            "Rebecca (Stars)",
            "Wesker (Stars)",
            "Kenneth 1",
            "Forrest",
            "Richard",
            "Enrico",
            "Kenneth 2",
            "Barry 2",
            "Barry 2 (Stars)",
            "Rebecca 2 (Stars)",
            "Barry 3",
            "Wesker 2 (Stars)",
            "Chris (Jacket)",
            "Jill (Black Shirt)",
            "Chris 2 (Jacket)",
            "Jill (Red Shirt)",
        };

        private string[] g_itemNames = new string[]
        {
            "Nothing",
            "Combat Knife",
            "Beretta",
            "Shotgun",
            "DumDum Colt",
            "Colt Python",
            "FlameThrower",
            "Bazooka Acid",
            "Bazooka Explosive",
            "Bazooka Flame",
            "Rocket Launcher",
            "Clip",
            "Shells",
            "DumDum Rounds",
            "Magnum Rounds",
            "FlameThrower Fuel",
            "Explosive Rounds",
            "Acid Rounds",
            "Flame Rounds",
            "Empty Bottle",
            "Water",
            "Umb No. 2",
            "Umb No. 4",
            "Umb No. 7",
            "Umb No. 13",
            "Yellow 6",
            "NP-003",
            "V-Jolt",
            "Broken Shotgun",
            "Square Crank",
            "Hex Crank",
            "Wood Emblem",
            "Gold Emblem",
            "Blue Jewel",
            "Red Jewel",
            "Music Notes",
            "Wolf Medal",
            "Eagle Medal",
            "Chemical",
            "Battery",
            "MO Disk",
            "Wind Crest",
            "Flare",
            "Slides",
            "Moon Crest",
            "Star Crest",
            "Sun Crest",
            "Ink Ribbon",
            "Lighter",
            "Lock Pick",
            "Nameless (Can of Oil)",
            "Sword Key",
            "Armor Key",
            "Sheild Key",
            "Helmet Key",
            "Lab Key (1)",
            "Special Key",
            "Dorm Key (002)",
            "Dorm Key (003)",
            "C. Room Key",
            "Lab Key (2)",
            "Small Key",
            "Red Book",
            "Doom Book (2)",
            "Doom Book (1)",
            "F-Aid Spray",
            "Serum",
            "Red Herb",
            "Green Herb",
            "Blue Herb",
            "Mixed (Red+Green)",
            "Mixed (2 Green)",
            "Mixed (Blue + Green)",
            "Mixed (All)",
            "Mixed (Silver Color)",
            "Mixed (Bright Blue-Green)"
        };
    }
}
