namespace IntelOrca.Biohazard.BioRand.Routing
{
    public enum NodeKind : byte
    {
        AndGate,
        OrGate,
        OneWay,
        Item,
        ReusuableKey,
        ConsumableKey,
        RemovableKey
    }
}
