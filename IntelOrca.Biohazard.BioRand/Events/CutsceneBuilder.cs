using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.BioRand.Events
{
    public class CutsceneBuilder
    {
        internal const byte FG_STATUS = 1;
        internal const byte FG_STOP = 2;
        internal const byte FG_SCENARIO = 3;
        internal const byte FG_COMMON = 4;
        internal const byte FG_ROOM = 5;
        internal const byte FG_ITEM = 8;

        private StringBuilder _sb;
        private StringBuilder _sbMain = new StringBuilder();
        private List<Procedure> _subProcedureList = new List<Procedure>();

        private int _labelCount;
        private Stack<int> _labelStack = new Stack<int>();
        private bool _else;

        public Queue<int> AvailableAotIds { get; } = new Queue<int>();
        public Queue<int> AvailableEnemyIds { get; } = new Queue<int>();
        public HashSet<int> PlacedEnemyIds { get; } = new HashSet<int>();

        public override string ToString() => _sb.ToString();

        public CutsceneBuilder()
        {
            _sb = _sbMain;
        }

        public void DeactivateEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", 16, 7);
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", 16, "0x8000");
            AppendLine("member_set2", 7, 16);
            AppendLine("nop");
        }

        public void ActivateEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", 16, 7);
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", 16, "0x7FFF");
            AppendLine("member_set2", 7, 16);
            AppendLine("nop");
        }

        public void DisableEnemyCollision(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_POINTER");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", "V_TEMP", 0x0002);
            AppendLine("member_set2", "M_POINTER", "V_TEMP");
            AppendLine("nop");
        }

        public void EnableEnemyCollision(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_POINTER");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", "V_TEMP", 0xFFFD);
            AppendLine("member_set2", "M_POINTER", "V_TEMP");
            AppendLine("nop");
        }

        public void HideEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_BE_FLAG");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", "V_TEMP", 0x0008);
            AppendLine("member_set2", "M_BE_FLAG", "V_TEMP");
            AppendLine("nop");
        }

        public void UnhideEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_BE_FLAG");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", "V_TEMP", 0xFFF7);
            AppendLine("member_set2", "M_BE_FLAG", "V_TEMP");
            AppendLine("nop");
        }

        public void WorkOnEnemy(int id)
        {
            if (id == -1)
            {
                AppendLine("work_set", "WK_PLAYER", 0);
            }
            else
            {
                AppendLine("work_set", "WK_ENEMY", id);
            }
            AppendLine("nop");
        }

        public void CheckFlag(ReFlag flag, bool value = true) => CheckFlag(flag.Group, flag.Index, value);
        public void CheckFlag(int group, int index, bool value = true)
        {
            AppendLine("ck", group, index, value ? 1 : 0);
        }

        public void SetFlag(ReFlag flag, bool value = true) => SetFlag(flag.Group, flag.Index, value);
        public void SetFlag(int group, int index, bool value = true)
        {
            AppendLine("set", group, index, value ? 1 : 0);
        }

        public void Goto(int label)
        {
            AppendLine("goto", 255, 255, 0, LabelName(label));
        }

        public void BeginWhileLoop(int conditionLength)
        {
            AppendLine("while", conditionLength, LabelName(CreateLabel()));
        }

        public void EndLoop()
        {
            AppendLine("ewhile", 0);
            AppendLabel();
        }

        public int BeginDoWhileLoop()
        {
            var label = CreateLabel();
            AppendLine("do", 0, LabelName(label));
            return label;
        }

        public void BeginDoWhileConditions(int label)
        {
            AppendLine("edwhile", LabelName(label));
        }

        public void EndDoLoop()
        {
            AppendLabel();
        }

        public void BeginIf()
        {
            var labelIndex = CreateLabel();
            AppendLine("if", 0, LabelName(labelIndex));
        }

        public void Else()
        {
            var index = _labelStack.Pop();
            AppendLine("else", 0, LabelName(CreateLabel()));
            AppendLabel(index);
            _else = true;
        }

        public void EndIf()
        {
            if (_else)
            {
                _else = false;
            }
            else
            {
                AppendLine("endif");
                AppendLine("nop");
            }
            AppendLabel();
        }

        public void Switch(object variable)
        {
            var labelIndex = CreateLabel();
            AppendLine("switch", variable, LabelName(labelIndex));
        }

        public void BeginSwitchCase(int value)
        {
            var labelIndex = CreateLabel();
            AppendLine("case", 0, LabelName(labelIndex), value);
        }

        public void EndSwitchCase()
        {
            AppendLine("break", 0);
            AppendLabel();
        }

        public void EndSwitch()
        {
            AppendLine("eswitch", 0);
            AppendLabel();
        }

        public int CreateLabel()
        {
            _labelStack.Push(_labelCount);
            _labelCount++;
            return _labelCount - 1;
        }

        public void AppendLabel()
        {
            AppendLabel(_labelStack.Pop());
        }

        public void AppendLabel(int index)
        {
            AppendBlankLine();
            _sb.AppendLine($"{LabelName(index)}:");
        }

        public void BeginProcedure(string name)
        {
            AppendBlankLine();
            _sb.AppendLine($".proc {name}");
        }

        public void EndProcedure()
        {
            AppendLine("evt_end", 0);

            foreach (var p in _subProcedureList)
            {
                _sbMain.Append(p.Sb.ToString());
            }
            _subProcedureList.Clear();
        }

        public void Call(string procedureName)
        {
            AppendLine("gosub", procedureName);
        }

        public string LabelName(int index) => $"label_{index}";

        public void AppendBlankLine() => _sb.AppendLine();

        public void AppendLine(string instruction, params object[] parameters)
        {
            _sb.AppendLine(string.Format("    {0,-24}{1}", instruction, string.Join(", ", parameters)));
        }

        private class Procedure
        {
            public string Name { get; }
            public StringBuilder Sb { get; } = new StringBuilder();

            public Procedure(string name)
            {
                Name = name;
            }
        }
    }
}
