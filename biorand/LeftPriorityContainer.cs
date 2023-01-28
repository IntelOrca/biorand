using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    internal class LeftPriorityContainer : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = new Size();
            if (InternalChildren.Count > 0)
            {
                var firstChild = InternalChildren[0];
                firstChild.Measure(availableSize);
                size = firstChild.DesiredSize;

                availableSize.Width -= size.Width;
                availableSize.Height = size.Height;
                for (int i = 1; i < InternalChildren.Count; i++)
                {
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
