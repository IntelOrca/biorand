using System;
using System.Collections.Generic;
using System.Text;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand
{
    public class CutsceneBuilder
    {
        internal const int FG_STATUS = 1;
        internal const int FG_STOP = 2;
        internal const int FG_SCENARIO = 3;
        internal const int FG_COMMON = 4;
        internal const int FG_ROOM = 5;

        private StringBuilder _sb;
        private StringBuilder _sbMain = new StringBuilder();
        private StringBuilder _sbSub = new StringBuilder();
        private string? _subProcedureName;

        private int _labelCount;
        private int _plotCount;
        private Stack<int> _labelStack = new Stack<int>();
        private bool _else;
        private int _plotFlagIndex;
        private int _genericProcIndex;
        private bool _ifPlotTriggered;
        private bool _hasPlotTrigger;

        public Queue<int> AvailableEnemyIds { get; } = new Queue<int>();
        public HashSet<int> PlacedEnemyIds { get; } = new HashSet<int>();

        public override string ToString() => _sb.ToString();

        public CutsceneBuilder()
        {
            _sb = _sbMain;
        }

        public void Begin()
        {
        }

        public void End()
        {
            BeginProcedure("biorand_custom");
            UnlockPlot();
            for (int i = 0; i < _plotCount; i++)
            {
                Call($"plot_{i}");
            }
            EndProcedure();
        }

        public int BeginPlot(ushort flag)
        {
            BeginProcedure($"plot_{_plotCount}");
            _plotFlagIndex = flag;
            _plotCount++;
            _ifPlotTriggered = false;
            _hasPlotTrigger = false;
            return _plotFlagIndex;
        }

        public void IfPlotTriggered()
        {
            BeginIf();
            CheckFlag(_plotFlagIndex >> 8, _plotFlagIndex & 0xFF);
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
                SetFlag(_plotFlagIndex >> 8, _plotFlagIndex & 0xFF);
            }
            else if (_ifPlotTriggered)
            {
                Else();
                SetFlag(_plotFlagIndex >> 8, _plotFlagIndex & 0xFF);
                EndIf();
            }
            EndProcedure();
        }

        public void Event(REPosition pos, int size, string proc)
        {
            AppendLine("aot_set", 20, "SCE_EVENT", "SAT_PL | SAT_MANUAL | SAT_FRONT", pos.Floor, 0, pos.X - (size / 2), pos.Z - (size / 2), size, size, 255, 0, "I_GOSUB", proc, 0, 0);
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
            AppendLine("sce_em_set", 0, id, type, 0, 64, position.Floor, 3, 0, 255, position.X, position.Y, position.Z, position.D, 0, 0);
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
            AppendLine("calc", 0, "OP_OR", 16, "0x8000");
            AppendLine("member_set2", 7, 16);
        }

        public void ActivateEnemy(int id)
        {
            WorkOnEnemy(id);
            AppendLine("member_copy", 16, 7);
            AppendLine("calc", 0, "OP_AND", 16, "0x7FFF");
            AppendLine("member_set2", 7, 16);
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

        public void SetEnemyNeck(int id)
        {
            AppendLine("plc_neck", 5, 1, 0, 0, 148, 206);
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
            SetFlag(FG_STOP, 7, true);
        }

        public void EndCutsceneMode()
        {
            SetFlag(FG_STOP, 7, false);
            SetFlag(FG_STATUS, 27, false);
        }

        public void PlayMusic(RdtId rdtId, byte main, byte sub)
        {
            AppendLine("sce_bgmtbl_set", 0, rdtId.Room, rdtId.Stage, main | (sub << 8), 0);
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
        }

        public void CheckFlag(int group, int index, bool value = true)
        {
            AppendLine("ck", group, index, value ? 1 : 0);
        }

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

        public void WaitForPlot(int id) => WaitForFlag(id >> 8, id & 0xFF);

        public void WaitForPlotUnlock()
        {
            WaitForFlag(FG_ROOM, 23, false);
            WaitForFlag(FG_STATUS, 27, false);
            WaitForFlag(FG_STOP, 7, false);
        }

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

        public void BeginWhileLoop(int conditionLength)
        {
            AppendLine("while", conditionLength, LabelName(CreateLabel()));
        }

        public void EndLoop()
        {
            AppendLine("ewhile", 0);
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
                AppendLine("nop");
                AppendLine("nop");
            }
            else
            {
                AppendLine("endif");
                AppendLine("nop");
            }
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

            if (_subProcedureName == null && _sbSub.Length != 0)
            {
                _sbMain.Append(_sbSub.ToString());
                _sbSub.Clear();
            }
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
            if (_subProcedureName != null)
                throw new InvalidOperationException("Already in a sub procedure");

            _sb = _sbSub;
            _subProcedureName = AllocateProcedure();
            BeginProcedure(_subProcedureName);
            return _subProcedureName;
        }

        public string EndSubProcedure()
        {
            var subProcedureName = _subProcedureName;
            if (subProcedureName == null)
                throw new InvalidOperationException("Not in a sub procedure");

            EndProcedure();

            _sb = _sbMain;
            _subProcedureName = null;
            return subProcedureName;
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
    }

    public enum PlcDestKind
    {
        Walk = 4,
        Run = 5,
        Backstep = 8
    }
}
