using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class StaticEnemyPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var enemies = builder.AllocateEnemies();
            return new CsPlot(new SbProcedure(
                new SbCommentNode($"[plot] {enemies.Length} enemies",
                    enemies.Select(x => new SbEnemy(x)).ToArray())));
        }
    }
}
