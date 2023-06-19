namespace IntelOrca.Biohazard.Script
{
    public class ScriptAstVisitor
    {
        public virtual void VisitNode(IScriptAstNode node)
        {
            if (node is ScriptAstNode scriptNode)
                VisitScript(scriptNode);
            else if (node is SubroutineAstNode subroutineNode)
                VisitSubroutine(subroutineNode);
            else if (node is IfAstNode ifNode)
                VisitIf(ifNode);
            else if (node is OpcodeAstNode opcodeNode)
                VisitOpcode(opcodeNode);
        }

        public virtual void VisitScript(ScriptAstNode node)
        {
        }

        public virtual void VisitSubroutine(SubroutineAstNode node)
        {
        }

        public virtual void VisitIf(IfAstNode node)
        {
        }

        public virtual void VisitElse(IfAstNode node)
        {
        }

        public virtual void VisitEndIf(IfAstNode node)
        {
        }

        public virtual void VisitOpcode(OpcodeAstNode node)
        {
        }
    }
}
