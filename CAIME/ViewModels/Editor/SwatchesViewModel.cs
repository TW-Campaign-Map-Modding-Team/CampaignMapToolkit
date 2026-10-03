using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CAIME
{
    public class ActiveSwatchChangedEventArgs : EventArgs
    {
        public Swatch ActiveSwatch { get; private set; }

        public ActiveSwatchChangedEventArgs(Swatch activeSwatch)
        {
            ActiveSwatch = activeSwatch;
        }
    }

    public delegate void ActiveSwatchChangedHandler(object sender, ActiveSwatchChangedEventArgs e);

    public class SwatchesViewModel : BaseViewModel
    {
        private Swatch _activeSwatch;
        public Swatch ActiveSwatch
        {
            get
            {
                return _activeSwatch;
            }
            set
            {
                _activeSwatch = value;

                if (_activeSwatch != null)
                {
                    SwatchColour = _activeSwatch.Colour;
                }

                OnPropertyChanged(nameof(ActiveSwatch));

                OnActiveSwatchChanged?.Invoke(this, new ActiveSwatchChangedEventArgs(ActiveSwatch));
            }
        }
        private int _swatchColour;
        public int SwatchColour
        {
            get
            {
                return _swatchColour;
            }
            set
            {
                _swatchColour = value;
                OnPropertyChanged(nameof(SwatchColour));
            }
        }
        private int _swatchIndex;
        public int SwatchIndex
        {
            get
            {
                return _swatchIndex;
            }
            set
            {
                _swatchIndex = value;
                OnPropertyChanged(nameof(SwatchIndex));
            }
        }

        public Dictionary<LayerType, List<Swatch>> Swatches { get; private set; }
        public Dictionary<LayerType, ObservableCollection<Swatch>> SwatchesCollection { get; private set; }
        public ObservableCollection<Swatch> ActiveSwatches { get; private set; }

        public event ActiveSwatchChangedHandler OnActiveSwatchChanged;

        public SwatchesViewModel()
        {
            Swatches = new Dictionary<LayerType, List<Swatch>>();
            SwatchesCollection = new Dictionary<LayerType, ObservableCollection<Swatch>>();
        }

        public void SetActiveSwatches(LayerType layer, int swatchIndex)
        {
            ActiveSwatches = SwatchesCollection[layer];
            OnPropertyChanged(nameof(ActiveSwatches));
            SwatchIndex = swatchIndex;
        }

        public void UpdateSwatchesSet(Project project)
        {
            // Both are rebuilt below for the layers this game has. Collections left behind from the
            // previous project would otherwise still be served for layers this one does not use.
            Swatches.Clear();
            SwatchesCollection.Clear();

            var riverSwatches = new List<Swatch>()
            {
                new RiverSwatch(true),
                // new RiverSwatch(false)
            };

            var roadSwatches = new List<Swatch>()
            {
                new RoadSwatch(true),
                // new RoadSwatch(false)
            };

            var nogoSwatches = new List<Swatch>()
            {
                new NogoSwatch(true),
                // new NogoSwatch(false)
            };

            var bridgesSwatches = new List<Swatch>()
            {
                // new BridgeSwatch(false),
                new BridgeSwatch(true),
            };

            var beachSwatches = new List<Swatch>()
            {
                // new BeachSwatch(false),
                new BeachSwatch(true),
            };

            var townSlotSwatches = new List<Swatch>();
            for (sbyte i = 0/*Hex.INVALID_SLOT_INDEX*/; i < Hex.MAX_SLOTS_COUNT; ++i)
            {
                townSlotSwatches.Add(new TownSlotSwatch(i));
            }

            var townSprawlSwatches = new List<Swatch>()
            {
                // new TownSprawlSwatch(false),
                new TownSprawlSwatch(true),
            };

            Swatches.Add(LayerType.Beaches,     beachSwatches);
            Swatches.Add(LayerType.Rivers,      riverSwatches);
            Swatches.Add(LayerType.Bridges,     bridgesSwatches);
            Swatches.Add(LayerType.TownSprawl,  townSprawlSwatches);
            Swatches.Add(LayerType.TownSlots,   townSlotSwatches);
            Swatches.Add(LayerType.Roads,       roadSwatches);
            Swatches.Add(LayerType.Impassable,  nogoSwatches);

            this.UpdateGroundTypeSwatches(project);
            this.UpdateAttritionSwatches(project);
            this.UpdateClimateSwatches(project);
            this.UpdateRegionSwatches(project);
            this.UpdateAreaOfInterestSwatches(project);

            if (project.Game == GameTemplate.Attila ||
                project.Game == GameTemplate.Thrones_Of_Britannia ||
                project.Game == GameTemplate.Warhammer ||
                project.Game == GameTemplate.Warhammer2 ||
                project.Game == GameTemplate.Warhammer3 ||
                project.Game == GameTemplate.Three_Kingdoms ||
                project.Game == GameTemplate.Troy ||
                project.Game == GameTemplate.Pharaoh ||
                project.Game == GameTemplate.Pharaoh_Dynasties)
            {
                var restrictionSwatches = new List<Swatch>();
                for (byte i = 0; i < Hex.MAX_RESTRICTIONS_COUNT; ++i)
                {
                    restrictionSwatches.Add(new RestrictionSwatch(i));
                }

                var regionBorderSwatches = new List<Swatch>()
                {
                    // new RegionBorderSwatch(false),
                    new RegionBorderSwatch(true),
                };

                Swatches.Add(LayerType.RegionBorders, regionBorderSwatches);
                Swatches.Add(LayerType.Restrictions, restrictionSwatches);

                SwatchesCollection[LayerType.RegionBorders] = new ObservableCollection<Swatch>(Swatches[LayerType.RegionBorders]);
                SwatchesCollection[LayerType.Restrictions]  = new ObservableCollection<Swatch>(Swatches[LayerType.Restrictions]);
            }

            if (project.Game == GameTemplate.Rome2 ||
                project.Game == GameTemplate.Attila ||
                project.Game == GameTemplate.Thrones_Of_Britannia ||
                project.Game == GameTemplate.Three_Kingdoms)
            {
                var tradeRouteSwatches = new List<Swatch>()
                {
                    // new TradeRouteSwatch(false),
                    new TradeRouteSwatch(true),
                };

                Swatches.Add(LayerType.TradeRoutes, tradeRouteSwatches);

                SwatchesCollection[LayerType.TradeRoutes] = new ObservableCollection<Swatch>(Swatches[LayerType.TradeRoutes]);
            }

            SwatchesCollection[LayerType.Beaches]       = new ObservableCollection<Swatch>(Swatches[LayerType.Beaches]);
            SwatchesCollection[LayerType.Rivers]        = new ObservableCollection<Swatch>(Swatches[LayerType.Rivers]);
            SwatchesCollection[LayerType.Bridges]       = new ObservableCollection<Swatch>(Swatches[LayerType.Bridges]);
            SwatchesCollection[LayerType.TownSprawl]    = new ObservableCollection<Swatch>(Swatches[LayerType.TownSprawl]);
            SwatchesCollection[LayerType.TownSlots]     = new ObservableCollection<Swatch>(Swatches[LayerType.TownSlots]);
            SwatchesCollection[LayerType.Roads]         = new ObservableCollection<Swatch>(Swatches[LayerType.Roads]);
            SwatchesCollection[LayerType.Impassable]    = new ObservableCollection<Swatch>(Swatches[LayerType.Impassable]);
        }

        public void UpdateSwatches(LayerType layerType, Project project)
        {
            switch (layerType)
            {
                case LayerType.GroundTypes:
                    this.UpdateGroundTypeSwatches(project);
                    break;
                case LayerType.Attritions:
                    this.UpdateAttritionSwatches(project);
                    break;
                case LayerType.Climates:
                    this.UpdateClimateSwatches(project);
                    break;
                case LayerType.Regions:
                    this.UpdateRegionSwatches(project);
                    break;
                case LayerType.AreasOfInterest:
                    this.UpdateAreaOfInterestSwatches(project);
                    break;
            }
        }

        public Swatch GetSwatchForHex(LayerType layerType, Hex hex)
        {
            if (Swatches.TryGetValue(layerType, out var swatches) == false)
            {
                return null;
            }

            switch (layerType)
            {
                case LayerType.GroundTypes:     return swatches.Find(s => ((GroundSwatch)s).GroundTypeIndex == hex.GroundTypeIndex);
                case LayerType.Attritions:      return swatches.Find(s => ((AttritionSwatch)s).AttritionIndex == hex.AttritionIndex);
                case LayerType.Climates:        return swatches.Find(s => ((ClimateSwatch)s).ClimateIndex == hex.ClimateIndex);
                case LayerType.AreasOfInterest: return swatches.Find(s => ((AreaOfInterestSwatch)s).AreaOfInterestIndex == hex.InterestIndex);
                case LayerType.Regions:         return swatches.Find(s => ((RegionSwatch)s).RegionIndex == hex.RegionId);
                case LayerType.RegionBorders:   return swatches.Find(s => ((RegionBorderSwatch)s).IsBorder == hex.IsBorder);
                case LayerType.Beaches:         return swatches.Find(s => ((BeachSwatch)s).IsBeach == hex.IsBeach);
                case LayerType.Rivers:          return swatches.Find(s => ((RiverSwatch)s).IsRiver == hex.IsRiver);
                case LayerType.Roads:           return swatches.Find(s => ((RoadSwatch)s).IsRoad == hex.IsRoad);
                case LayerType.Bridges:         return swatches.Find(s => ((BridgeSwatch)s).IsBridge == hex.IsBridge);
                case LayerType.TradeRoutes:     return swatches.Find(s => ((TradeRouteSwatch)s).IsTradeRoute == hex.IsTradeRoute);
                case LayerType.Impassable:      return swatches.Find(s => ((NogoSwatch)s).IsImpassable == hex.IsImpassable);
                case LayerType.TownSlots:       return swatches.Find(s => ((TownSlotSwatch)s).SlotIndex == hex.TownSlotIndex);
                case LayerType.TownSprawl:      return swatches.Find(s => ((TownSprawlSwatch)s).Value == hex.IsTownSprawl);
                case LayerType.Restrictions:    return swatches.Find(s => ((RestrictionSwatch)s).RestrictionLevel == hex.RestrictionLvl);
            }

            return null;
        }

        public void ResetIndex()
        {
            SwatchIndex = 0;
        }

        private void UpdateGroundTypeSwatches(Project project)
        {
            if (Swatches.ContainsKey(LayerType.GroundTypes))
            {
                Swatches[LayerType.GroundTypes].Clear();
            }
            else
            {
                Swatches[LayerType.GroundTypes] = new List<Swatch>();
            }

            var groundTypes = new List<string>();
            groundTypes.AddRange(project.MapHexFile.LandGroundTypes);
            groundTypes.AddRange(project.MapHexFile.SeaGroundTypes);

            var groundSwatches = Swatches[LayerType.GroundTypes];

            // groundSwatches.Add(new GroundSwatch("No ground type", Hex.INVALID_GROUND_TYPE_INDEX, project.ColourTable.GetTerrainColour(Hex.INVALID_GROUND_TYPE_INDEX)));
            for (sbyte index = 0; index < groundTypes.Count; ++index)
            {
                groundSwatches.Add(new GroundSwatch(groundTypes[index], index, project.ColourTable.GetTerrainColour(index)));
            }

            SwatchesCollection[LayerType.GroundTypes] = new ObservableCollection<Swatch>(Swatches[LayerType.GroundTypes]);
        }

        private void UpdateAttritionSwatches(Project project)
        {
            if (Swatches.ContainsKey(LayerType.Attritions))
            {
                Swatches[LayerType.Attritions].Clear();
            }
            else
            {
                Swatches[LayerType.Attritions] = new List<Swatch>();
            }

            var attritions = new List<string>(project.MapHexFile.Attritions);
            var attritionSwatches = Swatches[LayerType.Attritions];

            // attritionSwatches.Add(new AttritionSwatch("No attrition", Hex.INVALID_ATTRITION_INDEX, project.ColourTable.GetAttritionColour(Hex.INVALID_ATTRITION_INDEX)));
            for (sbyte index = 0; index < attritions.Count; ++index)
            {
                attritionSwatches.Add(new AttritionSwatch(attritions[index], index, project.ColourTable.GetAttritionColour(index)));
            }

            SwatchesCollection[LayerType.Attritions] = new ObservableCollection<Swatch>(Swatches[LayerType.Attritions]);
        }

        private void UpdateClimateSwatches(Project project)
        {
            if (Swatches.ContainsKey(LayerType.Climates))
            {
                Swatches[LayerType.Climates].Clear();
            }
            else
            {
                Swatches[LayerType.Climates] = new List<Swatch>();
            }

            var climates = new List<string>(project.MapHexFile.Climates);
            var climateSwatches = Swatches[LayerType.Climates];

            // climateSwatches.Add(new ClimateSwatch("No climate", Hex.INVALID_CLIMATE_INDEX, project.ColourTable.GetClimateColour(Hex.INVALID_CLIMATE_INDEX)));
            for (sbyte index = 0; index < climates.Count; ++index)
            {
                climateSwatches.Add(new ClimateSwatch(climates[index], index, project.ColourTable.GetClimateColour(index)));
            }

            SwatchesCollection[LayerType.Climates] = new ObservableCollection<Swatch>(Swatches[LayerType.Climates]);
        }

        private void UpdateAreaOfInterestSwatches(Project project)
        {
            if (project.Game != GameTemplate.Warhammer3 && project.Game != GameTemplate.Three_Kingdoms)
            {
                return;
            }

            if (Swatches.ContainsKey(LayerType.AreasOfInterest))
            {
                Swatches[LayerType.AreasOfInterest].Clear();
            }
            else
            {
                Swatches[LayerType.AreasOfInterest] = new List<Swatch>();
            }

            var areas = new List<string>(project.MapHexFile.AreasOfInterest);
            var areasSwatches = Swatches[LayerType.AreasOfInterest];

            // areasSwatches.Add(new AreaOfInterestSwatch("No area", Hex.INVALID_AREA_OF_INT_INDEX, project.ColourTable.GetAreaOfInterestColour(Hex.INVALID_AREA_OF_INT_INDEX)));
            for (sbyte index = 0; index < areas.Count; ++index)
            {
                areasSwatches.Add(new AreaOfInterestSwatch(areas[index], index, project.ColourTable.GetAreaOfInterestColour(index)));
            }

            SwatchesCollection[LayerType.AreasOfInterest] = new ObservableCollection<Swatch>(Swatches[LayerType.AreasOfInterest]);
        }

        private void UpdateRegionSwatches(Project project)
        {
            if (Swatches.ContainsKey(LayerType.Regions))
            {
                Swatches[LayerType.Regions].Clear();
            }
            else
            {
                Swatches[LayerType.Regions] = new List<Swatch>();
            }

            var regions = new List<string>();
            regions.AddRange(project.MapHexFile.LandRegions);
            regions.AddRange(project.MapHexFile.SeaRegions);

            var regionSwatches = Swatches[LayerType.Regions];
            var colourTable = project.ColourTable;

            // regionSwatches.Add(new RegionSwatch("No region", Hex.INVALID_REGION_INDEX, ColourTable.Zero));
            for (int index = 0; index < regions.Count; ++index)
            {
                regionSwatches.Add(new RegionSwatch(regions[index], index, colourTable.GetRegionColour(index)));
            }

            SwatchesCollection[LayerType.Regions] = new ObservableCollection<Swatch>(Swatches[LayerType.Regions]);
        }
    }
}
