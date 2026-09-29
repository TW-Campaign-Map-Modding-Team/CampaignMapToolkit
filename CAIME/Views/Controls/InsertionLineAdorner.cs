using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CAIME
{
    /// <summary>
    /// Draws a thick white line along the top or bottom edge of an element, marking where a
    /// dragged item will be inserted
    /// </summary>
    public class InsertionLineAdorner : Adorner
    {
        private const double THICKNESS = 4;

        public bool IsBelow { get; }

        public InsertionLineAdorner(UIElement adornedElement, bool isBelow) : base(adornedElement)
        {
            IsBelow             = isBelow;
            IsHitTestVisible    = false;
        }

        // Centred on the edge, so between two rows the line straddles the gap the item drops into.
        protected override void OnRender(DrawingContext drawingContext)
        {
            var size  = AdornedElement.RenderSize;
            var edgeY = IsBelow ? size.Height : 0;

            drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, edgeY - THICKNESS / 2, size.Width, THICKNESS));
        }
    }
}
