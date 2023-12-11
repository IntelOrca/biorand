using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class StaticEnemyPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var enemies = builder.AllocateEnemies();
            var proc = new SbProcedure(
                new SbCommentNode($"[plot] {enemies.Length} enemies",
                    builder.CreateEnemyConditionGuard(
                        enemies.Select(x => new SbEnemy(x)).ToArray())));
            return new CsPlot(proc, endOfScript: true);
        }
    }
}
