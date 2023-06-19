using System.Collections.Generic;

namespace IntelOrca.Biohazard.Script
{
    public class SwitchAstNode : IScriptAstNode
    {
        public OpcodeAstNode? Switch { get; set; }
        public List<CaseAstNode> Cases { get; } = new List<CaseAstNode>();
        public OpcodeAstNode? Eswitch { get; set; }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
            Switch?.Visit(visitor);
            foreach (var c in Cases)
            {
                c.Visit(visitor);
            }
            Eswitch?.Visit(visitor);
        }
    }
}
