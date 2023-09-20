using IntelOrca.Biohazard.BioRand.Process;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2ProcessHelper : IProcessHelper
    {
        private readonly IProcess _process;

        public Re2ProcessHelper(IProcess process)
        {
            _process = process;
        }

        public ItemBox GetItemBox()
        {
            var items = _process.ReadArray<ReItem>(0x0098ED60, 64);
            return new ItemBox(items);
        }

        public void SetItemBox(ItemBox itemBox)
        {
            _process.WriteArray<ReItem>(0x0098ED60, itemBox.Items);
        }
    }
}
