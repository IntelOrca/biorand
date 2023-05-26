namespace IntelOrca.Biohazard
{
    internal struct ItemPoolEntry
    {
        public RdtId RdtId { get; set; }
        public int Offset { get; set; }
        public byte Id { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public ushort[]? Requires { get; set; }
        public ItemPriority Priority { get; set; }

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
            return $"{RdtId}:{Id} [{Items.GetItemName(Type)} x{Amount}]";
        }
    }
}
