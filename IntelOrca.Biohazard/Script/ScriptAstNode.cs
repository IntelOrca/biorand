namespace IntelOrca.Biohazard.Script
{
    public class ScriptAstNode : IScriptAstNode
    {
        public BioVersion Version { get; }
        public SubroutineAstNode[] Subroutines { get; } = new SubroutineAstNode[0];

        public ScriptAstNode(BioVersion version, SubroutineAstNode[] subroutines)
        {
            Version = version;
            Subroutines = subroutines;
        }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
            foreach (var subroutine in Subroutines)
                subroutine.Visit(visitor);
        }
    }
}
