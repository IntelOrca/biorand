using System.IO;
using System.Text;

namespace IntelOrca.Biohazard.Script
{
    internal class Bio2ConstantTable : IConstantTable
    {
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
                case 'C':
                    if (value < g_comparators.Length)
                        return g_comparators[value];
                    break;
                case 'O':
                    if (value < g_operators.Length)
                        return g_operators[value];
                    break;
                case 'S':
                    if (value < g_sceNames.Length)
                        return g_sceNames[value];
                    break;
                case 'A':
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
                case 'W':
                    if (value < g_workKinds.Length)
                        return g_workKinds[value];
                    break;
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
            }

            if (symbol.StartsWith("ENEMY_"))
                return FindConstantValue(symbol, 'e');
            else if (symbol.StartsWith("ITEM_"))
                return FindConstantValue(symbol, 't');
            else if (symbol.StartsWith("CMP_"))
                return FindConstantValue(symbol, 'C');
            else if (symbol.StartsWith("OP_"))
                return FindConstantValue(symbol, 'O');
            else if (symbol.StartsWith("SCE_"))
                return FindConstantValue(symbol, 'S');
            else if (symbol.StartsWith("SAT_"))
                return FindConstantValue(symbol, 'A');
            else if (symbol.StartsWith("WK_"))
                return FindConstantValue(symbol, 'W');

            return null;
        }

        public string GetEnemyName(byte kind)
        {
            return $"ENEMY_{((EnemyType)kind).ToString().ToUpperInvariant()}";
        }

        public string GetItemName(byte kind)
        {
            return $"ITEM_{((ItemType)kind).ToString().ToUpperInvariant()}";
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

        private static int[] g_instructionSizes = new int[]
        {
            1, 2, 1, 4, 4, 2, 4, 4, 1, 4, 3, 1, 1, 6, 2, 4,
            2, 4, 2, 4, 6, 2, 2, 6, 2, 2, 2, 6, 1, 4, 1, 1,
            1, 4, 4, 6, 4, 3, 6, 4, 1, 2, 1, 6, 20, 38, 3, 4,
            1, 1, 8, 8, 4, 3, 12, 4, 3, 8, 16, 32, 2, 3, 6, 4,
            8, 10, 1, 4, 22, 5, 10, 2, 16, 8, 2, 3, 5, 22, 22, 4,
            4, 6, 6, 6, 22, 6, 4, 8, 4, 4, 2, 2, 3, 2, 2, 2,
            14, 4, 2, 1, 16, 2, 1, 28, 40, 30, 6, 4, 1, 4, 6, 2,
            1, 1, 16, 8, 4, 22, 3, 4, 6, 1, 16, 16, 6, 6, 6, 6,
            2, 3, 3, 1, 2, 6, 1, 1, 3, 1, 6, 6, 8, 24, 24
        };

        private static string[] g_instructionSignatures = new string[]
        {
            "nop",
            "evt_end",
            "evt_next",
            "evt_chain",
            "evt_exec",
            "evt_kill",
            "if:uL",
            "else:uL",
            "endif",
            "sleep:uU",
            "sleeping:U",
            "wsleep",
            "wsleeping",
            "for:uLU",
            "next",
            "while:uL",

            "ewhile",
            "do:uL",
            "edwhile:l",
            "switch:uL",
            "case:uLU",
            "default",
            "eswitch",
            "goto:uuu~",
            "gosub",
            "return",
            "break",
            "for2",
            "break_point",
            "work_copy",
            "nop_1E",
            "nop_1F",

            "nop_20",
            "ck",
            "set",
            "cmp:uuCI",
            "save:uI",
            "copy",
            "calc:uOuI",
            "calc2:Ouu",
            "sce_rnd",
            "cut_chg",
            "cut_old",
            "message_on",
            "aot_set:uSAuuIIIIUUU",
            "obj_model_set",
            "work_set",
            "speed_set:uI",

            "add_speed",
            "add_aspeed",
            "pos_set",
            "dir_set",
            "member_set",
            "member_set2",
            "se_on",
            "sca_id_set",
            "flr_set",
            "dir_ck",
            "sce_espr_on",
            "door_aot_se:uSAuuIIIIIIIIuuuuuuuuTu",
            "cut_auto",
            "member_copy",
            "member_cmp",
            "plc_motion",

            "plc_dest",
            "plc_neck",
            "plc_ret",
            "plc_flg",
            "sce_em_set:uueuuuuuuIIIIUU",
            "col_chg_set",
            "aot_reset",
            "aot_on",
            "super_set",
            "super_reset",
            "plc_gun",
            "cut_replace",
            "sce_espr_kill",
            "",
            "item_aot_set",
            "sce_key_ck",

            "sce_trg_ck",
            "sce_bgm_control",
            "sce_espr_control",
            "sce_fade_set",
            "sce_espr3d_on",
            "member_calc",
            "member_calc2",
            "sce_bgmtbl_set",
            "plc_rot",
            "xa_on",
            "weapon_chg",
            "plc_cnt",
            "sce_shake_on",
            "mizu_div_set",
            "keep_item_ck",
            "xa_vol",

            "kage_set",
            "cut_be_set",
            "sce_item_lost",
            "plc_gun_eff",
            "sce_espr_on2",
            "sce_espr_kill2",
            "plc_stop",
            "aot_set_4p",
            "door_aot_set_4p:uSAuuIIIIIIIIIIIIuuuuuuuuTu",
            "item_aot_set_4p",
            "light_pos_set",
            "light_kido_set",
            "rbj_reset",
            "sce_scr_move",
            "parts_set",
            "movie_on",

            "splc_ret",
            "splc_sce",
            "super_on",
            "mirror_set",
            "sce_fade_adjust",
            "sce_espr3d_on2",
            "sce_item_get",
            "sce_line_start",
            "sce_line_main",
            "sce_line_end",
            "sce_parts_bomb",
            "sce_parts_down",
            "light_color_set",
            "light_pos_set2",
            "light_kido_set2",
            "light_color_set2",

            "se_vol",
            "",
            "",
            "",
            "",
            "",
            "poison_ck",
            "poison_clr",
            "sce_item_ck_lost",
            "",
            "nop_8a",
            "nop_8b",
            "nop_8c",
            "",
            "",
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
