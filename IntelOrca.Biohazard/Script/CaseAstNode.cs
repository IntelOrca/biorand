namespace IntelOrca.Biohazard.Script
{
    public class CaseAstNode : IScriptAstNode
    {
        public OpcodeAstNode? Case { get; set; }
        public BasicBlockAstNode? Block { get; set; }
        public bool IsDefault => Case?.Opcode.Opcode == (byte)OpcodeV3.Default;

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
            Case?.Visit(visitor);
            Block?.Visit(visitor);
        }
    }
}
