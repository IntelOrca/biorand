using System;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyFromDarkPlot : Plot
        {
            protected override void Build()
            {
                var min = Math.Max(1, Cr._maximumEnemyCount - 2);
                var max = Cr._maximumEnemyCount + 2;
                var count = Cr.TakeEnemyCountForEvent(min: min, max: max);
                var placements = Cr.GetEnemyPlacements(count);
                var ids = Builder.AllocateEnemies(count);

                Builder.IfPlotTriggered();
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    Builder.Enemy(opcode);
                }

                Builder.Else();
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], REPosition.OutOfBounds);
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }

                Builder.BeginTriggerThread();
                AddTriggers(minSleepTime: 5);
                Builder.LockPlot();

                Builder.LockControls();
                Builder.SetFade(0, 2, 7, 0, 0);
                for (var i = 0; i < 5; i++)
                {
                    Builder.AdjustFade(0, 0, (i & 1) == 0 ? 0 : 127);
                    Builder.Sleep1();
                }
                Builder.AdjustFade(0, 0, 127);
                Builder.Sleep(30);
                for (var i = 0; i < count; i++)
                {
                    Builder.MoveEnemy(ids[i], placements[i]);
                    Builder.ActivateEnemy(ids[i]);
                }
                Builder.AdjustFade(0, 0, 0);
                Builder.Sleep1();
                Builder.SetFade(0, 2, 7, 255, 127);
                Builder.Sleep1();
                Builder.UnlockControls();

                Builder.UnlockPlot();
                LogAction($"{count}x enemy spawn in from dark");
            }
        }
    }
}
