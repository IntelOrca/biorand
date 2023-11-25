using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal partial class CutsceneRandomiser
    {
        private class StaticEnemyPlot : Plot, INewPlot
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

            public CsPlot BuildPlot(PlotBuilder builder)
            {
                var enemies = builder.AllocateEnemies();
                return new CsPlot(new SbProcedure(
                    new SbCommentNode($"[plot] {enemies.Length} enemies",
                        enemies.Select(x => new SbEnemy(x)).ToArray())));
            }
        }
    }
}
