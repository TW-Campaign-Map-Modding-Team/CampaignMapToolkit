using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace CAIME
{
    public class LayersViewModel : BaseViewModel
    {
        private Dictionary<LayerType, Layer> LayersMap;
        private List<Layer> defaultOrder;

        public ObservableCollection<Layer> Layers { get; private set; }

        private LayerType activeLayer;
        public LayerType ActiveLayer
        {
            get
            {
                return activeLayer;
            }
            private set
            {
                activeLayer = value;
                OnPropertyChanged(nameof(ActiveLayer));
                OnPropertyChanged(nameof(ActiveLayerModel));
            }
        }

        public Layer ActiveLayerModel => LayersMap != null && LayersMap.TryGetValue(ActiveLayer, out var layer) ? layer : null;

        public LayerType TopVisibleLayer    { get; private set; }

        public ICommand MoveLayerUpCommand            { get; }
        public ICommand MoveLayerDownCommand          { get; }
        public ICommand MoveLayerToTopCommand         { get; }
        public ICommand MoveLayerToBottomCommand      { get; }
        public ICommand ResetLayerOrderCommand        { get; }
        public ICommand SetLayerOpacityPercentCommand { get; }

        public event EventHandler LayerOrderChanged;

        public event EventHandler OpacityAdjustmentStarted;
        public event EventHandler OpacityAdjustmentEnded;

        public LayersViewModel()
        {
            MoveLayerUpCommand            = new RelayCommand<object>(layer => MoveLayer(layer as Layer, IndexOf(layer) - 1), CanMoveUp);
            MoveLayerDownCommand          = new RelayCommand<object>(layer => MoveLayer(layer as Layer, IndexOf(layer) + 1), CanMoveDown);
            MoveLayerToTopCommand         = new RelayCommand<object>(layer => MoveLayer(layer as Layer, 0), CanMoveUp);
            MoveLayerToBottomCommand      = new RelayCommand<object>(layer => MoveLayer(layer as Layer, Layers.Count - 1), CanMoveDown);
            ResetLayerOrderCommand        = new RelayCommand<object>(_ => ResetLayerOrder(), _ => IsInDefaultOrder() == false);
            SetLayerOpacityPercentCommand = new RelayCommand<object>(SetLayerOpacityPercent);
        }

        public bool Initialise(GameTemplate game, IReadOnlyList<LayerType> savedOrder = null)
        {
            LayersMap = new Dictionary<LayerType, Layer>
            {
                [LayerType.Impassable]  = new Layer(LayerType.Impassable),
                [LayerType.Roads]       = new Layer(LayerType.Roads),
                [LayerType.TownSlots]   = new Layer(LayerType.TownSlots),
                [LayerType.TownSprawl]  = new Layer(LayerType.TownSprawl),
                [LayerType.Bridges]     = new Layer(LayerType.Bridges),
                [LayerType.Rivers]      = new Layer(LayerType.Rivers),
                [LayerType.Beaches]     = new Layer(LayerType.Beaches),
                [LayerType.Regions]     = new Layer(LayerType.Regions),
                [LayerType.Attritions]  = new Layer(LayerType.Attritions),
                [LayerType.Climates]    = new Layer(LayerType.Climates),
                [LayerType.GroundTypes] = new Layer(LayerType.GroundTypes)
            };

            Layers = new ObservableCollection<Layer>
            {
                LayersMap[LayerType.Impassable],
                LayersMap[LayerType.Roads],
                LayersMap[LayerType.TownSlots],
                LayersMap[LayerType.TownSprawl],
                LayersMap[LayerType.Bridges],
                LayersMap[LayerType.Rivers],
                LayersMap[LayerType.Beaches],
                LayersMap[LayerType.Regions],
                LayersMap[LayerType.Attritions],
                LayersMap[LayerType.Climates],
                LayersMap[LayerType.GroundTypes]
            };

            if (game == GameTemplate.Rome2 ||
                game == GameTemplate.Attila ||
                game == GameTemplate.Thrones_Of_Britannia ||
                game == GameTemplate.Three_Kingdoms)
            {
                LayersMap[LayerType.TradeRoutes] = new Layer(LayerType.TradeRoutes);
                Layers.Insert(1, LayersMap[LayerType.TradeRoutes]);
            }

            if (game == GameTemplate.Attila ||
                game == GameTemplate.Thrones_Of_Britannia ||
                game == GameTemplate.Warhammer ||
                game == GameTemplate.Warhammer2 ||
                game == GameTemplate.Warhammer3 ||
                game == GameTemplate.Three_Kingdoms ||
                game == GameTemplate.Troy ||
                game == GameTemplate.Pharaoh ||
                game == GameTemplate.Pharaoh_Dynasties)
            {
                LayersMap[LayerType.RegionBorders] = new Layer(LayerType.RegionBorders);
                Layers.Insert(Layers.Count - 4, LayersMap[LayerType.RegionBorders]);

                LayersMap[LayerType.Restrictions] = new Layer(LayerType.Restrictions);
                Layers.Insert(Layers.Count - 5, LayersMap[LayerType.Restrictions]);
            }

            if (game == GameTemplate.Warhammer3 ||
                game == GameTemplate.Three_Kingdoms)
            {
                LayersMap[LayerType.AreasOfInterest] = new Layer(LayerType.AreasOfInterest);
                Layers.Insert(Layers.Count - 4, LayersMap[LayerType.AreasOfInterest]);
            }

            defaultOrder = Layers.ToList();

            if (savedOrder != null)
            {
                ArrangeLayers(MergeWithDefaultOrder(savedOrder));
            }

            OnPropertyChanged(nameof(Layers));
            OnPropertyChanged(nameof(ActiveLayerModel));
            RefreshCommandStates();

            LayersMap[LayerType.GroundTypes].SetActive(isActive: true, raiseEvent: true);
            LayersMap[LayerType.GroundTypes].SetVisible(isVisible: true, raiseEvent: true);

            return true;
        }

        /// <summary>
        /// Updates <see cref="ActiveLayer"/>
        /// </summary>
        public void UpdateActiveLayer(LayerType layer)
        {
            ActiveLayer = layer;
        }

        /// <summary>
        /// Updates <see cref="TopVisibleLayer"/>
        /// </summary>
        public void UpdateTopLayer()
        {
            for (int index = 0; index < Layers.Count; ++index)
            {
                if (Layers[index].IsVisible)
                {
                    TopVisibleLayer = Layers[index].Type;
                    return;
                }
            }

            TopVisibleLayer = LayerType.GroundTypes;
        }

        /// <summary>
        /// Set provided layer to be active (raises <see cref="Layer.ActiveLayerChanged"/> event)
        /// </summary>
        public void SetActiveLayer(LayerType layer)
        {
            ActiveLayer = layer;
            LayersMap[ActiveLayer].SetActive(true, raiseEvent: true);
        }

        public void ActivateLayer(Layer layer)
        {
            if (layer.IsActive == false)
            {
                SetActiveLayer(layer.Type);
            }
        }

        /// <summary>
        /// Set provided layer to be visible (raises <see cref="Layer.VisibilityChanged"/> event)
        /// </summary>
        public void SetVisibleLayer(LayerType layer)
        {
            LayersMap[layer].SetVisible(true, raiseEvent: true);
        }

        public void SetColours(ColourTable colourTable)
        {
            colourTable.SetColours(Layers);
        }

        /// <summary>
        /// Get active layer (can be only one)
        /// </summary>
        public Layer GetActiveLayer()
        {
            return LayersMap[ActiveLayer];
        }

        /// <summary>
        /// Get topmost visible layer
        /// </summary>
        /// <returns>First visible layer in layers stack</returns>
        public Layer GetTopLayer()
        {
            return LayersMap[TopVisibleLayer];
        }

        /// <summary>
        /// Determines whether newly painted colour can be displayed
        /// </summary>
        public bool CanDisplay(int colourIndex)
        {
            return CanDisplay(GetActiveLayer(), colourIndex);
        }

        /// <summary>
        /// Determines whether a colour painted on the given layer can change what the hex shows
        /// </summary>
        public bool CanDisplay(Layer layer, int colourIndex)
        {
            return LayerCompositor.CanChangeHex(Layers, layer, colourIndex);
        }

        public void BeginOpacityAdjustment()
        {
            OpacityAdjustmentStarted?.Invoke(this, EventArgs.Empty);
        }

        public void EndOpacityAdjustment()
        {
            OpacityAdjustmentEnded?.Invoke(this, EventArgs.Empty);
        }

        public bool IsDropBelow(Layer target, bool isOverLowerHalf)
        {
            return isOverLowerHalf && target == Layers.LastOrDefault();
        }

        public void DropLayer(Layer layer, Layer target, bool isOverLowerHalf)
        {
            if (IsDropBelow(target, isOverLowerHalf))
            {
                MoveLayerBelow(layer, target);
            }
            else
            {
                MoveLayerAbove(layer, target);
            }
        }

        public void MoveLayerAbove(Layer layer, Layer target)
        {
            var targetIndex = Layers.IndexOf(target);
            if (targetIndex >= 0)
            {
                MoveLayer(layer, Layers.IndexOf(layer) < targetIndex ? targetIndex - 1 : targetIndex);
            }
        }

        public void MoveLayerBelow(Layer layer, Layer target)
        {
            var targetIndex = Layers.IndexOf(target);
            if (targetIndex >= 0)
            {
                MoveLayer(layer, Layers.IndexOf(layer) > targetIndex ? targetIndex + 1 : targetIndex);
            }
        }

        public void ResetLayerOrder()
        {
            if (IsInDefaultOrder())
            {
                return;
            }

            ArrangeLayers(defaultOrder);
            OnLayerOrderChanged();
        }

        public bool IsInDefaultOrder()
        {
            return defaultOrder == null || Layers.SequenceEqual(defaultOrder);
        }

        private void ArrangeLayers(IReadOnlyList<Layer> order)
        {
            for (int index = 0; index < order.Count; ++index)
            {
                Layers.Move(Layers.IndexOf(order[index]), index);
            }
        }

        private List<Layer> MergeWithDefaultOrder(IReadOnlyList<LayerType> savedOrder)
        {
            var order = savedOrder.Distinct()
                                  .Where(LayersMap.ContainsKey)
                                  .Select(type => LayersMap[type])
                                  .ToList();

            for (int index = 0; index < defaultOrder.Count; ++index)
            {
                if (order.Contains(defaultOrder[index]) == false)
                {
                    order.Insert(Math.Min(index, order.Count), defaultOrder[index]);
                }
            }

            return order;
        }

        private void MoveLayer(Layer layer, int newIndex)
        {
            var oldIndex = Layers.IndexOf(layer);
            if (oldIndex < 0 || newIndex < 0 || newIndex >= Layers.Count || oldIndex == newIndex)
            {
                return;
            }

            Layers.Move(oldIndex, newIndex);
            OnLayerOrderChanged();
        }

        private void OnLayerOrderChanged()
        {
            UpdateTopLayer();
            RefreshCommandStates();
            LayerOrderChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void RefreshCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private static void SetLayerOpacityPercent(object parameter)
        {
            if (parameter is object[] values && values.Length == 2 && values[0] is Layer layer)
            {
                layer.Opacity = OpacityPercent.ToOpacity(Convert.ToDouble(values[1], CultureInfo.InvariantCulture));
            }
        }

        private int IndexOf(object layer)
        {
            return Layers?.IndexOf(layer as Layer) ?? -1;
        }

        private bool CanMoveUp(object layer)
        {
            return IndexOf(layer) > 0;
        }

        private bool CanMoveDown(object layer)
        {
            var index = IndexOf(layer);
            return index >= 0 && index < Layers.Count - 1;
        }

        public Layer GetLayerByName(string name)
        {
            foreach (var layer in Layers)
            {
                if (layer.Name == name)
                {
                    return layer;
                }
            }

            return null;
        }

        public List<LayerType> GetLayers()
        {
            var list = new List<LayerType>(Layers.Count);

            foreach (var layer in Layers)
            {
                list.Add(layer.Type);
            }

            return list;
        }

        public Layer GetLayer(LayerType type)
        {
            return LayersMap[type];
        }
    }
}
