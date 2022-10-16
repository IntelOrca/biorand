namespace rer
{
    internal class Utility
    {
        public static string GetHumanRoomId(int stage, int room)
        {
            return $"{stage + 1:X}{room:X2}";
        }
    }
}
