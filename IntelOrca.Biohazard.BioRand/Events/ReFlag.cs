namespace IntelOrca.Biohazard.BioRand.Events
{
    public readonly struct ReFlag
    {
        public ushort Value { get; }

        public ReFlag(ushort value)
        {
            Value = value;
        }

        public ReFlag(byte group, byte index)
        {
            Value = (ushort)((group << 8) | index);
        }

        public byte Group => (byte)(Value >> 8);
        public byte Index => (byte)(Value & 0xFF);

        public override string ToString() => $"{Group}:{Index}";
    }
}
