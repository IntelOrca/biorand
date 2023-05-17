using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard
{
    public class CutsceneBuilder
    {
        private StringBuilder _sb = new StringBuilder();
        private int _enemyCount;
        private int _labelCount;
        private int _flagCount = 16;
        private int _plotCount;
        private Stack<int> _labelStack = new Stack<int>();
        private bool _else;
        private int _plotFlagIndex;

        public override string ToString() => _sb.ToString();

        public void Begin()
        {
        }

        public void End()
        {
            BeginProcedure("biorand_custom");
            for (int i = 0; i < _plotCount; i++)
            {
                Call($"plot_{i}");
            }
            EndProcedure();
        }

        public void BeginPlot()
        {
            BeginProcedure($"plot_{_plotCount}");
            _plotFlagIndex = CreateFlag();
            _plotCount++;
        }

        public void PlotActivated()
        {
            BeginIf();
            CheckFlag(4, _plotFlagIndex);
        }

        public void PlotTriggerLoop()
        {
            var plotLoopName = $"plot_loop_{_plotCount - 1}";

            Else();
            AppendLine("evt_exec", 255, "I_GOSUB", plotLoopName);
            EndIf();
            EndProcedure();

            BeginProcedure(plotLoopName);
        }

        public void EndPlot()
        {
            SetFlag(4, _plotFlagIndex);
            EndProcedure();
        }

        public int AddEnemy()
        {
            AppendLine("sce_em_set", 0, _enemyCount, "ENEMY_ZOMBIERANDOM", 6, 128, 0, 3, 0, 255, -32000, -10000, -32000, 0, 0, 0);
            _enemyCount++;
            return _enemyCount - 1;
        }

        public int AddNPC()
        {
            AppendLine("sce_em_set", 0, _enemyCount, "ENEMY_CLAIREREDFIELD", 0, 64, 0, 0, 0, 255, -32000, -10000, -32000, 0, 0, 0);
            _enemyCount++;
            return _enemyCount - 1;
        }

        public void MoveEnemy(int id, REPosition pos)
        {
            AppendLine("work_set", "WK_ENEMY", id);
            AppendLine("pos_set", 0, pos.X, pos.Y, pos.Z);
            AppendLine("dir_set", 0, 0, pos.D, 0);
        }

        public void SpawnEnemy(int id, REPosition pos)
        {
            MoveEnemy(id, pos);
            AppendLine("work_set", "WK_ENEMY", id);
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

        public void SetEnemyDestination(int id, REPosition pos, bool run = false)
        {
            WorkOnEnemy(id);
            AppendLine("plc_dest", 0, run ? 5 : 4, 32, pos.X, pos.Z);
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
            BeginLoop(4);
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
            BeginLoop(6);
            AppendLine("cmp", 0, 26, "CMP_NE", id);
            AppendLine("evt_next");
            AppendLine("nop");
            EndLoop();
        }

        public void EndTrigger()
        {
            EndIf();
        }

        public void BeginLoop(int conditionLength)
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
        }

        public void Call(string procedureName)
        {
            AppendLine("gosub", procedureName);
        }

        public string LabelName(int index) => $"label_{index}";

        public void AppendBlankLine() => _sb.AppendLine();

        public void AppendLine(string instruction, params object[] parameters)
        {
            _sb.AppendLine(string.Format("    {0,-24}{1}", instruction, string.Join(", ", parameters)));
        }
    }
}
