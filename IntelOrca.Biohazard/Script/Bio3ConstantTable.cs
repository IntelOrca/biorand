using System.Collections;
using System.IO;
using System.Text;

namespace IntelOrca.Biohazard.Script
{
    internal class Bio3ConstantTable : IConstantTable
    {
        private const byte SCE_EVENT = 5;

        public byte? FindOpcode(string name)
        {
            for (int i = 0; i < g_instructionSignatures.Length; i++)
            {
                var signature = g_instructionSignatures[i];
                if (signature == "")
                    continue;

                var opcodeName = signature;
                var colonIndex = signature.IndexOf(':');
                if (colonIndex != -1)
                    opcodeName = signature.Substring(0, colonIndex);

                if (name == opcodeName)
                    return (byte)i;
            }
            return null;
        }

        public string? GetConstant(byte opcode, int pIndex, BinaryReader reader)
        {
            using (var br = reader.Fork())
            {
                if (opcode == (byte)OpcodeV2.AotReset)
                {
                    if (pIndex == 5)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 3;
                            return GetConstant('g', br.ReadByte());
                        }
                    }
                    else if (pIndex == 6)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 3;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                }
                else if (opcode == (byte)OpcodeV2.AotSet)
                {
                    if (pIndex == 11)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 13;
                            return GetConstant('g', br.ReadByte());
                        }
                    }
                    else if (pIndex == 12)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 13;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                }
                else if (opcode == (byte)OpcodeV2.AotSet4p)
                {
                    if (pIndex == 15)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 21;
                            return GetConstant('g', br.ReadByte());
                        }
                    }
                    else if (pIndex == 16)
                    {
                        br.BaseStream.Position++;
                        var sce = br.ReadByte();
                        if (sce == SCE_EVENT)
                        {
                            br.BaseStream.Position += 21;
                            if (br.ReadByte() == (byte)OpcodeV2.Gosub)
                            {
                                return GetConstant('p', br.ReadByte());
                            }
                        }
                    }
                }
                return null;
            }
        }

        public string? GetConstant(char kind, int value)
        {
            switch (kind)
            {
                case 'e':
                    return GetEnemyName((byte)value);
                case 't':
                case 'T':
                    if (value == 255)
                        return "LOCKED";
                    else if (value == 254)
                        return "UNLOCK";
                    else if (value == 0)
                        return "UNLOCKED";
                    else
                        return GetItemName((byte)value);
                case 'c':
                    if (value < g_comparators.Length)
                        return g_comparators[value];
                    break;
                case 'o':
                    if (value < g_operators.Length)
                        return g_operators[value];
                    break;
                case 's':
                    if (value < g_sceNames.Length)
                        return g_sceNames[value];
                    break;
                case 'a':
                    if (value == 0)
                        return "SAT_AUTO";
                    var sb = new StringBuilder();
                    for (int i = 0; i < 8; i++)
                    {
                        var mask = 1 << i;
                        if (value == mask)
                        {
                            return g_satNames[i];
                        }
                        else if ((value & (1 << i)) != 0)
                        {
                            sb.Append(g_satNames[i]);
                            sb.Append(" | ");
                        }
                    }
                    sb.Remove(sb.Length - 3, 3);
                    return sb.ToString();
                case 'w':
                    if (value < g_workKinds.Length)
                        return g_workKinds[value];
                    break;
                case 'g':
                    if (value == (byte)OpcodeV2.Gosub)
                        return "I_GOSUB";
                    break;
                case 'p':
                    return $"main_{value:X2}";
            }
            return null;
        }

        private int? FindConstantValue(string symbol, char kind)
        {
            for (int i = 0; i < 256; i++)
            {
                var name = GetConstant(kind, i);
                if (name == symbol)
                    return i;
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
                case "I_GOSUB":
                    return (byte)OpcodeV2.Gosub;
            }

            if (symbol.StartsWith("ENEMY_"))
                return FindConstantValue(symbol, 'e');
            else if (symbol.StartsWith("ITEM_"))
                return FindConstantValue(symbol, 't');
            else if (symbol.StartsWith("CMP_"))
                return FindConstantValue(symbol, 'c');
            else if (symbol.StartsWith("OP_"))
                return FindConstantValue(symbol, 'o');
            else if (symbol.StartsWith("SCE_"))
                return FindConstantValue(symbol, 's');
            else if (symbol.StartsWith("SAT_"))
                return FindConstantValue(symbol, 'a');
            else if (symbol.StartsWith("WK_"))
                return FindConstantValue(symbol, 'w');

            return null;
        }

        public string GetEnemyName(byte kind)
        {
            if (kind >= g_enemyNames.Length || string.IsNullOrEmpty(g_enemyNames[kind]))
                return $"ENEMY_{kind:X2}";
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

        public int GetInstructionSize(byte opcode, BinaryReader? br)
        {
            if (opcode < g_instructionSizes.Length)
                return g_instructionSizes[opcode];
            return 0;
        }

        public string GetOpcodeSignature(byte opcode)
        {
            if (opcode < g_instructionSignatures.Length)
                return g_instructionSignatures[opcode];
            return "";
        }

        public bool IsOpcodeCondition(byte opcode)
        {
            switch ((OpcodeV3)opcode)
            {
                case OpcodeV3.Ck:
                case OpcodeV3.Cmp:
                case OpcodeV3.KeepItemCk:
                case OpcodeV3.KeyCk:
                case OpcodeV3.TrgCk:
                    return true;
            }
            return false;
        }

        public string? GetNamedFlag(int obj, int index)
        {
            if (obj == 0 && index == 23)
                return "game.easy";

            // Carried over from RE 2 (may be incorrect)
            if (obj == 0 && index == 0x19)
                return "game.difficult";
            if (obj == 1 && index == 0)
                return "game.player";
            if (obj == 1 && index == 1)
                return "game.scenario";
            if (obj == 1 && index == 6)
                return "game.bonus";
            if (obj == 1 && index == 0x1B)
                return "game.cutscene";
            if (obj == 0xB && index == 0x1F)
                return "input.question";
            return null;
        }

        private string[] g_enemyNames = new string[]
        {
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",

            "Zombie (Guy 1)",
            "Zombie (Girl 1)",
            "Zombie (Fat)",
            "Zombie (Girl 2)",
            "Zombie (RPD 1)",
            "Zombie (Guy 2)",
            "Zombie (Guy 3)",
            "Zombie (Guy 4)",
            "Zombie (Naked)",
            "Zombie (Guy 5)",
            "Zombie (Guy 6)",
            "Zombie (Lab)",
            "Zombie (Girl 3)",
            "Zombie (RPD 2)",
            "Zombie (Guy 7)",
            "Zombie (Guy 8)",

            "Cerberus",
            "Crow",
            "Hunter",
            "BS23",
            "HunterGamma",
            "Spider",
            "MiniSpider",
            "MiniBrainsucker",
            "BS28",
            "",
            "",
            "",
            "",
            "Arm",
            "",
            "",

            "",
            "",
            "MiniWorm",
            "",
            "Nemesis",
            "",
            "Nemesis 3",

            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "Nikolai Zinoviev",
            "Brad Vickers",

            "Dario Rosso",
            "Murphy Seeker",
            "",
            "",
            "Brad (Zombie)",
            "Dario (Zombie)",
            "Promo Girl",
            "",
            "Carlos Oliveira"
        };

        private string[] g_itemNames = new string[]
        {
            "",
            "Combat Knife",
            "Handgun Sigpro",
            "Handgun Beretta",
            "Shotgun Benelli",
            "Magnum SW",
            "Grenade Launcher (Burst)",
            "Grenade Launcher (Flame)",
            "Grenade Launcher (Acid)",
            "Grenade Launcher (Freeze)",
            "Rocket Launcher",
            "Gatling Gun",
            "Mine Thrower",
            "Hangun Eagle",
            "Rifle M4A1 (Manual)",
            "Rifle M4A1 (Auto)",
            "Shotgun M37",
            "Handgun Sigpro (Enhanced)",
            "Handgun Beretta (Enhanced)",
            "Shotgun Benelli (Enhanced)",
            "Mine Thrower (Enhanced)",
            "Handgun Ammo",
            "Magnum Ammo",
            "Shotgun Ammo",
            "Grenade Rounds",
            "Flame Rounds",
            "Acid Rounds",
            "Freeze Rounds",
            "Mine Thrower Ammo",
            "Rifle Ammo",
            "Handgun Enhanced Ammo",
            "Shotgun Enhanced Ammo",
            "First Aid Spray",
            "Herb (G)",
            "Herb (B)",
            "Herb (R)",
            "Herb (GG)",
            "Herb (GB)",
            "Herb (GR)",
            "Herb (GGG)",
            "Herb (GGB)",
            "Herb (GRB)",
            "First Aid Spray Box",
            "Crank",
            "Medal (Red)",
            "Medal (Blue)",
            "Medal (Gold)",
            "STARS Card (Jill)",
            "Oil Can",
            "Battery",
            "Fire Hook",
            "Power Cable",
            "Fuse",
            "Broken Fire Hose",
            "Oil Additive",
            "Card case (Brad)",
            "STARS Card (Brad)",
            "Machine Oil",
            "Mixed Oil",
            "Unknown Steel Chain",
            "Wrench",
            "Iron Pipe",
            "Unknown Cylinder",
            "Fire Hose",
            "Tape Recorder",
            "Lighter (Oil)",
            "Lighter (No Oil)",
            "Lighter",
            "Gem (Green)",
            "Gem (Blue)",
            "Ball (Amber)",
            "Ball (Obsidian)",
            "Ball (Crystal)",
            "Remote Control (No Batteries)",
            "Remote Control (Batteries)",
            "AA Batteries",
            "Gear (Gold)",
            "Gear (Silver)",
            "Gear (Chronos)",
            "Bronze Book",
            "Bronze Compass",
            "Vaccine Medium",
            "Vaccine Base",
            "",
            "",
            "Vaccine",
            "",
            "",
            "Medium Base",
            "EAGLE Parts (A)",
            "EAGLE Parts (B)",
            "M37 Parts (A)",
            "M37 Parts (B)",
            "",
            "Chronos Chain",
            "Rusted Crank",
            "Card Key",
            "Gunpowder (A)",
            "Gunpowder (B)",
            "Gunpowder (C)",
            "Gunpowder (AA)",
            "Gunpowder (BB)",
            "Gunpowder (AC)",
            "Gunpowder (BC)",
            "Gunpowder (CC)",
            "Gunpowder (AAA)",
            "Gunpowder (AAB)",
            "Gunpowder (BBA)",
            "Gunpowder (BBB)",
            "Gunpowder (CCC)",
            "Infinite Bullets",
            "Water Sample",
            "System Disk",
            "Dummy Key",
            "Lockpick",
            "Warehouse Key",
            "Sickroom Key",
            "Emblem Key",
            "Keyring With 4 Unknown Keys",
            "Clock Tower Key (Bezel)",
            "Clock Tower Key (Winder)",
            "Chronos Key",
            "",
            "Park Key (Front)",
            "Park Key (Graveyard)",
            "Park Key (Rear)",
            "Facility Key (No barcode)",
            "Facility Key (Barcode)",
            "Boutique Key",
            "Ink Ribbon",
            "Reloading Tool",
            "Game Instructions (A)",
            "Game Instructions (B)",
            "Game Instructions (A2)",
        };

        private static int[] g_instructionSizes = new int[]
        {
            1, 2, 1, 2, 4, 2, 4, 4, 2, 1, 3, 1, 1, 6, 4, 2,
            4, 2, 4, 2, 4, 6, 2, 2, 6, 2, 4, 2, 1, 4, 4, 4,
            6, 4, 4, 1, 2, 4, 6, 1, 1, 8, 6, 2, 4, 4, 6, 2,
            6, 6, 6, 4, 10, 6, 3, 2, 2, 16, 16, 3, 1, 2, 2, 3,
            4, 3, 3, 6, 6, 4, 11, 3, 4, 1, 1, 1, 4, 4, 6, 1,
            2, 1, 2, 3, 4, 8, 8, 6, 6, 8, 2, 6, 2, 1, 3, 2,
            22, 32, 40, 20, 28, 10, 2, 22, 30, 14, 16, 2, 4, 4, 4, 2,
            16, 18, 22, 24, 5, 2, 3, 12, 6, 4, 2, 6, 1, 24, 2, 40,
            4, 8, 10, 1, 4, 2, 1, 1, 4, 2, 1, 1, 1, 1, 4, 2
        };

        private static string[] g_instructionSignatures = new string[]
        {
            "nop",
            "evt_end",
            "sleep_1",
            "evt_chain",
            "evt_exec:ugp",
            "evt_kill",
            "if:uL",
            "else:u@",
            "endif",
            "sleep",
            "sleeping:U",
            "wsleep",
            "wsleeping",
            "for:uLU",
            "",
            "next",

            "while:uL",
            "ewhile",
            "do:uL",
            "edwhile:'",
            "switch:uL",
            "case:uuuuu",
            "default",
            "eswitch",
            "goto:uuu~",
            "gosub",
            "return",
            "break",
            "break_point",
            "",
            "set_1e",
            "set_1f",

            "calc_op",
            "",
            "evt_cut",
            "",
            "chaser_evt_clr",
            "map_open",
            "point_add",
            "door_ck",
            "diedemo_on",
            "dir_ck",
            "parts_set",
            "vloop_set",
            "ota_be_set",
            "line_begin",
            "line_main",
            "line_end",

            "light_pos_set",
            "light_kido_set",
            "light_color_set",
            "ahead_room_set",
            "espr_ctr",
            "eval_bgm_tbl_ck",
            "item_get_ck",
            "om_rev",
            "chaser_life_init",
            "parts_bomb",
            "parts_down",
            "chaser_item_set",
            "weapon_chg_old",
            "sel_evt_on",
            "item_lost",
            "floor_set",

            "memb_set",
            "memb_set2",
            "memb_cpy",
            "memb_cmp",
            "memb_calc",
            "memb_calc2",
            "fade_set",
            "work_set",
            "spd_set",
            "add_spd",
            "add_aspd",
            "add_vspd",
            "ck:uuu",
            "set:uuu",
            "cmp:uucI",
            "rnd",

            "cut_chg",
            "cut_old",
            "cut_auto",
            "cut_replace",
            "cut_be_set",
            "pos_set",
            "dir_set",
            "set_vib0",
            "set_vib1",
            "set_vib_fade",
            "rbj_set",
            "message_on",
            "rain_set",
            "message_off",
            "shake_on",
            "weapon_chg",

            "",
            "door_aot_se:usauuIIIIIIIIuuuuuuuutu",
            "door_aot_set_4p:usauuIIIIIIIIIIIIuuuuuuuutu",
            "aot_set:usauuIIIIuuuuuu",
            "aot_set_4p:usauuIIIIIIIIuuuuuu",
            "aot_reset:usauuuuuu",
            "aot_on",
            "item_aot_set:usauuIIUUTUUuu",
            "item_aot_set_4p:usauuIIIIIIIITUUuu",
            "kage_set",
            "super_set",
            "keep_item_ck",
            "key_ck",
            "trg_ck",
            "sca_id_set",
            "om_bomb",

            "espr_on",
            "espr_on2",
            "espr3d_on",
            "espr3d_on2",
            "espr_kill",
            "espr_kill2",
            "espr_kill_all",
            "se_on",
            "bgm_ctl",
            "xa_on",
            "movie_on",
            "bgm_tbl_set",
            "status_on",
            "em_set:uueuuuuuuuuIIIIUU",
            "mizu_div",
            "om_set",

            "plc_motion",
            "plc_dest",
            "plc_neck",
            "plc_ret",
            "plc_flg",
            "plc_gun",
            "plc_gun_eff",
            "plc_stop",
            "plc_rot",
            "plc_cnt",
            "splc_ret",
            "splc_sce",
            "plc_sce",
            "spl_weapon_chg",
            "plc_mot_num",
            "em_reset"
        };

        private static readonly string[] g_comparators = new string[]
        {
            "CMP_EQ",
            "CMP_GT",
            "CMP_GE",
            "CMP_LT",
            "CMP_LE",
            "CMP_NE"
        };

        private static readonly string[] g_operators = new string[]
        {
            "OP_ADD",
            "OP_SUB",
            "OP_MUL",
            "OP_DIV",
            "OP_MOD",
            "OP_OR",
            "OP_AND",
            "OP_XOR",
            "OP_NOT",
            "OP_LSL",
            "OP_LSR",
            "OP_ASR"
        };

        private static readonly string[] g_satNames = new string[]
        {
            "SAT_PL",
            "SAT_EM",
            "SAT_SPL",
            "SAT_OB",
            "SAT_MANUAL",
            "SAT_FRONT",
            "SAT_UNDER",
            "0x80"
        };

        private static readonly string[] g_sceNames = new string[] {
            "SCE_AUTO",
            "SCE_DOOR",
            "SCE_ITEM",
            "SCE_NORMAL",
            "SCE_MESSAGE",
            "SCE_EVENT",
            "SCE_FLAG_CHG",
            "SCE_WATER",
            "SCE_MOVE",
            "SCE_SAVE",
            "SCE_ITEMBOX",
            "SCE_DAMAGE",
            "SCE_STATUS",
            "SCE_HIKIDASHI",
            "SCE_WINDOWS"
        };

        private static readonly string[] g_workKinds = new string[]
        {
            "WK_NONE",
            "WK_PLAYER",
            "WK_SPLAYER",
            "WK_ENEMY",
            "WK_OBJECT",
            "WK_DOOR",
            "WK_ALL"
        };
    }
}
