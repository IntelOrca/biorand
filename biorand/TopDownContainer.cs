using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    internal class TopDownContainer : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(MinWidth, MinHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }
            return finalSize;
        }
    }
}
