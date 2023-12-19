using IntelOrca.Biohazard.BioRand.RE2;

namespace IntelOrca.Biohazard.BioRand.Events.Plots
{
    internal class PartnerPlot : IPlot
    {
        private const byte AI_FOLLOW = 0x04;
        private const byte AI_WAIT = 0x40;

        public CsPlot? BuildPlot(PlotBuilder builder)
        {
            var enemyType = builder.EnemyType ?? 0;
            if (enemyType == Re2EnemyIds.ZombieArms)
                return null;

            var partner = builder.AllocatePartner();
            if (partner == null)
                return null;

            var randomPoi = builder.PoiGraph.GetRandomPoi(builder.Rng, _ => true);
            if (randomPoi == null)
                return null;

            var node = new SbAlly(partner, randomPoi.Position, AI_FOLLOW);
            return new CsPlot(new SbProcedure(node));
        }
    }
}
