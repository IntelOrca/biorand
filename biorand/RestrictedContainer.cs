using System;
using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    internal class RestrictedContainer : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = new Size(0, 0);
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(availableSize);
                size.Width = Math.Max(size.Width, child.DesiredSize.Width);
                size.Height = Math.Max(size.Height, child.DesiredSize.Height);
            }
            if (!double.IsInfinity(availableSize.Width))
                size.Width = 0;
            if (!double.IsInfinity(availableSize.Height))
                size.Height = 0;
            return size;
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
