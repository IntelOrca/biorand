using System.Text;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    public class AstPrinter : ConditionalAstVisitor
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
            switch (node.Opcode)
            {
                case IDoorAotSetOpcode door:
                    _sb.Append($"{indent}Door #{door.Id}: {new RdtId(door.NextStage, door.NextRoom)} (0x{door.Offset:X2})");
                    AppendConditions();
                    _sb.AppendLine();
                    break;
                case IItemAotSetOpcode item:
                    _sb.Append($"{indent}Item #{item.Id}: {item.Type} x{item.Amount} (0x{item.Offset:X2})");
                    AppendConditions();
                    _sb.AppendLine();
                    break;
                case SceEmSetOpcode enemy:
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
}
