using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class LayersViewModelTests
    {
        private const int Hex = 0;

        private LayersViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _viewModel = new LayersViewModel();
            _viewModel.Initialise(GameTemplate.Warhammer3);

            foreach (var layer in _viewModel.Layers)
            {
                layer.SetColours(new int[1]);
            }
        }

        [TestMethod]
        public void MoveLayerAbove_LandsDirectlyAboveTheTarget_WhetherMovingDownOrUp()
        {
            var roads   = LayerOf(LayerType.Roads);
            var regions = LayerOf(LayerType.Regions);
            var rivers  = LayerOf(LayerType.Rivers);

            _viewModel.MoveLayerAbove(roads, regions);
            Assert.AreEqual(_viewModel.Layers.IndexOf(regions) - 1, _viewModel.Layers.IndexOf(roads), "moving down");

            _viewModel.MoveLayerAbove(regions, rivers);
            Assert.AreEqual(_viewModel.Layers.IndexOf(rivers) - 1, _viewModel.Layers.IndexOf(regions), "moving up");
        }

        [TestMethod]
        public void MoveLayerAbove_ShiftsTheLayersBetween_AndKeepsTheirOrder()
        {
            var before = Order();
            var roads  = LayerOf(LayerType.Roads);

            _viewModel.MoveLayerAbove(roads, LayerOf(LayerType.Regions));

            var expected = before.ToList();
            expected.Remove(LayerType.Roads);
            expected.Insert(expected.IndexOf(LayerType.Regions), LayerType.Roads);
            CollectionAssert.AreEqual(expected, Order());
        }

        [TestMethod]
        public void MoveLayerBelow_LandsDirectlyBelowTheTarget_WhetherMovingDownOrUp()
        {
            var bottom = _viewModel.Layers.Last();
            var roads  = LayerOf(LayerType.Roads);

            _viewModel.MoveLayerBelow(roads, bottom);
            Assert.AreEqual(_viewModel.Layers.Count - 1, _viewModel.Layers.IndexOf(roads), "moving down to the bottom");

            _viewModel.MoveLayerBelow(roads, LayerOf(LayerType.Impassable));
            Assert.AreEqual(_viewModel.Layers.IndexOf(LayerOf(LayerType.Impassable)) + 1, _viewModel.Layers.IndexOf(roads), "moving up");
        }

        [TestMethod]
        public void MoveLayer_RaisesLayerOrderChanged_OnlyWhenTheOrderChanges()
        {
            var raised = 0;
            _viewModel.LayerOrderChanged += (sender, e) => ++raised;
            var roads     = LayerOf(LayerType.Roads);
            var nextLayer = _viewModel.Layers[_viewModel.Layers.IndexOf(roads) + 1];

            _viewModel.MoveLayerAbove(roads, roads);
            _viewModel.MoveLayerAbove(roads, nextLayer);
            _viewModel.MoveLayerBelow(nextLayer, roads);
            _viewModel.MoveLayerBelow(roads, new Layer(LayerType.TradeRoutes));
            Assert.AreEqual(0, raised, "already there, or the target is not in the stack");

            _viewModel.MoveLayerAbove(roads, LayerOf(LayerType.GroundTypes));
            Assert.AreEqual(1, raised, "real move");
        }

        [TestMethod]
        public void MoveUpAndDown_AreDisabledAtTheEdgesOfTheStack()
        {
            var top    = _viewModel.Layers.First();
            var bottom = _viewModel.Layers.Last();

            Assert.IsFalse(_viewModel.MoveLayerUpCommand.CanExecute(top),          "up at top");
            Assert.IsFalse(_viewModel.MoveLayerToTopCommand.CanExecute(top),       "to top at top");
            Assert.IsTrue(_viewModel.MoveLayerDownCommand.CanExecute(top),         "down at top");
            Assert.IsFalse(_viewModel.MoveLayerDownCommand.CanExecute(bottom),     "down at bottom");
            Assert.IsFalse(_viewModel.MoveLayerToBottomCommand.CanExecute(bottom), "to bottom at bottom");
            Assert.IsTrue(_viewModel.MoveLayerUpCommand.CanExecute(bottom),        "up at bottom");
        }

        [TestMethod]
        public void LayerCommands_AreDisabledForAnythingButALayerInTheStack()
        {
            var sentinel = new object();

            Assert.IsFalse(_viewModel.MoveLayerUpCommand.CanExecute(null));
            Assert.IsFalse(_viewModel.MoveLayerDownCommand.CanExecute(sentinel));
            Assert.IsFalse(_viewModel.MoveLayerDownCommand.CanExecute(new Layer(LayerType.TradeRoutes)));
        }

        [TestMethod]
        public void MoveCommands_MoveTheLayerOneStepOrToEitherEnd()
        {
            var rivers = LayerOf(LayerType.Rivers);
            var index  = _viewModel.Layers.IndexOf(rivers);

            _viewModel.MoveLayerUpCommand.Execute(rivers);
            Assert.AreEqual(index - 1, _viewModel.Layers.IndexOf(rivers), "up");

            _viewModel.MoveLayerDownCommand.Execute(rivers);
            Assert.AreEqual(index, _viewModel.Layers.IndexOf(rivers), "down");

            _viewModel.MoveLayerToTopCommand.Execute(rivers);
            Assert.AreEqual(0, _viewModel.Layers.IndexOf(rivers), "to top");

            _viewModel.MoveLayerToBottomCommand.Execute(rivers);
            Assert.AreEqual(_viewModel.Layers.Count - 1, _viewModel.Layers.IndexOf(rivers), "to bottom");
        }

        [TestMethod]
        public void ResetLayerOrder_RestoresTheOrderTheProjectOpenedWith()
        {
            var initial = Order();
            Assert.IsFalse(_viewModel.ResetLayerOrderCommand.CanExecute(null), "nothing to reset yet");

            _viewModel.MoveLayerAbove(LayerOf(LayerType.GroundTypes), LayerOf(LayerType.Impassable));
            _viewModel.MoveLayerAbove(LayerOf(LayerType.Beaches), LayerOf(LayerType.Climates));
            Assert.IsTrue(_viewModel.ResetLayerOrderCommand.CanExecute(null), "re-arranged");

            _viewModel.ResetLayerOrderCommand.Execute(null);

            CollectionAssert.AreEqual(initial, Order());
            Assert.IsFalse(_viewModel.ResetLayerOrderCommand.CanExecute(null), "reset");
        }

        [TestMethod]
        public void MovingALayer_TellsBoundControlsToRequeryTheResetCommand()
        {
            RunQueuedDispatcherWork();
            var requeried = false;
            EventHandler onCanExecuteChanged = (sender, e) => requeried = true;
            _viewModel.ResetLayerOrderCommand.CanExecuteChanged += onCanExecuteChanged;
            try
            {
                _viewModel.MoveLayerAbove(LayerOf(LayerType.GroundTypes), LayerOf(LayerType.Impassable));
                RunQueuedDispatcherWork();

                Assert.IsTrue(requeried);
                Assert.IsTrue(_viewModel.ResetLayerOrderCommand.CanExecute(null));
            }
            finally
            {
                _viewModel.ResetLayerOrderCommand.CanExecuteChanged -= onCanExecuteChanged;
            }
        }

        [TestMethod]
        public void CanDisplay_FollowsTheStackOrder_NotTheLayerTypeOrder()
        {
            var impassable = LayerOf(LayerType.Impassable);
            var regions    = LayerOf(LayerType.Regions);
            impassable.SetVisible(true);
            regions.SetVisible(true);
            regions.Colours[Hex] = 1;

            Assert.IsTrue(_viewModel.CanDisplay(impassable, Hex), "impassable above regions");

            _viewModel.MoveLayerAbove(regions, impassable);

            Assert.IsFalse(_viewModel.CanDisplay(impassable, Hex), "regions now covers impassable");
            Assert.IsTrue(_viewModel.CanDisplay(regions, Hex),     "regions is on top");
        }

        [TestMethod]
        public void CanDisplay_IgnoresHiddenAndUncolouredLayersAbove()
        {
            var roads   = LayerOf(LayerType.Roads);
            var regions = LayerOf(LayerType.Regions);
            _viewModel.MoveLayerAbove(regions, roads);
            roads.SetVisible(true);

            regions.Colours[Hex] = 1;
            Assert.IsTrue(_viewModel.CanDisplay(roads, Hex), "hidden layer above");

            regions.SetVisible(true);
            regions.Colours[Hex] = ColourTable.Zero;
            Assert.IsTrue(_viewModel.CanDisplay(roads, Hex), "uncoloured hex above");
        }

        [TestMethod]
        public void CanDisplay_IsBlockedByAnOpaqueLayerAbove_ButNotByOneTranslucentLayer()
        {
            var roads   = LayerOf(LayerType.Roads);
            var regions = LayerOf(LayerType.Regions);
            _viewModel.MoveLayerAbove(regions, roads);
            roads.SetVisible(true);
            regions.SetVisible(true);
            regions.Colours[Hex] = 1;

            Assert.IsFalse(_viewModel.CanDisplay(roads, Hex), "opaque regions above");

            regions.Opacity = Layer.FullyOpaque - 1;
            Assert.IsTrue(_viewModel.CanDisplay(roads, Hex), "regions lets some of roads through");
        }

        [TestMethod]
        public void CanDisplay_IsFalseForAFullyTransparentLayer()
        {
            var impassable = LayerOf(LayerType.Impassable);
            impassable.SetVisible(true);
            impassable.Opacity = 0;

            Assert.IsFalse(_viewModel.CanDisplay(impassable, Hex));
        }

        [TestMethod]
        public void OpeningAProjectAgain_StartsEveryLayerFullyOpaque()
        {
            LayerOf(LayerType.Regions).Opacity = 64;

            _viewModel.Initialise(GameTemplate.Warhammer3, Order());

            Assert.IsTrue(_viewModel.Layers.All(layer => layer.IsOpaque));
        }

        [TestMethod]
        public void MovingALayer_KeepsItsOpacity()
        {
            var roads = LayerOf(LayerType.Roads);
            roads.Opacity = 64;

            _viewModel.MoveLayerToBottomCommand.Execute(roads);

            Assert.AreEqual(64, _viewModel.Layers.Last().Opacity);
        }

        [TestMethod]
        public void SetLayerOpacityPercentCommand_SetsTheOpacity_AndRaisesOpacityChangedOnlyOnChange()
        {
            var roads  = LayerOf(LayerType.Roads);
            var raised = 0;
            roads.OpacityChanged += (sender, e) => ++raised;

            _viewModel.SetLayerOpacityPercentCommand.Execute(new object[] { roads, "50" });
            _viewModel.SetLayerOpacityPercentCommand.Execute(new object[] { roads, "50" });

            Assert.AreEqual(128, roads.Opacity);
            Assert.AreEqual(1, raised);

            _viewModel.SetLayerOpacityPercentCommand.Execute(new object[] { roads, "100" });
            Assert.IsTrue(roads.IsOpaque);
        }

        [TestMethod]
        public void SetLayerOpacityPercentCommand_IgnoresADisconnectedRow()
        {
            _viewModel.SetLayerOpacityPercentCommand.Execute(new object[] { new object(), "50" });

            Assert.IsTrue(_viewModel.Layers.All(layer => layer.IsOpaque));
        }

        [TestMethod]
        public void DropLayer_LandsAboveTheTarget_UnlessOverTheLowerHalfOfTheLastRow()
        {
            var roads   = LayerOf(LayerType.Roads);
            var regions = LayerOf(LayerType.Regions);
            var bottom  = _viewModel.Layers.Last();

            _viewModel.DropLayer(roads, regions, isOverLowerHalf: true);
            Assert.AreEqual(_viewModel.Layers.IndexOf(regions) - 1, _viewModel.Layers.IndexOf(roads), "lower half of a middle row");

            _viewModel.DropLayer(roads, bottom, isOverLowerHalf: false);
            Assert.AreEqual(_viewModel.Layers.IndexOf(bottom) - 1, _viewModel.Layers.IndexOf(roads), "upper half of the last row");

            _viewModel.DropLayer(roads, bottom, isOverLowerHalf: true);
            Assert.AreSame(roads, _viewModel.Layers.Last(), "lower half of the last row");
        }

        [TestMethod]
        public void IsDropBelow_OnlyForTheLowerHalfOfTheLastRow()
        {
            var bottom = _viewModel.Layers.Last();
            var top    = _viewModel.Layers.First();

            Assert.IsTrue(_viewModel.IsDropBelow(bottom, isOverLowerHalf: true));
            Assert.IsFalse(_viewModel.IsDropBelow(bottom, isOverLowerHalf: false));
            Assert.IsFalse(_viewModel.IsDropBelow(top, isOverLowerHalf: true));
        }

        [TestMethod]
        public void ActivateLayer_MakesAnInactiveLayerActive_AndLeavesAnActiveOneAlone()
        {
            var roads   = LayerOf(LayerType.Roads);
            var raised  = 0;
            roads.ActiveLayerChanged += (sender, e) => ++raised;

            _viewModel.ActivateLayer(roads);
            _viewModel.ActivateLayer(roads);

            Assert.AreEqual(LayerType.Roads, _viewModel.ActiveLayer);
            Assert.AreEqual(1, raised);
        }

        [TestMethod]
        public void CanDisplay_IsBlockedByTranslucentLayersAboveThatTogetherHideTheHex()
        {
            var roads = LayerOf(LayerType.Roads);
            roads.SetVisible(true);

            foreach (var layer in _viewModel.Layers.Where(layer => layer != roads).ToList())
            {
                _viewModel.MoveLayerAbove(layer, roads);
                layer.SetVisible(true);
                layer.Colours[Hex] = 1;
                layer.Opacity = 200;
            }

            Assert.IsFalse(_viewModel.CanDisplay(roads, Hex));
        }

        [TestMethod]
        public void ActiveLayerModel_FollowsTheActiveLayer_AndTellsBindingsWhenItChanges()
        {
            var changed = new List<string>();
            _viewModel.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            _viewModel.UpdateActiveLayer(LayerType.Roads);

            Assert.AreSame(LayerOf(LayerType.Roads), _viewModel.ActiveLayerModel);
            CollectionAssert.Contains(changed, nameof(LayersViewModel.ActiveLayerModel));
        }

        [TestMethod]
        public void OpacityAdjustment_RaisesStartedThenEnded()
        {
            var raised = new List<string>();
            _viewModel.OpacityAdjustmentStarted += (sender, e) => raised.Add("started");
            _viewModel.OpacityAdjustmentEnded   += (sender, e) => raised.Add("ended");

            _viewModel.BeginOpacityAdjustment();
            _viewModel.EndOpacityAdjustment();

            CollectionAssert.AreEqual(new[] { "started", "ended" }, raised);
        }

        [TestMethod]
        public void MovingALayer_UpdatesTheTopVisibleLayer()
        {
            var climates = LayerOf(LayerType.Climates);
            climates.SetVisible(true);
            _viewModel.UpdateTopLayer();
            Assert.AreEqual(LayerType.Climates, _viewModel.TopVisibleLayer, "climates sits above ground types");

            _viewModel.MoveLayerToTopCommand.Execute(LayerOf(LayerType.GroundTypes));

            Assert.AreEqual(LayerType.GroundTypes, _viewModel.TopVisibleLayer);
        }

        [TestMethod]
        public void Initialise_AppliesASavedOrder_WithoutRaisingLayerOrderChanged()
        {
            var saved  = Order().Reverse().ToArray();
            var raised = 0;
            var viewModel = new LayersViewModel();
            viewModel.LayerOrderChanged += (sender, e) => ++raised;

            viewModel.Initialise(GameTemplate.Warhammer3, saved);

            CollectionAssert.AreEqual(saved, viewModel.Layers.Select(layer => layer.Type).ToArray());
            Assert.IsFalse(viewModel.IsInDefaultOrder(), "saved order is not the built-in one");
            Assert.AreEqual(0, raised, "opening a project is not a user re-arrangement");
        }

        [TestMethod]
        public void Initialise_KeepsLayersTheSavedOrderMisses_AtTheirBuiltInPosition()
        {
            var builtIn = Order();
            var aoiIndex = Array.IndexOf(builtIn, LayerType.AreasOfInterest);
            var saved = builtIn.Reverse().Where(type => type != LayerType.AreasOfInterest).ToArray();

            var viewModel = new LayersViewModel();
            viewModel.Initialise(GameTemplate.Warhammer3, saved);
            var order = viewModel.Layers.Select(layer => layer.Type).ToArray();

            Assert.AreEqual(aoiIndex, Array.IndexOf(order, LayerType.AreasOfInterest), "missing layer at its built-in index");
            CollectionAssert.AreEqual(saved, order.Where(type => type != LayerType.AreasOfInterest).ToArray(), "the rest keep the saved order");
        }

        [TestMethod]
        public void Initialise_IgnoresSavedLayersTheGameDoesNotHave_AndDuplicates()
        {
            var saved = new[] { LayerType.GroundTypes, LayerType.TradeRoutes, LayerType.GroundTypes };

            var viewModel = new LayersViewModel();
            viewModel.Initialise(GameTemplate.Warhammer3, saved);
            var order = viewModel.Layers.Select(layer => layer.Type).ToList();

            Assert.AreEqual(Order().Length, order.Count, "same layers as the built-in stack");
            Assert.IsFalse(order.Contains(LayerType.TradeRoutes), "Warhammer 3 has no trade routes layer");
            Assert.AreEqual(1, order.Count(type => type == LayerType.GroundTypes));
        }

        private static void RunQueuedDispatcherWork()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private Layer LayerOf(LayerType type) => _viewModel.GetLayer(type);

        private LayerType[] Order() => _viewModel.Layers.Select(layer => layer.Type).ToArray();
    }
}
