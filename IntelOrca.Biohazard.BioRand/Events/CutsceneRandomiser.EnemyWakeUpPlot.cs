namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyWakeUpPlot : Plot
        {
            protected override void Build()
            {
                var count = Cr.TakeEnemyCountForEvent();
                var placements = Cr.GetEnemyPlacements(count);
                var ids = Builder.AllocateEnemies(count);

                Builder.IfPlotTriggered();
                // Setup enemies in woken up positions
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    Builder.Enemy(opcode);
                }

                Builder.Else();

                // Setup initial enemy positions
                for (int i = 0; i < count; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    opcode.State = 4;
                    opcode.Ai = 128;
                    Builder.Enemy(opcode);
                }
                for (var i = 0; i < count; i++)
                {
                    Builder.DisableEnemyCollision(ids[i]);
                }

                // Wait for triggers
                Builder.BeginTriggerThread();
                AddTriggers();

                // Wake up enemies incrementally
                Builder.LockPlot();
                foreach (var eid in ids)
                {
                    Builder.Sleep(Rng.Next(5, 15));
                    Builder.EnableEnemyCollision(eid);
                    Builder.ActivateEnemy(eid);
                }
                Builder.UnlockPlot();
                LogAction($"{ids.Length}x enemy wake up");
            }
        }
    }
}
