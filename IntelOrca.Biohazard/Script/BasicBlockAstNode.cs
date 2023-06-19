namespace IntelOrca.Biohazard.Script
{
    public class BasicBlockAstNode : IScriptAstNode
    {
        public IScriptAstNode[] Statements { get; set; } = new IScriptAstNode[0];

        public BasicBlockAstNode(IScriptAstNode[] statements)
        {
            Statements = statements;
        }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
            foreach (var statement in Statements)
            {
                statement.Visit(visitor);
            }
        }
    }
}
