using System;
using System.Collections.Generic;
using System.Text;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.Events
{
    public class CutsceneBuilder
    {
        internal const byte FG_STATUS = 1;
        internal const byte FG_STOP = 2;
        internal const byte FG_SCENARIO = 3;
        internal const byte FG_COMMON = 4;
        internal const byte FG_ROOM = 5;
        internal const byte FG_ITEM = 8;

        private StringBuilder _sb;
        private StringBuilder _sbMain = new StringBuilder();
        private List<Procedure> _subProcedureList = new List<Procedure>();
        private Stack<Procedure> _subProcedureStack = new Stack<Procedure>();

        private int _labelCount;
        private int _plotCount;
        private Stack<int> _labelStack = new Stack<int>();
        private bool _else;
        private ReFlag _plotFlag;
        private int _genericProcIndex;
        private bool _ifPlotTriggered;
        private bool _hasPlotTrigger;

        public Queue<int> AvailableAotIds { get; } = new Queue<int>();
        public Queue<int> AvailableEnemyIds { get; } = new Queue<int>();
        public HashSet<int> PlacedEnemyIds { get; } = new HashSet<int>();

        public override string ToString() => _sb.ToString();

        public CutsceneBuilder()
        {
            _sb = _sbMain;
        }

        public ReFlag BeginPlot(ReFlag flag)
        {
            BeginProcedure($"plot_{_plotCount}");
            _plotFlag = flag;
            _plotCount++;
            _ifPlotTriggered = false;
            _hasPlotTrigger = false;
            return _plotFlag;
        }

        public void IfPlotTriggered()
        {
            BeginIf();
            CheckFlag(_plotFlag);
            _ifPlotTriggered = true;
        }

        public void ElseBeginTriggerThread()
        {
            Else();
            BeginTriggerThread();
        }

        public void BeginTriggerThread()
        {
            if (!_ifPlotTriggered)
                throw new InvalidOperationException("Plot not activated");

            var plotLoopName = $"plot_loop_{_plotCount - 1}";

            CallThread(plotLoopName);
            EndIf();
            EndProcedure();

            BeginProcedure(plotLoopName);

            _hasPlotTrigger = true;
        }

        public void EndPlot()
        {
            if (_hasPlotTrigger)
            {
                SetFlag(_plotFlag);
            }
            else if (_ifPlotTriggered)
            {
                Else();
                SetFlag(_plotFlag);
                EndIf();
            }
            EndProcedure();
        }

        public void Event(int id, REPosition pos, int size, string proc)
        {
            AppendLine("aot_set", id, "SCE_EVENT", "SAT_PL | SAT_MANUAL | SAT_FRONT", pos.Floor, 0, pos.X - size / 2, pos.Z - size / 2, size, size, 255, 0, "I_GOSUB", proc, 0, 0);
        }

        public int[] AllocateAots(int count)
        {
            var result = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (AvailableAotIds.Count == 0)
                    break;

                result.Add(AvailableAotIds.Dequeue());
            }
            return result.ToArray();
        }

        public int[] AllocateEnemies(int count)
        {
            var result = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (AvailableEnemyIds.Count == 0)
                    break;

                result.Add(AvailableEnemyIds.Dequeue());
            }
            return result.ToArray();
        }

        public void Enemy(SceEmSetOpcode opcode)
        {
            AppendLine("sce_em_set", 0, opcode.Id, opcode.Type, opcode.State, opcode.Ai, opcode.Floor, opcode.SoundBank, 0, opcode.KillId,
                opcode.X, opcode.Y, opcode.Z, opcode.D, 0, 0);
            PlacedEnemyIds.Add(opcode.Id);
        }

        public void Ally(int id, byte type, REPosition position)
        {
            AppendLine("sce_em_set", 0, id, type, 0, 64, position.Floor, 0, 0, 255, position.X, position.Y, position.Z, position.D, 0, 0);
        }

        public void MoveEnemy(int id, REPosition pos)
        {
            WorkOnEnemy(id);
            AppendLine("pos_set", 0, pos.X, pos.Y, pos.Z);
            AppendLine("dir_set", 0, 0, pos.D, 0);
            AppendLine("member_set", "M_FLOOR", pos.Y / -1800);
        }

        public void DeactivateEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", 16, 7);
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", 16, "0x8000");
            AppendLine("member_set2", 7, 16);
            AppendLine("nop");
        }

        public void ActivateEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", 16, 7);
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", 16, "0x7FFF");
            AppendLine("member_set2", 7, 16);
            AppendLine("nop");
        }

        public void DisableEnemyCollision(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_POINTER");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", "V_TEMP", 0x0002);
            AppendLine("member_set2", "M_POINTER", "V_TEMP");
            AppendLine("nop");
        }

        public void EnableEnemyCollision(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_POINTER");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", "V_TEMP", 0xFFFD);
            AppendLine("member_set2", "M_POINTER", "V_TEMP");
            AppendLine("nop");
        }

        public void HideEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_BE_FLAG");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_OR", "V_TEMP", 0x0008);
            AppendLine("member_set2", "M_BE_FLAG", "V_TEMP");
            AppendLine("nop");
        }

        public void UnhideEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", "V_TEMP", "M_BE_FLAG");
            AppendLine("nop");
            AppendLine("calc", 0, "OP_AND", "V_TEMP", 0xFFF7);
            AppendLine("member_set2", "M_BE_FLAG", "V_TEMP");
            AppendLine("nop");
        }

        public void PlayVoice(int id)
        {
            AppendLine("xa_on", 0, id);
            AppendLine("wsleep");
            AppendLine("wsleeping");
        }

        public void PlayVoiceAsync(int id)
        {
            AppendLine("xa_on", 0, id);
        }

        public void SetEnemyDestination(int id, REPosition pos, PlcDestKind kind)
        {
            WorkOnEnemy(id);
            AppendLine("plc_dest", 0, (byte)kind, id == -1 ? 32 : 33, pos.X, pos.Z);
        }

        public void SetEnemyNeck(int id, int speed)
        {
            WorkOnEnemy(id);
            AppendLine("plc_neck", 5, 1, 0, 0, speed, speed);
        }

        public void SetEnemyNeck(int id, int speed, int x, int y)
        {
            WorkOnEnemy(id);
            AppendLine("plc_neck", 2, 0, x, y, speed, speed);
        }

        public void WorkOnEnemy(int id)
        {
            if (id == -1)
            {
                AppendLine("work_set", "WK_PLAYER", 0);
            }
            else
            {
                AppendLine("work_set", "WK_ENEMY", id);
            }
            AppendLine("nop");
        }

        public void WaitForEnemyTravel(int id)
        {
            BeginWhileLoop(4);
            CheckFlag(FG_ROOM, id == -1 ? 32 : 33, false);
            AppendLine("evt_next");
            AppendLine("nop");
            EndLoop();
        }

        public void ReleaseEnemyControl(int id)
        {
            WorkOnEnemy(id);
            AppendLine("plc_ret");
        }

        public void StopEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("plc_stop");
        }

        public void PlayDoorSoundOpen(REPosition pos)
        {
            AppendLine("se_on", 0, 0, 772, pos.X, pos.Y, pos.Z);
        }

        public void PlayDoorSoundClose(REPosition pos)
        {
            AppendLine("se_on", 0, 1, 772, pos.X, pos.Y, pos.Z);
        }

        public void CutChange(int cut)
        {
            AppendLine("cut_chg", cut);
        }

        public void CutRevert()
        {
            AppendLine("cut_old");
            AppendLine("nop");
            CutAuto();
        }

        public void CutAuto()
        {
            AppendLine("cut_auto", 1);
        }

        public void Sleep(int ticks)
        {
            AppendLine("sleep", 10, ticks);
        }

        public void BeginCutsceneMode()
        {
            SetFlag(FG_STATUS, 27, true);
            LockControls();
        }

        public void LockControls()
        {
            SetFlag(FG_STOP, 7, true);
        }

        public void UnlockControls()
        {
            SetFlag(FG_STOP, 7, false);
        }

        public void EndCutsceneMode()
        {
            UnlockControls();
            SetFlag(FG_STATUS, 27, false);
        }

        public void PlayMusic(RdtId rdtId, byte main, byte sub)
        {
            AppendLine("sce_bgmtbl_set", 0, rdtId.Room, rdtId.Stage, main | sub << 8, 0);
            AppendLine("sce_bgm_control", 0, 3, 0, 0, 0);
        }

        public void FadeOutMusic()
        {
            AppendLine("sce_bgm_control", 0, 5, 0, 0, 0);
        }

        public void ResumeMusic()
        {
            AppendLine("sce_bgm_control", 0, 3, 0, 0, 0);
        }

        public void SetFade(int a, int b, int c, int d, int e)
        {
            AppendLine("sce_fade_set", a, b, c, d, e);
        }

        public void AdjustFade(int a, int b, int c)
        {
            AppendLine("sce_fade_adjust", a, b, c);
        }

        public void Sleep1()
        {
            AppendLine("evt_next");
            AppendLine("nop");
        }

        public void Item(int globalId, int id, byte type, byte amount)
        {
            AppendLine("item_aot_set", id, "SCE_ITEM", "SAT_PL | SAT_MANUAL | SAT_FRONT", 0, 0, -32000, -32000, 0, 0, type, amount, globalId, 255, 0);
        }

        public void AotOn(int id)
        {
            AppendLine("aot_on", id);
            Sleep1();
        }

        public void CheckFlag(ReFlag flag, bool value = true) => CheckFlag(flag.Group, flag.Index, value);
        public void CheckFlag(int group, int index, bool value = true)
        {
            AppendLine("ck", group, index, value ? 1 : 0);
        }

        public void SetFlag(ReFlag flag, bool value = true) => SetFlag(flag.Group, flag.Index, value);
        public void SetFlag(int group, int index, bool value = true)
        {
            AppendLine("set", group, index, value ? 1 : 0);
        }

        public void WaitForTriggerCut(int id)
        {
            BeginWhileLoop(6);
            AppendLine("cmp", 0, 26, "CMP_NE", id);
            AppendLine("evt_next");
            AppendLine("nop");
            EndLoop();
        }

        public void WaitForPlot(ReFlag flag) => WaitForFlag(flag);

        public void WaitForPlotUnlock()
        {
            WaitForFlag(FG_ROOM, 23, false);
            WaitForFlag(FG_STATUS, 27, false);
            WaitForFlag(FG_STOP, 7, false);
        }

        public void WaitForFlag(ReFlag flag, bool value = true) => WaitForFlag(flag.Group, flag.Index, value);
        public void WaitForFlag(int flagGroup, int flag, bool value = true)
        {
            BeginWhileLoop(4);
            CheckFlag(flagGroup, flag, !value);
            AppendLine("evt_next");
            AppendLine("nop");
            EndLoop();
        }

        public void LockPlot()
        {
            SetFlag(FG_ROOM, 23);
        }

        public void UnlockPlot()
        {
            SetFlag(FG_ROOM, 23, false);
        }

        public void EndTrigger()
        {
            EndIf();
        }

        public void Goto(int label)
        {
            AppendLine("goto", 255, 255, 0, LabelName(label));
        }

        public void BeginWhileLoop(int conditionLength)
        {
            AppendLine("while", conditionLength, LabelName(CreateLabel()));
        }

        public void EndLoop()
        {
            AppendLine("ewhile", 0);
            AppendLabel();
        }

        public int BeginDoWhileLoop()
        {
            var label = CreateLabel();
            AppendLine("do", 0, LabelName(label));
            return label;
        }

        public void BeginDoWhileConditions(int label)
        {
            AppendLine("edwhile", LabelName(label));
        }

        public void EndDoLoop()
        {
            AppendLabel();
        }

        public void BeginIf()
        {
            var labelIndex = CreateLabel();
            AppendLine("if", 0, LabelName(labelIndex));
        }

        public void Else()
        {
            var index = _labelStack.Pop();
            AppendLine("else", 0, LabelName(CreateLabel()));
            AppendLabel(index);
            _else = true;
        }

        public void EndIf()
        {
            if (_else)
            {
                _else = false;
            }
            else
            {
                AppendLine("endif");
                AppendLine("nop");
            }
            AppendLabel();
        }

        public void Switch(object variable)
        {
            var labelIndex = CreateLabel();
            AppendLine("switch", variable, LabelName(labelIndex));
        }

        public void BeginSwitchCase(int value)
        {
            var labelIndex = CreateLabel();
            AppendLine("case", 0, LabelName(labelIndex), value);
        }

        public void EndSwitchCase()
        {
            AppendLine("break", 0);
            AppendLabel();
        }

        public void EndSwitch()
        {
            AppendLine("eswitch", 0);
            AppendLabel();
        }

        public int CreateLabel()
        {
            _labelStack.Push(_labelCount);
            _labelCount++;
            return _labelCount - 1;
        }

        public void AppendLabel()
        {
            AppendLabel(_labelStack.Pop());
        }

        public void AppendLabel(int index)
        {
            AppendBlankLine();
            _sb.AppendLine($"{LabelName(index)}:");
        }

        public void BeginProcedure(string name)
        {
            AppendBlankLine();
            _sb.AppendLine($".proc {name}");
        }

        public void EndProcedure()
        {
            AppendLine("evt_end", 0);

            foreach (var p in _subProcedureList)
            {
                _sbMain.Append(p.Sb.ToString());
            }
            _subProcedureList.Clear();
        }

        public void Call(string procedureName)
        {
            AppendLine("gosub", procedureName);
        }

        public void CallThread(string procedureName)
        {
            AppendLine("evt_exec", 255, "I_GOSUB", procedureName);
        }

        public string BeginSubProcedure()
        {
            var p = new Procedure(AllocateProcedure());
            _subProcedureList.Add(p);
            _subProcedureStack.Push(p);

            _sb = p.Sb;
            BeginProcedure(p.Name);
            return p.Name;
        }

        public string EndSubProcedure()
        {
            if (_subProcedureStack.Count == 0)
                throw new InvalidOperationException("Not in a sub procedure");

            var p = _subProcedureStack.Pop();
            AppendLine("evt_end", 0);

            if (_subProcedureStack.Count == 0)
            {
                _sb = _sbMain;
            }
            else
            {
                _sb = _subProcedureStack.Peek().Sb;
            }
            return p.Name;
        }

        public string AllocateProcedure()
        {
            _genericProcIndex++;
            return $"xproc_{_genericProcIndex}";
        }

        public string LabelName(int index) => $"label_{index}";

        public void AppendBlankLine() => _sb.AppendLine();

        public void AppendLine(string instruction, params object[] parameters)
        {
            _sb.AppendLine(string.Format("    {0,-24}{1}", instruction, string.Join(", ", parameters)));
        }

        private class Procedure
        {
            public string Name { get; }
            public StringBuilder Sb { get; } = new StringBuilder();

            public Procedure(string name)
            {
                Name = name;
            }
        }
    }

    public enum PlcDestKind
    {
        Walk = 4,
        Run = 5,
        Backstep = 8
    }
}
