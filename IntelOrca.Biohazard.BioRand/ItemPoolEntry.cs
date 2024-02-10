namespace IntelOrca.Biohazard.BioRand
{
    internal struct ItemPoolEntry
    {
        public MapRoomItem Raw { get; set; }

        public RdtId RdtId { get; set; }
        public byte Id { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public byte[]? Requires { get; set; }
        public ItemPriority Priority { get; set; }
        public bool AllowDocuments { get; set; }

        public RdtItemId RdtItemId
        {
            get => new RdtItemId(RdtId, Id);
            set
            {
                RdtId = value.Rdt;
                Id = value.Id;
            }
        }

        public string ToString(IItemHelper itemHelper)
        {
            return $"{RdtId}:{Id} [{itemHelper.GetItemName((byte)Type)} x{Amount}]";
        }

        public override string ToString()
        {
            return $"{RdtId}:{Id} [{Type} x{Amount}]";
        }
    }
}
