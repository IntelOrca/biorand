using System.Collections.Generic;
using System.Text;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    public abstract class ConditionalAstVisitor : ScriptAstVisitor
    {
        private Stack<ScriptCondition> _conditions = new Stack<ScriptCondition>();
        private HashSet<int> _subroutinesDone = new HashSet<int>();
        private ScriptAstNode _scriptNode;

        protected ScriptCondition Condition => _conditions.Peek();

        protected ConditionalAstVisitor(ScriptAstNode scriptNode)
        {
            _scriptNode = scriptNode;
            _conditions.Push(new ScriptCondition());
        }

        private void PushCondition()
        {
            _conditions.Push(_conditions.Peek().Fork());
        }

        private void PopCondition()
        {
            _conditions.Pop();
        }

        protected void VisitScript() => VisitSubroutine(0);

        private void VisitSubroutine(int index)
        {
            if (_subroutinesDone.Add(index))
            {
                if (index >= _scriptNode.Subroutines.Length)
                    return;

                _scriptNode.Subroutines[index].Visit(this);
            }
        }

        public override void VisitIf(IfAstNode node)
        {
            PushCondition();
            PrintIf(node, false);
        }

        public override void VisitElse(IfAstNode node)
        {
            PopCondition();
            PushCondition();
            PrintIf(node, true);
        }

        public override void VisitEndIf(IfAstNode node)
        {
            PopCondition();
        }

        private void PrintIf(IfAstNode node, bool isElse)
        {
            var currentCondition = Condition;
            foreach (var condition in node.Conditions)
            {
                if (condition.Opcode is CkOpcode ckOpcode)
                {
                    var value = isElse ? (ckOpcode.Value == 0 ? 1 : 0) : ckOpcode.Value;
                    if (ckOpcode.BitArray == 1 && ckOpcode.Index == 0)
                    {
                        if (currentCondition.Player == null)
                            currentCondition.Player = value;
                    }
                    else if (ckOpcode.BitArray == 1 && ckOpcode.Index == 1)
                    {
                        if (currentCondition.Scenario == null)
                            currentCondition.Scenario = value;
                    }
                    else if (ckOpcode.BitArray == 1 && ckOpcode.Index == 6)
                    {
                        if (currentCondition.GameMode == null)
                            currentCondition.GameMode = value;
                    }
                    else if (ckOpcode.BitArray == 0 && ckOpcode.Index == 0x19)
                    {
                        if (currentCondition.Difficulty == null)
                            currentCondition.Difficulty = value;
                    }
                }
            }
        }

        public override void VisitOpcode(OpcodeAstNode node)
        {
            if (node.Opcode is GosubOpcode gosubOp)
            {
                VisitSubroutine(gosubOp.Index);
            }
            else if (node.Opcode is EvtExecOpcode execOp)
            {
                VisitSubroutine(execOp.BackgroundOperand);
            }
        }

        public class ScriptCondition
        {
            public int? Player { get; set; }
            public int? Scenario { get; set; }
            public int? Difficulty { get; set; }
            public int? GameMode { get; set; }

            public ScriptCondition Fork()
            {
                return new ScriptCondition()
                {
                    Player = Player,
                    Scenario = Scenario,
                    Difficulty = Difficulty,
                    GameMode = GameMode
                };
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                var c = this;
                var hasConditions = c.Player != null || c.Scenario != null || c.GameMode != null || c.Difficulty != null;
                if (hasConditions)
                    sb.Append("[");

                if (c.Player == 0)
                    sb.Append("leon, ");
                else if (c.Player != null)
                    sb.Append("claire, ");

                if (c.Scenario == 0)
                    sb.Append("a, ");
                else if (c.Scenario != null)
                    sb.Append("b, ");

                if (c.GameMode == 0)
                    sb.Append("normal, ");
                else if (c.GameMode != null)
                    sb.Append("bonus, ");

                if (c.Difficulty == 0)
                    sb.Append("jpn, ");
                else if (c.Difficulty != null)
                    sb.Append("usa, ");

                if (hasConditions)
                {
                    sb.Remove(sb.Length - 2, 2);
                    sb.Append(']');
                }
                return sb.ToString();
            }
        }
    }
}
