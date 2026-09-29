using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CAIME
{
    public class InsertionLineAdorner : Adorner
    {
        private const double LineThickness = 4;

        public bool IsBelow { get; }

        public InsertionLineAdorner(UIElement adornedElement, bool isBelow) : base(adornedElement)
        {
            IsBelow             = isBelow;
            IsHitTestVisible    = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var size  = AdornedElement.RenderSize;
            var edgeY = IsBelow ? size.Height : 0;

            drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, edgeY - LineThickness / 2, size.Width, LineThickness));
        }
    }
}
