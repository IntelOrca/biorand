using System.Collections.Generic;
using System.Diagnostics;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    internal class ScriptAstBuilder : BioScriptVisitor
    {
        private List<SubroutineAstNode> _subroutines = new List<SubroutineAstNode>();
        private Stack<IfAstNode> _ifStack = new Stack<IfAstNode>();
        private Stack<List<IScriptAstNode>> _statementStack = new Stack<List<IScriptAstNode>>();
        private bool _endOfSubroutine;
        private BioVersion _version;

        public ScriptAst Ast { get; set; } = new ScriptAst();

        public override void VisitVersion(BioVersion version)
        {
            _version = version;
        }

        public override void VisitBeginScript(BioScriptKind kind)
        {
            _subroutines.Clear();
        }

        public override void VisitBeginSubroutine(int index)
        {
            _endOfSubroutine = false;
            PushBasicBlock();
        }

        protected override void VisitOpcode(OpcodeBase opcode)
        {
            if (_endOfSubroutine)
                return;

            CheckEndIfElseStatement(opcode.Offset);

            var opcodeNode = new OpcodeAstNode(opcode);
            if (_version == BioVersion.Biohazard1)
            {
                switch ((OpcodeV1)opcode.Opcode)
                {
                    case OpcodeV1.IfelCk:
                        VisitIfOpcode(opcodeNode);
                        break;
                    case OpcodeV1.Ck:
                    case OpcodeV1.Cmp6:
                    case OpcodeV1.Cmp7:
                        VisitConditionOpcode(opcodeNode);
                        break;
                    case OpcodeV1.ElseCk:
                        VisitElseOpcode(opcodeNode);
                        break;
                    case OpcodeV1.EndIf:
                        VisitEndIfOpcode(opcodeNode);
                        break;
                    default:
                        AddStatement(opcodeNode);
                        break;
                }
            }
            else
            {
                switch ((OpcodeV2)opcode.Opcode)
                {
                    case OpcodeV2.IfelCk:
                        VisitIfOpcode(opcodeNode);
                        break;
                    case OpcodeV2.Ck:
                    case OpcodeV2.Cmp:
                    case OpcodeV2.MemberCmp:
                        VisitConditionOpcode(opcodeNode);
                        break;
                    case OpcodeV2.ElseCk:
                        VisitElseOpcode(opcodeNode);
                        break;
                    case OpcodeV2.EndIf:
                        VisitEndIfOpcode(opcodeNode);
                        break;
                    case OpcodeV2.EvtEnd:
                        VisitEndSubroutineOpcode(opcodeNode);
                        break;
                    default:
                        AddStatement(opcodeNode);
                        break;
                }
            }
        }

        private void VisitIfOpcode(OpcodeAstNode opcodeNode)
        {
            var ifNode = new IfAstNode();
            ifNode.If = opcodeNode;
            _ifStack.Push(ifNode);
            PushBasicBlock();
        }

        private void VisitConditionOpcode(OpcodeAstNode opcodeNode)
        {
            if (_ifStack.Count != 0)
            {
                var ifNode = _ifStack.Peek();
                ifNode.Conditions.Add(opcodeNode);
            }
        }

        private void VisitElseOpcode(OpcodeAstNode opcodeNode)
        {
            var ifNode = _ifStack.Peek();
            ifNode.IfBlock = PopBasicBlock();
            ifNode.Else = opcodeNode;
            PushBasicBlock();
        }

        private void VisitEndIfOpcode(OpcodeAstNode opcodeNode)
        {
            var ifNode = _ifStack.Pop();
            if (ifNode.IfBlock == null)
            {
                ifNode.IfBlock = PopBasicBlock();
            }
            else
            {
                ifNode.ElseBlock = PopBasicBlock();
            }
            ifNode.EndIf = opcodeNode;
            AddStatement(ifNode);
        }

        private void VisitEndSubroutineOpcode(OpcodeAstNode opcodeNode)
        {
            AddStatement(opcodeNode);
            if (_ifStack.Count == 0)
            {
                _endOfSubroutine = true;
            }
        }

        public override void VisitEndSubroutine(int index)
        {
            var basicBlockNode = PopBasicBlock();
            _subroutines.Add(new SubroutineAstNode(index, basicBlockNode.Statements));
        }

        public override void VisitEndScript(BioScriptKind kind)
        {
            var node = new ScriptAstNode(_version, _subroutines.ToArray());
            if (kind == BioScriptKind.Init)
            {
                Ast.Init = node;
            }
            else if (kind == BioScriptKind.Main)
            {
                Ast.Main = node;
            }
        }

        private void AddStatement(IScriptAstNode statement)
        {
            var statements = _statementStack.Peek();
            statements.Add(statement);
        }

        private void PushBasicBlock()
        {
            _statementStack.Push(new List<IScriptAstNode>());
        }

        private BasicBlockAstNode PopBasicBlock()
        {
            return new BasicBlockAstNode(_statementStack.Pop().ToArray());
        }

        private void CheckEndIfElseStatement(int offset)
        {
            if (_ifStack.Count == 0)
                return;

            var ifNode = _ifStack.Peek();
            if (ifNode.Else == null)
                return;

            var elseCkOpcode = (ElseCkOpcode)ifNode.Else.Opcode;
            var endOffset = elseCkOpcode.Offset + elseCkOpcode.BlockLength;
            if (offset == endOffset)
            {
                ifNode = _ifStack.Pop();
                ifNode.ElseBlock = PopBasicBlock();
                AddStatement(ifNode);
            }
        }
    }

    internal class ScriptAstVisitor
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

    internal class ScriptAst : IScriptAstNode
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

    internal interface IScriptAstNode
    {
        void Visit(ScriptAstVisitor visitor);
    }

    [DebuggerDisplay("{Opcode}")]
    internal class OpcodeAstNode : IScriptAstNode
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

    internal class ScriptAstNode : IScriptAstNode
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

    [DebuggerDisplay("{DebuggerDisplay}")]
    internal class SubroutineAstNode : BasicBlockAstNode
    {
        private string DebuggerDisplay => $"sub_{Index:X2}";
        public int Index { get; }

        public SubroutineAstNode(int index, IScriptAstNode[] statements)
            : base(statements)
        {
            Index = index;
        }
    }

    internal class BasicBlockAstNode : IScriptAstNode
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

    internal class IfAstNode : IScriptAstNode
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
