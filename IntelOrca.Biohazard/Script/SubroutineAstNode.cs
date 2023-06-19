using System.Diagnostics;

namespace IntelOrca.Biohazard.Script
{
    [DebuggerDisplay("{DebuggerDisplay}")]
    public class SubroutineAstNode : BasicBlockAstNode
    {
        private string DebuggerDisplay => $"sub_{Index:X2}";
        public int Index { get; }

        public SubroutineAstNode(int index, IScriptAstNode[] statements)
            : base(statements)
        {
            Index = index;
        }
    }
}
