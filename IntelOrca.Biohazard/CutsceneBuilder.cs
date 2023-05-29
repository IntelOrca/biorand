using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class CutsceneBuilder
    {
        private StringBuilder _sb;
        private StringBuilder _sbMain = new StringBuilder();
        private StringBuilder _sbSub = new StringBuilder();
        private string? _subProcedureName;

        private int _enemyCount;
        private int _labelCount;
        private int _flagCount = 16;
        private int _plotCount;
        private Stack<int> _labelStack = new Stack<int>();
        private bool _else;
        private int _plotFlagIndex;
        private int _genericProcIndex;
        private bool _ifPlotTriggered;
        private bool _hasPlotTrigger;

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

        public int BeginPlot()
        {
            BeginProcedure($"plot_{_plotCount}");
            _plotFlagIndex = CreateFlag();
            _plotCount++;
            _ifPlotTriggered = false;
            _hasPlotTrigger = false;
            return _plotFlagIndex;
        }

        public void IfPlotTriggered()
        {
            BeginIf();
            CheckFlag(4, _plotFlagIndex);
            _ifPlotTriggered = true;
        }

        public void ElseBeginTriggerThread()
        {
            if (!_ifPlotTriggered)
                throw new InvalidOperationException("Plot not activated");

            var plotLoopName = $"plot_loop_{_plotCount - 1}";

            Else();
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
                SetFlag(4, _plotFlagIndex);
            }
            else if (_ifPlotTriggered)
            {
                Else();
                SetFlag(4, _plotFlagIndex);
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
            var start = _enemyCount;
            _enemyCount += count;
            return Enumerable.Range(start, count).ToArray();
        }

        public void Enemy(int id, REPosition position, int pose, int state)
        {
            AppendLine("sce_em_set", 0, id, "ENEMY_ZOMBIERANDOM", pose, state, position.Floor, 3, 0, 255, position.X, position.Y, position.Z, position.D, 0, 0);
        }

        public void Ally(int id, REPosition position)
        {
            AppendLine("sce_em_set", 0, id, "ENEMY_CLAIREREDFIELD", 0, 64, position.Floor, 3, 0, 255, position.X, position.Y, position.Z, position.D, 0, 0);
        }

        public void MoveEnemy(int id, REPosition pos)
        {
            WorkOnEnemy(id);
            AppendLine("pos_set", 0, pos.X, pos.Y, pos.Z);
            AppendLine("dir_set", 0, 0, pos.D, 0);
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
            CheckFlag(5, id == -1 ? 32 : 33, false);
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
            SetFlag(1, 27, true);
            SetFlag(2, 7, true);
        }

        public void EndCutsceneMode()
        {
            SetFlag(2, 7, false);
            SetFlag(1, 27, false);
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

        public void CheckFlag(int group, int index, bool value = true)
        {
            AppendLine("ck", group, index, value ? 1 : 0);
        }

        public int CreateFlag()
        {
            _flagCount++;
            return _flagCount - 1;
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

        public void WaitForPlot(int id) => WaitForFlag(id);

        public void WaitForPlotUnlock()
        {
            WaitForFlag(15, false);
        }

        public void WaitForFlag(int flag, bool value = true)
        {
            BeginWhileLoop(4);
            CheckFlag(4, flag, !value);
            AppendLine("evt_next");
            AppendLine("nop");
            EndLoop();
        }

        public void LockPlot()
        {
            SetFlag(4, 15);
        }

        public void UnlockPlot()
        {
            SetFlag(4, 15, false);
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
