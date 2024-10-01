namespace IntelOrca.Biohazard.BioRand.Routing
{
    public enum NodeKind : byte
    {
        AndGate,
        OrGate,
        OneWay,
        Item,
        ReusableKey,
        ConsumableKey,
        RemovableKey
    }
}
