﻿using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    internal class GrowlessContainer : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, 0);
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
