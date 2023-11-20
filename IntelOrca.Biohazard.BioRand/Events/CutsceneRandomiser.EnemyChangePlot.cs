namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class EnemyChangePlot : Plot
        {
            protected override void Build()
            {
                var numPlacements = Rng.Next(6, 12);
                var placements = Cr._enemyPositions
                    .Next(numPlacements);
                var enemyIds = Builder.AllocateEnemies(placements.Length);

                Builder.IfPlotTriggered();
                LogTrigger("re-enter room");
                for (int i = 0; i < enemyIds.Length; i++)
                {
                    var opcode = GenerateEnemy(enemyIds[i], Cr._enemyPositions.Next());
                    Builder.Enemy(opcode);
                }
                LogAction($"{enemyIds.Length}x enemy");
            }
        }
    }
}
