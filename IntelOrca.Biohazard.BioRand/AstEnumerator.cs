using System.Collections.Generic;

namespace IntelOrca.Biohazard.Script
{
    public class AstEnumerator<T> : ConditionalAstVisitor
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
