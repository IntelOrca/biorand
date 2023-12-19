using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.Events
{
    internal sealed class SbNodeBuilder
    {
        private List<SbNode> _list = new List<SbNode>();

        public void Prepend(SbNode node)
        {
            _list.Insert(0, node);
        }

        public void Append(SbNode node)
        {
            _list.Add(node);
        }

        public void Append(SbNode[] node)
        {
            _list.AddRange(node);
        }

        public void Reparent(Func<SbNode, SbNode> f)
        {
            var result = f(new SbContainerNode(_list.ToArray()));
            _list.Clear();
            _list.Add(result);
        }

        public SbNode Build()
        {
            var result = new SbContainerNode(_list.ToArray());
            _list.Clear();
            return result;
        }
    }
}
