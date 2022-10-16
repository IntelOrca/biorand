namespace rer
{
    internal struct ItemPoolEntry
    {
        public RdtId RdtId { get; set; }
        public byte Id { get; set; }
        public ushort Type { get; set; }
        public ushort Amount { get; set; }
        public ushort[]? Requires { get; set; }

        public override string ToString()
        {
            return $"{RdtId}:{Id} {Items.GetItemName(Type)} x{Amount}";
        }
    }
}
