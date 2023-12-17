namespace IntelOrca.Biohazard.BioRand.Events
{
    public struct REPosition
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int D { get; }

        public int Floor => Y / -1800;

        public REPosition(int x, int y, int z) : this(x, y, z, 0) { }
        public REPosition(int x, int y, int z, int d)
        {
            X = x;
            Y = y;
            Z = z;
            D = d;
        }

        public REPosition WithY(int y) => new REPosition(X, y, Z, D);
        public REPosition WithD(int d) => new REPosition(X, Y, Z, d);

        public REPosition Reverse()
        {
            return new REPosition(X, Y, Z, (D + 2048) % 4096);
        }

        public static REPosition OutOfBounds { get; } = new REPosition(-32000, -32000, -32000);

        public static REPosition operator +(REPosition a, REPosition b)
            => new REPosition(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.D + b.D);

        public override string ToString() => $"({X},{Y},{Z},{D})";
    }
}
