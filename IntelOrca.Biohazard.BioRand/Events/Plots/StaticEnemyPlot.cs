using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class StaticEnemyPlot : IPlot
    {
        public CsPlot BuildPlot(PlotBuilder builder)
        {
            var enemies = builder.AllocateEnemies();
            SbNode node =
                new SbContainerNode(
                    enemies.Select(x => new SbEnemy(x)).ToArray());

            var enemyCondition = builder.GetEnemyWaitCondition();
            if (enemyCondition != null)
            {
                node = new SbIf(enemyCondition, node);
            }

            return new CsPlot(new SbProcedure(
                new SbCommentNode($"[plot] {enemies.Length} enemies", node)));
        }
    }
}
