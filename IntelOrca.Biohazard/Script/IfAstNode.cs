using System.Collections.Generic;

namespace IntelOrca.Biohazard.Script
{
    public class IfAstNode : IScriptAstNode
    {
        public OpcodeAstNode? If { get; set; }
        public BasicBlockAstNode? IfBlock { get; set; }
        public OpcodeAstNode? Else { get; set; }
        public BasicBlockAstNode? ElseBlock { get; set; }
        public OpcodeAstNode? EndIf { get; set; }
        public List<OpcodeAstNode> Conditions { get; set; } = new List<OpcodeAstNode>();

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitIf(this);
            If?.Visit(visitor);
            foreach (var condition in Conditions)
            {
                condition.Visit(visitor);
            }
            IfBlock?.Visit(visitor);

            if (Else != null || ElseBlock != null)
            {
                visitor.VisitElse(this);
            }
            Else?.Visit(visitor);
            ElseBlock?.Visit(visitor);

            visitor.VisitEndIf(this);
            EndIf?.Visit(visitor);
        }
    }
}
