using System.Collections.Generic;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    public class ScriptAstBuilder : BioScriptVisitor
    {
        private List<SubroutineAstNode> _subroutines = new List<SubroutineAstNode>();
        private Stack<IfAstNode> _ifStack = new Stack<IfAstNode>();
        private Stack<SwitchAstNode> _switchStack = new Stack<SwitchAstNode>();
        private Stack<List<IScriptAstNode>> _statementStack = new Stack<List<IScriptAstNode>>();
        private bool _endOfSubroutine;

        public ScriptAst Ast { get; set; } = new ScriptAst();

        public override void VisitBeginScript(BioScriptKind kind)
        {
            _subroutines.Clear();
        }

        public override void VisitBeginSubroutine(int index)
        {
            _endOfSubroutine = false;
            _ifStack.Clear();
            _statementStack.Clear();
            PushBasicBlock();
        }

        protected override void VisitOpcode(OpcodeBase opcode)
        {
            if (_endOfSubroutine)
                return;

            CheckEndIfElseStatement(opcode.Offset);

            var opcodeNode = new OpcodeAstNode(opcode);
            switch (Version)
            {
                case BioVersion.Biohazard1:
                    switch ((OpcodeV1)opcode.Opcode)
                    {
                        case OpcodeV1.IfelCk:
                            VisitIfOpcode(opcodeNode);
                            break;
                        case OpcodeV1.Ck:
                        case OpcodeV1.Cmp6:
                        case OpcodeV1.Cmp7:
                        case OpcodeV1.TestItem:
                        case OpcodeV1.TestPickup:
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
                    break;
                case BioVersion.Biohazard2:
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
                    break;
                case BioVersion.Biohazard3:
                    switch ((OpcodeV3)opcode.Opcode)
                    {
                        case OpcodeV3.Switch:
                            VisitSwitchOpcode(opcodeNode);
                            break;
                        case OpcodeV3.Case:
                        case OpcodeV3.Default:
                            VisitCaseOpcode(opcodeNode);
                            break;
                        case OpcodeV3.Eswitch:
                            VisitEswitchOpcode(opcodeNode);
                            break;
                        case OpcodeV3.IfelCk:
                            VisitIfOpcode(opcodeNode);
                            break;
                        case OpcodeV3.Ck:
                        case OpcodeV3.Cmp:
                        case OpcodeV3.KeepItemCk:
                        case OpcodeV3.KeyCk:
                        case OpcodeV3.TrgCk:
                            VisitConditionOpcode(opcodeNode);
                            break;
                        case OpcodeV3.ElseCk:
                            VisitElseOpcode(opcodeNode);
                            break;
                        case OpcodeV3.EndIf:
                            VisitEndIfOpcode(opcodeNode);
                            break;
                        case OpcodeV3.EvtEnd:
                            VisitEndSubroutineOpcode(opcodeNode);
                            break;
                        default:
                            AddStatement(opcodeNode);
                            break;
                    }
                    break;
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
            if (_ifStack.Count == 0)
                return;

            var ifNode = _ifStack.Peek();
            ifNode.IfBlock = PopBasicBlock();
            ifNode.Else = opcodeNode;
            PushBasicBlock();
        }

        private void VisitEndIfOpcode(OpcodeAstNode opcodeNode)
        {
            if (_ifStack.Count == 0)
                return;

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

        private void VisitSwitchOpcode(OpcodeAstNode opcodeNode)
        {
            var switchNode = new SwitchAstNode();
            switchNode.Switch = opcodeNode;
            _switchStack.Push(switchNode);
            PushBasicBlock();
        }

        private void VisitCaseOpcode(OpcodeAstNode opcodeNode)
        {
            if (_switchStack.Count == 0)
                return;

            var switchNode = _switchStack.Peek();
            if (switchNode.Cases.Count != 0)
            {
                var lastCase = switchNode.Cases[switchNode.Cases.Count - 1];
                lastCase.Block = PopBasicBlock();
            }
            switchNode.Cases.Add(new CaseAstNode()
            {
                Case = opcodeNode
            });
            PushBasicBlock();
        }

        private void VisitEswitchOpcode(OpcodeAstNode opcodeNode)
        {
            if (_switchStack.Count == 0)
                return;

            var switchNode = _switchStack.Pop();
            if (switchNode.Cases.Count != 0)
            {
                var lastCase = switchNode.Cases[switchNode.Cases.Count - 1];
                lastCase.Block = PopBasicBlock();
            }
            PopBasicBlock();
            switchNode.Eswitch = opcodeNode;
            AddStatement(switchNode);
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
            while (_statementStack.Count > 1)
                PopBasicBlock();
            var basicBlockNode = PopBasicBlock();
            _subroutines.Add(new SubroutineAstNode(index, basicBlockNode.Statements));
        }

        public override void VisitEndScript(BioScriptKind kind)
        {
            var node = new ScriptAstNode(Version, _subroutines.ToArray());
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
}
