using System.Collections.Generic;
using System.Text;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    internal abstract class ConditionalAstVisitor : ScriptAstVisitor
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
            if (_scriptNode.Version == BioVersion.Biohazard1)
                return;

            if ((OpcodeV2)node.Opcode.Opcode == OpcodeV2.Gosub)
            {
                VisitSubroutine(((GosubOpcode)node.Opcode).Index);
            }
        }

        internal class ScriptCondition
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

    internal class AstPrinter : ConditionalAstVisitor
    {
        private readonly StringBuilder _sb;

        private AstPrinter(StringBuilder sb, ScriptAstNode scriptNode)
            : base(scriptNode)
        {
            _sb = sb;
        }

        public static void Print(StringBuilder sb, ScriptAst ast)
        {
            if (ast.Init is ScriptAstNode InitNode)
            {
                Print(sb, InitNode);
            }
            if (ast.Main is ScriptAstNode mainNode)
            {
                Print(sb, mainNode);
            }
        }

        private static void Print(StringBuilder sb, ScriptAstNode scriptNode)
        {
            if (scriptNode.Subroutines.Length != 0)
            {
                var instance = new AstPrinter(sb, scriptNode);
                instance.VisitScript();
            }
        }

        public override void VisitOpcode(OpcodeAstNode node)
        {
            base.VisitOpcode(node);
            var indent = new string(' ', 4);
            switch ((OpcodeV2)node.Opcode.Opcode)
            {
                case OpcodeV2.DoorAotSe:
                case OpcodeV2.DoorAotSet4p:
                    {
                        var door = (IDoorAotSetOpcode)node.Opcode;
                        _sb.Append($"{indent}Door #{door.Id}: {new RdtId(door.NextStage, door.NextRoom)} (0x{door.Offset:X2})");
                        AppendConditions();
                        _sb.AppendLine();
                        break;
                    }
                case OpcodeV2.ItemAotSet:
                case OpcodeV2.ItemAotSet4p:
                    var item = (ItemAotSetOpcode)node.Opcode;
                    _sb.Append($"{indent}Item #{item.Id}: {(ItemType)item.Type} x{item.Amount} (0x{item.Offset:X2})");
                    AppendConditions();
                    _sb.AppendLine();
                    break;
                case OpcodeV2.SceEmSet:
                    var enemy = (SceEmSetOpcode)node.Opcode;
                    _sb.Append($"{indent}Enemy #{enemy.Id}: {enemy.Type} (0x{enemy.Offset:X2})");
                    AppendConditions();
                    _sb.AppendLine();
                    break;
            }
        }

        private void AppendConditions()
        {
            var c = Condition.ToString();
            if (!string.IsNullOrEmpty(c))
            {
                _sb.Append(' ');
                _sb.Append(c);
            }
        }
    }

    internal class AstEnumerator<T> : ConditionalAstVisitor
    {
        private readonly RandoConfig _config;
        private List<T> _opcodes = new List<T>();

        public AstEnumerator(RandoConfig config, ScriptAstNode scriptNode)
            : base(scriptNode)
        {
            _config = config;
        }

        public static IEnumerable<T> Enumerate(ScriptAst ast, RandoConfig config)
        {
            if (ast.Init is ScriptAstNode initNode)
            {
                foreach (var n in Enumerate(initNode, config))
                    yield return n;
            }
            if (ast.Main is ScriptAstNode mainNode)
            {
                foreach (var n in Enumerate(mainNode, config))
                    yield return n;
            }
        }

        private static IEnumerable<T> Enumerate(ScriptAstNode scriptNode, RandoConfig config)
        {
            if (scriptNode.Subroutines.Length != 0)
            {
                var instance = new AstEnumerator<T>(config, scriptNode);
                instance.VisitScript();
                foreach (var opcode in instance._opcodes)
                    yield return opcode;
            }
        }

        public override void VisitOpcode(OpcodeAstNode node)
        {
            base.VisitOpcode(node);

            if (Condition.GameMode != null && Condition.GameMode != 0)
                return;
            if (Condition.Difficulty != null && Condition.Difficulty != 0)
                return;
            if (Condition.Player != null && Condition.Player != _config.Player)
                return;
            if (Condition.Scenario != null && Condition.Scenario != _config.Scenario)
                return;

            if (node.Opcode is T opcode)
            {
                _opcodes.Add(opcode);
            }
        }
    }
}
