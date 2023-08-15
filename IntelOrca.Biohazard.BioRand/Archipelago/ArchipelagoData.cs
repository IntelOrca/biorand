using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.Archipelago
{
    public class ArchipelagoData
    {
        public string? Description { get; set; }
        public string? Seed { get; set; }
        public ArchipelagoRegion[]? Regions { get; set; }
        public ArchipelagoLocation[]? Locations { get; set; }
        public ArchipelagoItem[]? Items { get; set; }
    }

    public class ArchipelagoRegion
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public int[]? Locations { get; set; }
        public List<ArchipelagoEdge>? Edges { get; set; } = new List<ArchipelagoEdge>();

        public override string ToString() => $"{Id}: {Name}";
    }

    public class ArchipelagoEdge
    {
        public int? Region { get; set; }
        public int[]? Requires { get; set; }
    }

    public class ArchipelagoLocation
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public int Item { get; set; }
        public string? Priority { get; set; }
        public int[]? Requires { get; set; }

        public override string ToString() => $"{Name}: {Item}";
    }

    public class ArchipelagoItem
    {
        private int? _id;

        public int? Id
        {
            get => _id ?? Type | (Amount << 8);
            set => _id = value;
        }
        public int Type { get; set; }
        public int Amount { get; set; }
        public string? Name { get; set; }
        public string? Group { get; set; }

        public override string ToString() => $"{Id}: {Name}";
    }
}
