namespace IntelOrca.Biohazard.Script
{
    public interface IScriptAstNode
    {
        void Visit(ScriptAstVisitor visitor);
    }
}
