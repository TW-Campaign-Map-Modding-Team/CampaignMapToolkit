using System;
using System.Collections.ObjectModel;

namespace CAIME
{
    /// <summary>
    /// Colour lookup table for the editor
    /// </summary>
    public class ColourTable
    {
        private readonly MapHexFile _mapHexFile;

        public ColourTable(MapHexFile mapHexFile)
        {
            _mapHexFile = mapHexFile;
        }

        #region Constants

        public const int White                      = -1;
        public const int Zero                       = 0;
        public const int Road                       = -15514229;
        public const int Impassable                 = -16776961;
        public const int Beach                      = -14094096;
        public const int Bridge                     = -1382640;
        public const int River                      = -65536;
        public const int MaskedHex                  = -65281;

        public static int[] MicrosoftColourTable = new int[]
        {
            -16777216,
            -8388608,
            -16744448,
            -8355840,
            -16777088,
            -8388480,
            -16744320,
            -8355712,
            -4137792,
            -5846288,
            -14008406,
            -14008321,
            -14000384,
            -14000299,
            -14000214,
            -14000129,
            -13992192,
            -13992107,
            -13992022,
            -13991937,
            -13984000,
            -13983915,
            -13983830,
            -13983745,
            -13975808,
            -13975723,
            -13975638,
            -13975553,
            -13967616,
            -13967531,
            -13967446,
            -13967361,
            -13959424,
            -13959339,
            -13959254,
            -13959169,
            -11206656,
            -11206571,
            -11206486,
            -11206401,
            -11198720,
            -11198635,
            -11198550,
            -11198465,
            -11190528,
            -11190443,
            -11190358,
            -11190273,
            -11182336,
            -11182251,
            -11182166,
            -11182081,
            -11174144,
            -11174059,
            -11173974,
            -11173889,
            -11165952,
            -11165867,
            -11165782,
            -11165697,
            -11157760,
            -11157675,
            -11157590,
            -11157505,
            -11149568,
            -11149483,
            -11149398,
            -11149313,
            -11141376,
            -11141291,
            -11141206,
            -11141121,
            -8454144,
            -8454059,
            -8453974,
            -8453889,
            -8446208,
            -8446123,
            -8446038,
            -8445953,
            -8438016,
            -8437931,
            -8437846,
            -8437761,
            -8429824,
            -8429739,
            -8429654,
            -8429569,
            -8421632,
            -8421547,
            -8421462,
            -8421377,
            -8413440,
            -8413355,
            -8413270,
            -8413185,
            -8405248,
            -8405163,
            -8405078,
            -8404993,
            -8397056,
            -8396971,
            -8396886,
            -8396801,
            -8388864,
            -8388779,
            -8388694,
            -8388609,
            -5636096,
            -5636011,
            -5635926,
            -5635841,
            -5628160,
            -5628075,
            -5627990,
            -5627905,
            -5619968,
            -5619883,
            -5619798,
            -5619713,
            -5611776,
            -5611691,
            -5611606,
            -5611521,
            -5603584,
            -5603499,
            -5603414,
            -5603329,
            -5595392,
            -5595307,
            -5595222,
            -5595137,
            -5587200,
            -5587115,
            -5587030,
            -5586945,
            -5579008,
            -5578923,
            -5578838,
            -5578753,
            -5570816,
            -5570731,
            -5570646,
            -5570561,
            -2883584,
            -2883499,
            -2883414,
            -2883329,
            -2875648,
            -2875563,
            -2875478,
            -2875393,
            -2867456,
            -2867371,
            -2867286,
            -2867201,
            -2859264,
            -2859179,
            -2859094,
            -2859009,
            -2851072,
            -2850987,
            -2850902,
            -2850817,
            -2842880,
            -2842795,
            -2842710,
            -2842625,
            -2834688,
            -2834603,
            -2834518,
            -2834433,
            -2826496,
            -2826411,
            -2826326,
            -2826241,
            -2818304,
            -2818219,
            -2818134,
            -2818049,
            -65451,
            -65366,
            -57600,
            -57515,
            -57430,
            -57345,
            -49408,
            -49323,
            -49238,
            -49153,
            -41216,
            -41131,
            -41046,
            -40961,
            -33024,
            -32939,
            -32854,
            -32769,
            -24832,
            -24747,
            -24662,
            -24577,
            -16640,
            -16555,
            -16470,
            -16385,
            -8448,
            -8363,
            -8278,
            -8193,
            -171,
            -86,
            -3355393,
            -13057,
            -13369345,
            -10027009,
            -6684673,
            -3342337,
            -16744704,
            -16744619,
            -16744534,
            -16744449,
            -16736512,
            -16736427,
            -16736342,
            -16736257,
            -16728320,
            -16728235,
            -16728150,
            -16728065,
            -16720128,
            -16720043,
            -16719958,
            -16719873,
            -16711851,
            -16711766,
            -14024704,
            -14024619,
            -14024534,
            -14024449,
            -14016768,
            -14016683,
            -14016598,
            -14016513,
            -14008576,
            -14008491,
            -1040,
            -6250332,
            -8355712,
            -65536,
            -16711936,
            -256,
            -16776961,
            -65281,
            -16711681,
            -1,
        };

        #endregion

        private readonly static Random _random = new Random();

        /// <summary>
        /// Sets in-editor colours to layers
        /// </summary>
        /// <param name="layers">Layers array</param>
        /// <param name="hexGrid">Data source</param>
        public void SetColours(Collection<Layer> layers)
        {
            foreach (var layer in layers)
            {
                SetColours(layer, false);
            }
        }

        /// <summary>
        /// Sets in-editor colours to layers
        /// </summary>
        /// <param name="layer">Layer destination</param>
        /// <param name="hexGrid">Hex data source</param>
        public void SetColours(Layer layer, bool raiseEvent)
        {
            var newColours = (layer.Colours == null || layer.Colours.Length != _mapHexFile.Capacity) ? new int[_mapHexFile.Capacity] : layer.Colours;

            switch (layer.Type)
            {
                case LayerType.Impassable:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetNogoColour(_mapHexFile.HexData[i].IsPassable);
                    }
                    break;
                case LayerType.Roads:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetRoadColour(_mapHexFile.HexData[i].IsRoad);
                    }
                    break;
                case LayerType.TownSlots:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetTownSlotColour(_mapHexFile.HexData[i].TownSlotIndex);
                    }
                    break;
                case LayerType.TownSprawl:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetTownSprawlColour(_mapHexFile.HexData[i].IsTownSprawl);
                    }
                    break;
                case LayerType.Rivers:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetRiverColour(_mapHexFile.HexData[i].IsRiver);
                    }
                    break;
                case LayerType.Bridges:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetBridgeColour(_mapHexFile.HexData[i].IsBridge);
                    }
                    break;
                case LayerType.Beaches:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetBeachColour(_mapHexFile.HexData[i].IsBeach);
                    }
                    break;
                case LayerType.Climates:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = this.GetClimateColour(_mapHexFile.HexData[i].ClimateIndex);
                    }
                    break;
                case LayerType.Attritions:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = this.GetAttritionColour(_mapHexFile.HexData[i].AttritionIndex);
                    }
                    break;
                case LayerType.GroundTypes:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = this.GetTerrainColour(_mapHexFile.HexData[i].GroundTypeIndex);
                    }
                    break;
                case LayerType.Regions:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        var hex = _mapHexFile.HexData[i];
                        newColours[i] = this.GetRegionColour(hex.RegionId);
                    }
                    break;
                case LayerType.TradeRoutes:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        var hex = _mapHexFile.HexData[i];
                        newColours[i] = GetTradeRouteColour(hex.IsTradeRoute);
                    }
                    break;
                case LayerType.Restrictions:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        var hex = _mapHexFile.HexData[i];
                        newColours[i] = GetRestrictionColour(hex.RestrictionLvl);
                    }
                    break;
                case LayerType.RegionBorders:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        var hex = _mapHexFile.HexData[i];
                        newColours[i] = GetRegionBorderColour(hex.IsBorder);
                    }
                    break;
                case LayerType.AreasOfInterest:
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        var hex = _mapHexFile.HexData[i];
                        newColours[i] = this.GetAreaOfInterestColour(hex.InterestIndex);
                    }
                    break;
                case LayerType.CampaignMasks:
                    var maskLayer = (CampaignMaskLayer)layer;
                    for (int i = 0; i < _mapHexFile.Capacity; ++i)
                    {
                        newColours[i] = GetCampaignMaskColour(maskLayer.IsMasked(_mapHexFile.HexData[i]));
                    }
                    break;
                default:
                    LoggerViewModel.Log($"{layer.Name} is not implemented in ColourTable.SetColours()", LogLevel.Warning);
                    break;
            }

            layer.SetColours(newColours, raiseEvent);
        }

        public int GetTerrainColour(sbyte groundTypeIndex)
        {
            var isSea = _mapHexFile.IsSeaGroundType(groundTypeIndex);
            int colourIndex = isSea ? groundTypeIndex - _mapHexFile.LandGroundTypes.Count : groundTypeIndex;
            return _mapHexFile.GetColour(isSea, colourIndex);
        }

        public int GetAttritionColour(sbyte attritionIndex)
        {
            return _mapHexFile.GetColour(false, attritionIndex);
        }

        public int GetClimateColour(sbyte climateIndex)
        {
            return _mapHexFile.GetColour(false, climateIndex);
        }

        public int GetAreaOfInterestColour(sbyte areaOfInterestIndex)
        {
            return _mapHexFile.GetColour(false, areaOfInterestIndex);
        }

        public int GetRegionColour(int regionIndex)
        {
            var isSea = regionIndex >= _mapHexFile.LandRegions.Count;

            if (regionIndex == Hex.INVALID_REGION_INDEX)
            {
                return Zero;
            }

            var coloursContainer = isSea ? _mapHexFile.ColoursSea : _mapHexFile.ColoursLand;
            int colourIndex = isSea ? regionIndex - _mapHexFile.LandRegions.Count : regionIndex;

            colourIndex = MathHelper.Clamp(colourIndex, 0, coloursContainer.Colours.Length - 1);
            return coloursContainer.Colours[colourIndex];
        }

        public static int GetBeachColour(bool isBeach)
        {
            return isBeach ? Beach : Zero;
        }

        /// <summary>
        /// Get nogo colour for in-editor use
        /// </summary>
        /// <param name="nogoType">Nogo type</param>
        /// <returns>Nogo colour for in-editor use</returns>
        public static int GetNogoColour(bool isPassable)
        {
            return isPassable ? Zero : Impassable;
        }

        /// <summary>
        /// Get river colour for in-editor use
        /// </summary>
        /// <returns>River display colour over terrain colours</returns>
        public static int GetRiverColour(bool isRiver)
        {
            return isRiver ? River : Zero;
        }

        /// <summary>
        /// Get road colour for in-editor use
        /// </summary>
        /// <returns>Road display colour over terrain + river colours</returns>
        public static int GetRoadColour(bool isRoad)
        {
            return isRoad ? Road : Zero;
        }

        public static int GetBridgeColour(bool isBridge)
        {
            return isBridge ? Bridge : Zero;
        }

        public static int GetTownSlotColour(sbyte slotIndex)
        {
            if (slotIndex == Hex.INVALID_SLOT_INDEX || slotIndex >= Hex.MAX_SLOTS_COUNT)
            {
                return Zero;
            }

            return MicrosoftColourTable[slotIndex + 1];
        }

        public static int GetTownSprawlColour(bool isSprawl)
        {
            return isSprawl ? MicrosoftColourTable[MicrosoftColourTable.Length - 1] : Zero;
        }

        public static int GetTradeRouteColour(bool isTradeRoute)
        {
            return isTradeRoute ? MicrosoftColourTable[MicrosoftColourTable.Length - 1] : Zero;
        }

        public static int GetRestrictionColour(byte restrictionLevel)
        {
            if (restrictionLevel == 0 || restrictionLevel >= Hex.MAX_RESTRICTIONS_COUNT)
            {
                return Zero;
            }

            return MicrosoftColourTable[restrictionLevel];
        }

        public static int GetRegionEdgeColour(bool isRegionEdge)
        {
            return isRegionEdge ? MicrosoftColourTable[MicrosoftColourTable.Length - 3] : Zero;
        }

        public static int GetRegionBorderColour(bool isBorder)
        {
            return isBorder ? White : Zero;
        }

        public static int GetCampaignMaskColour(bool isMasked)
        {
            return isMasked ? MaskedHex : Zero;
        }

        public static int GenerateRandomColour(int minR = byte.MinValue, int maxR = byte.MaxValue, int minG = byte.MinValue, int maxG = byte.MaxValue, int minB = byte.MinValue, int maxB = byte.MaxValue)
        {
            byte r = (byte)_random.Next(minR, maxR);
            byte g = (byte)_random.Next(minG, maxG);
            byte b = (byte)_random.Next(minB, maxB);

            return Utility.ToRgba(r, g, b);
        }

        public static int[] GenerateRandomColours(int count, int minR = byte.MinValue, int maxR = byte.MaxValue, int minG = byte.MinValue, int maxG = byte.MaxValue, int minB = byte.MinValue, int maxB = byte.MaxValue)
        {
            var colours = new int[count];

            for (int i = 0; i < count; ++i)
            {
                if (i == 0)
                {
                    colours[i] = -12566464; // R: 64, G: 64, B: 64, A: 255
                }
                else
                {
                    colours[i] = GenerateRandomColour(minR, maxR, minG, maxG, minB, maxB);
                }
            }

            return colours;
        }
    }
}
