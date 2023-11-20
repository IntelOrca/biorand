namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class StaticEnemyPlot : Plot
        {
            protected override void Build()
            {
                var count = Cr.TakeEnemyCountForEvent();
                var placements = Cr.GetEnemyPlacements(count);
                var ids = Builder.AllocateEnemies(count);
                for (int i = 0; i < ids.Length; i++)
                {
                    var opcode = GenerateEnemy(ids[i], placements[i]);
                    Builder.Enemy(opcode);
                }
                LogAction($"{ids.Length}x enemy");
            }
        }
    }
}
