using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.Archipelago
{
    public class ArchipelagoData
    {
        public string? Description { get; set; }
        public string? Seed { get; set; }
        public ArchipelagoRegion[]? Regions { get; set; }
        public ArchipelagoItem[]? Items { get; set; }
    }

    public class ArchipelagoRegion
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool Victory { get; set; }
        public ArchipelagoLocation[]? Locations { get; set; }
        public List<int>? Edges { get; set; }

        public override string ToString() => $"{Id}: {Name}";
    }

    public class ArchipelagoLocation
    {
        public string? Name { get; set; }
        public int Item { get; set; }
        public string? Priority { get; set; }

        public override string ToString() => $"{Name}: {Item}";
    }

    public class ArchipelagoItem
    {
        public int Id => Type | (Amount << 8);
        public int Type { get; set; }
        public int Amount { get; set; }
        public string? Name { get; set; }

        public override string ToString() => $"{Id}: {Name}";
    }
}
