using System.Globalization;

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

        public string GetOpcodeSignature(byte opcode)
        {
            if (opcode < _opcodes.Length)
                return _opcodes[opcode];
            return "";
        }

        public string? GetConstant(char kind, int value)
        {
            switch (kind)
            {
                case 'e':
                    return GetEnemyName((byte)value);
                case 't':
                    return GetItemName((byte)value);
                case 'T':
                    if (value == 255)
                        return "LOCKED";
                    else if (value == 254)
                        return "UNLOCK";
                    else if (value == 0)
                        return "UNLOCKED";
                    else
                        return GetItemName((byte)value);
                case 'v':
                    switch (value)
                    {
                        case 0x02:
                            return "NITEM_MESSAGE";
                        case 0x08:
                            return "NITEM_BOX";
                        case 0x10:
                            return "NITEM_TYPEWRITER";
                    }
                    break;
            }
            return null;
        }

        public int? GetConstantValue(string symbol)
        {
            switch (symbol)
            {
                case "LOCKED":
                    return 255;
                case "UNLOCK":
                    return 254;
                case "UNLOCKED":
                    return 0;
            }
            if (symbol.StartsWith("ENEMY_"))
            {
                for (int i = 0; i < 255; i++)
                {
                    if (symbol == GetEnemyName((byte)i))
                    {
                        return i;
                    }
                }
            }
            else if (symbol.StartsWith("ITEM_"))
            {
                for (int i = 0; i < 255; i++)
                {
                    if (symbol == GetItemName((byte)i))
                    {
                        return i;
                    }
                }
            }
            else if (symbol.StartsWith("NITEM_"))
            {
                for (int i = 0; i < 255; i++)
                {
                    if (symbol == GetConstant('v', (byte)i))
                    {
                        return i;
                    }
                }
            }
            else if (symbol.StartsWith("RDT_"))
            {
                var number = symbol.Substring(4);
                if (int.TryParse(number, NumberStyles.HexNumber, null, out var rdt))
                {
                    return rdt;
                }
            }
            return null;
        }

        public int GetInstructionSize(byte opcode)
        {
            return _instructionSizes1[opcode];
        }

        public byte? FindOpcode(string name)
        {
            for (int i = 0; i < _opcodes.Length; i++)
            {
                var signature = _opcodes[i];
                var colonIndex = signature.IndexOf(':');
                if (colonIndex == -1)
                    continue;

                var opcodeName = signature.Substring(0, colonIndex);
                if (name == opcodeName)
                {
                    return (byte)i;
                }
            }
            return null;
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

        private string[] _opcodes = new string[]
        {
            "end:u",
            "if:l",
            "else:l",
            "endif:u",
            "ck:uau",
            "set:uau",
            "cmpb:uuu",
            "cmpw:uuuI",
            "setb:uuu",
            "",
            "",
            "",
            "door:uIIIIuuuuurIIIITu",
            "nitem:uIIIIvuuuuuuu",
            "nop:u",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "item:uIIIItuuuuuuuuuuuuuuu",
            "",
            "",
            "enemy:euuuuuuIuuIIIuuuu",
            "",
            "",
            "",
            "obj:uuuIIIIuuuuuuuuuuuuuuuu",
        };

        private static int[] _instructionSizes1 = new int[]
        {
            2, 2, 2, 2, 4, 4, 4, 6, 4, 2, 2, 4, 26, 18, 2, 8,
            2, 2, 10, 4, 4, 2, 2, 10, 26, 4, 2, 22, 6, 2, 4, 28,
            14, 14, 4, 2, 4, 4, 0, 2, 4 + 0, 2, 12, 4, 2, 4, 0, 4,
            12, 4, 4, 4 + 0, 8, 4, 4, 4, 4, 2, 4, 6, 6, 12, 2, 6,
            16, 4, 4, 4, 2, 2, 44 + 0, 14, 2, 2, 2, 2, 4, 2, 4, 2,
            2
        };
    }
}
