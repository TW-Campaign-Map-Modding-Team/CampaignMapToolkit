using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for PropertiesControl.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {
        // x:Names of the row border and its drag grip in LayerTemplate.xaml
        private const string LAYER_ROW_NAME = "layerBorder";
        private const string DRAG_GRIP_NAME = "dragGrip";

        public readonly SidebarViewModel ViewModel = new SidebarViewModel();

        private Point                   layerDragStart;
        private Layer                   pressedLayer;
        private InsertionLineAdorner    insertionLine;

        public Sidebar()
        {
            InitializeComponent();

            DataContext                 = ViewModel;
            swatches.DataContext        = ViewModel.SwatchesVM;
            minimap.DataContext         = ViewModel.MinimapVM;
            layers.DataContext          = ViewModel.LayersVM;
            layerOpacity.DataContext    = ViewModel.LayersVM;
            actions.DataContext         = ViewModel.ActionsVM;
        }

        private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(sender as IInputElement);
            ViewModel.MinimapVM.MoveViewFrame(pos);
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(sender as IInputElement);
                ViewModel.MinimapVM.MoveViewFrame(pos);
            }
        }

        private void Canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ViewModel.MinimapVM.ScaleViewFrame(e.Delta);
            e.Handled = true;
        }

        private void OpacitySlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            ViewModel.LayersVM.BeginOpacityAdjustment();
        }

        private void OpacitySlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ViewModel.LayersVM.EndOpacityAdjustment();
        }

        private void Layers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            layerDragStart  = e.GetPosition(null);
            pressedLayer    = FindNamedAncestor(e.OriginalSource, DRAG_GRIP_NAME)?.DataContext as Layer;
        }

        private void Layers_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            pressedLayer = null;
        }

        private void Layers_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FindNamedAncestor(e.OriginalSource, DRAG_GRIP_NAME) != null)
            {
                return;
            }

            if (FindNamedAncestor(e.OriginalSource, LAYER_ROW_NAME)?.DataContext is Layer layer && layer.IsActive == false)
            {
                ViewModel.LayersVM.SetActiveLayer(layer.Type);
            }
        }

        private void Layers_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (pressedLayer == null || e.LeftButton != MouseButtonState.Pressed || IsBeyondDragThreshold(e.GetPosition(null)) == false)
            {
                return;
            }

            var draggedLayer = pressedLayer;
            pressedLayer = null;

            DragDrop.DoDragDrop((DependencyObject)sender, draggedLayer, DragDropEffects.Move);
            HideInsertionLine();
        }

        private void Layers_DragOver(object sender, DragEventArgs e)
        {
            var targetRow = FindNamedAncestor(e.OriginalSource, LAYER_ROW_NAME);
            var canDrop   = targetRow != null && e.Data.GetDataPresent(typeof(Layer));

            e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            if (canDrop)
            {
                ShowInsertionLine(targetRow, IsDropBelow(targetRow, e));
            }
            else
            {
                HideInsertionLine();
            }
        }

        private void Layers_DragLeave(object sender, DragEventArgs e)
        {
            HideInsertionLine();
        }

        private void Layers_Drop(object sender, DragEventArgs e)
        {
            HideInsertionLine();

            var draggedLayer = e.Data.GetData(typeof(Layer)) as Layer;
            var targetRow    = FindNamedAncestor(e.OriginalSource, LAYER_ROW_NAME);
            var targetLayer  = targetRow?.DataContext as Layer;
            if (draggedLayer == null || targetLayer == null)
            {
                return;
            }

            if (IsDropBelow(targetRow, e))
            {
                ViewModel.LayersVM.MoveLayerBelow(draggedLayer, targetLayer);
            }
            else
            {
                ViewModel.LayersVM.MoveLayerAbove(draggedLayer, targetLayer);
            }
        }

        private bool IsBeyondDragThreshold(Point position)
        {
            var offset = position - layerDragStart;
            return Math.Abs(offset.X) >= SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(offset.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        // A layer drops above the row under the cursor. The one exception is the lower half of the
        // last row, which is the only way to drop a layer at the bottom of the stack.
        private bool IsDropBelow(FrameworkElement row, DragEventArgs e)
        {
            return row.DataContext == ViewModel.LayersVM.Layers.LastOrDefault()
                && e.GetPosition(row).Y > row.ActualHeight / 2;
        }

        private void ShowInsertionLine(FrameworkElement row, bool isBelow)
        {
            if (insertionLine != null && insertionLine.AdornedElement == row && insertionLine.IsBelow == isBelow)
            {
                return;
            }

            HideInsertionLine();

            var adornerLayer = AdornerLayer.GetAdornerLayer(row);
            if (adornerLayer != null)
            {
                insertionLine = new InsertionLineAdorner(row, isBelow);
                adornerLayer.Add(insertionLine);
            }
        }

        private void HideInsertionLine()
        {
            if (insertionLine != null)
            {
                AdornerLayer.GetAdornerLayer(insertionLine.AdornedElement)?.Remove(insertionLine);
                insertionLine = null;
            }
        }

        private static FrameworkElement FindNamedAncestor(object source, string name)
        {
            var element = source as DependencyObject;
            while (element != null)
            {
                if (element is FrameworkElement frameworkElement && frameworkElement.Name == name)
                {
                    return frameworkElement;
                }

                element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }

            return null;
        }
    }
}
