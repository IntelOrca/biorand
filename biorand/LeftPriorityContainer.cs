﻿using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    internal class LeftPriorityContainer : Panel
    {
        private int _importantChildIndex;

        public int ImportantChildIndex
        {
            get => _importantChildIndex;
            set
            {
                if (_importantChildIndex != value)
                {
                    _importantChildIndex = value;
                    InvalidateMeasure();
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = new Size();
            if (InternalChildren.Count > ImportantChildIndex && ImportantChildIndex >= 0)
            {
                var importantChild = InternalChildren[ImportantChildIndex];
                importantChild.Measure(availableSize);
                size = importantChild.DesiredSize;

                availableSize.Width -= size.Width;
                availableSize.Height = size.Height;
                for (int i = 0; i < InternalChildren.Count; i++)
                {
                    if (i == ImportantChildIndex)
                        continue;

                    var child = InternalChildren[i];
                    child.Measure(availableSize);
                    size.Width += child.DesiredSize.Width;
                    availableSize.Width -= child.DesiredSize.Width;
                }
            }
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;
            foreach (UIElement child in InternalChildren)
            {
                var childSize = child.DesiredSize;
                child.Arrange(new Rect(x, 0, childSize.Width, finalSize.Height));
                x += childSize.Width;
            }
            return finalSize;
        }
    }
}
