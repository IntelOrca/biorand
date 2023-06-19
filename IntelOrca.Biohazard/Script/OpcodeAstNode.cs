using System.Diagnostics;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    [DebuggerDisplay("{Opcode}")]
    public class OpcodeAstNode : IScriptAstNode
    {
        public OpcodeBase Opcode { get; set; }

        public OpcodeAstNode(OpcodeBase opcode)
        {
            Opcode = opcode;
        }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
        }
    }
}
