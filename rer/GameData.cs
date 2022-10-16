using System.Linq;

namespace rer
{
    internal class GameData
    {
        public Rdt[] Rdts { get; }

        public GameData(Rdt[] rdts)
        {
            Rdts = rdts;
        }

        public Rdt? GetRdt(int stage, int room)
        {
            return Rdts.FirstOrDefault(x => x.Stage == stage && x.RoomId == room);
        }
    }
}
