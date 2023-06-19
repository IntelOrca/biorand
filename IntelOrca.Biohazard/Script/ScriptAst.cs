namespace IntelOrca.Biohazard.Script
{
    public class ScriptAst : IScriptAstNode
    {
        public ScriptAstNode? Init { get; set; }
        public ScriptAstNode? Main { get; set; }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
            Init?.Visit(visitor);
            Main?.Visit(visitor);
        }
    }
}
