namespace IntelOrca.Biohazard.BioRand.Events
{
    internal interface IPlot
    {
        public CsPlot? BuildPlot(PlotBuilder builder);
    }
}
