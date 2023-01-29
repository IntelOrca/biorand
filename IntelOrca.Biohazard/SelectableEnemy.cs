namespace IntelOrca.Biohazard
{
    public class SelectableEnemy
    {
        public string Name { get; }
        public string Colour { get; }
        public byte[] Types { get; }

        public SelectableEnemy(string name, string colour, byte[] types)
        {
            Name = name;
            Colour = colour;
            Types = types;
        }
    }
}
