namespace rer
{
    internal class RdtBuilder : BioScriptVisitor
    {
        private readonly Rdt _rdt;

        public RdtBuilder(Rdt rdt)
        {
            _rdt = rdt;
        }

        protected override void VisitDoorAotSe(Door door)
        {
            _rdt.AddDoor(door);
        }

        protected override void VisitItemAotSet(Item item)
        {
            _rdt.AddItem(item);
        }

        protected override void VisitSceEmSet(RdtEnemy enemy)
        {
            _rdt.AddEnemy(enemy);
        }

        protected override void VisitAotReset(Reset reset)
        {
            _rdt.AddReset(reset);
        }

        protected override void VisitXaOn(RdtSound sound)
        {
            _rdt.AddSound(sound);
        }

        protected override void VisitSceItemGet(ItemGet itemGet)
        {
            _rdt.AddItemGet(itemGet);
        }
    }
}
