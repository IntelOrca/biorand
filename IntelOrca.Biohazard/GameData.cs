using System.Linq;

namespace IntelOrca.Biohazard
{
    internal class GameData
    {
        public Rdt[] Rdts { get; }

        public GameData(Rdt[] rdts)
        {
            Rdts = rdts;
        }

        public Rdt? GetRdt(RdtId rtdId)
        {
            return Rdts.FirstOrDefault(x => x.RdtId == rtdId);
        }
    }
}
