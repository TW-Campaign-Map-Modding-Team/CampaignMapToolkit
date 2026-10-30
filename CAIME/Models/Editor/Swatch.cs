using System;
using System.Windows;

namespace CAIME
{
    public abstract class Swatch : ObservableObject
    {
        public int      Colour  { get; protected set; }
        public string   Name    { get; protected set; }

        public Swatch()
        {}

        public abstract void Apply(Hex hex);

        public void UpdateSwatchName(string newName)
        {
            Name = newName;
            OnPropertyChanged(nameof(Name));
        }
    }

    public class GroundSwatch : Swatch
    {
        public sbyte GroundTypeIndex { get; protected set; }

        public GroundSwatch(string key, sbyte index, int colour) : base()
        {
            Colour          = colour;
            GroundTypeIndex = index;
            Name            = key;
        }

        public override void Apply(Hex hex)
        {
            hex.GroundTypeIndex = GroundTypeIndex;
        }
    }

    public class AttritionSwatch : Swatch
    {
        public sbyte AttritionIndex { get; protected set; }

        public AttritionSwatch(string key, sbyte index, int colour) : base()
        {
            Colour          = colour;
            AttritionIndex  = index;
            Name            = key;
        }

        public override void Apply(Hex hex)
        {
            hex.AttritionIndex = AttritionIndex;
        }
    }

    public class ClimateSwatch : Swatch
    {
        public sbyte ClimateIndex { get; protected set; }

        public ClimateSwatch(string key, sbyte index, int colour) : base()
        {
            Colour          = colour;
            ClimateIndex    = index;
            Name            = key;
        }

        public override void Apply(Hex hex)
        {
            hex.ClimateIndex = ClimateIndex;
        }
    }

    public class AreaOfInterestSwatch : Swatch
    {
        public sbyte AreaOfInterestIndex { get; protected set; }

        public AreaOfInterestSwatch(string key, sbyte index, int colour) : base()
        {
            Colour              = colour;
            AreaOfInterestIndex = index;
            Name                = key;
        }

        public override void Apply(Hex hex)
        {
            hex.InterestIndex = AreaOfInterestIndex;
        }
    }

    public class BeachSwatch : Swatch
    {
        public bool IsBeach { get; protected set; }

        public BeachSwatch(bool isBeach) : base()
        {
            IsBeach = isBeach;
            Name    = isBeach ? "Beach" : "None";
            Colour  = ColourTable.GetBeachColour(isBeach);
        }

        public override void Apply(Hex hex)
        {
            hex.IsBeach = IsBeach;
        }
    }

    public class RiverSwatch : Swatch
    {
        public bool IsRiver { get; protected set; }

        public RiverSwatch(bool isRiver) : base()
        {
            IsRiver = isRiver;
            Name    = isRiver ? "River" : "Remove river";
            Colour  = ColourTable.GetRiverColour(isRiver);
        }

        public override void Apply(Hex hex)
        {
            hex.IsRiver = IsRiver;

            if (IsRiver == false)
            {
                hex.RiverEdgeMask = 0;
            }
            else
            {
                // Stamp paint order so triangle resolution follows the order rivers were painted.
                hex.RiverPaintSeq = ++MapHexFile.RiverPaintCounter;
            }

            // Flag this hex for an incremental mask rebuild on save/export (authored masks
            // elsewhere are preserved).
            MapHexFile.RiverDirtyHexes.Add(hex.Index);
        }
    }

    public class RoadSwatch : Swatch
    {
        public bool IsRoad { get; protected set; }

        public RoadSwatch(bool isRoad) : base()
        {
            IsRoad  = isRoad;
            Name    = isRoad ? "Road" : "Remove road";
            Colour  = ColourTable.GetRoadColour(isRoad);
        }

        public override void Apply(Hex hex)
        {
            hex.IsRoad = IsRoad;

            if (IsRoad == false)
            {
                hex.RoadEdgeMask = 0;
            }
            else
            {
                // Stamp paint order so triangle resolution follows the order roads were painted.
                hex.RoadPaintSeq = ++MapHexFile.RoadPaintCounter;
            }

            // Flag this hex for an incremental mask rebuild on save/export (authored masks
            // elsewhere are preserved).
            MapHexFile.RoadDirtyHexes.Add(hex.Index);
        }
    }

    public class NogoSwatch : Swatch
    {
        public bool IsImpassable { get; protected set; }

        public NogoSwatch(bool isNogo) : base()
        {
            IsImpassable    = isNogo;
            Name            = isNogo ? "Impassable" : "Passable";
            Colour          = ColourTable.GetNogoColour(isNogo == false);
        }

        public override void Apply(Hex hex)
        {
            hex.IsPassable = IsImpassable == false;
        }
    }

    /// <summary>
    /// Masks or unmasks hexes of one campaign mask. The swatch keeps its own mask, so undoing a
    /// stroke changes the mask it was painted on, whichever mask the layer shows by then.
    /// </summary>
    public class CampaignMaskSwatch : Swatch
    {
        private readonly MapHexFile _mapHexFile;

        public CampaignMask Mask     { get; }
        public bool         IsMasked { get; }

        public CampaignMaskSwatch(MapHexFile mapHexFile, CampaignMask mask, bool isMasked) : base()
        {
            _mapHexFile = mapHexFile;
            Mask        = mask;
            IsMasked    = isMasked;
            Name        = isMasked ? "Masked" : "Unmasked";
            Colour      = ColourTable.GetCampaignMaskColour(isMasked);
        }

        public bool Matches(Hex hex)
        {
            return Mask.IsMasked(hex.Q, hex.R, _mapHexFile.MapWidth) == IsMasked;
        }

        public override void Apply(Hex hex)
        {
            Mask.SetMasked(hex.Q, hex.R, _mapHexFile.MapWidth, _mapHexFile.MapHeight, IsMasked);
        }
    }

    public class RegionSwatch : Swatch
    {
        public int RegionIndex { get; protected set; }

        public RegionSwatch(string key, int index, int colour) : base()
        {
            Colour      = colour;
            RegionIndex = index;
            Name        = key;
        }

        public override void Apply(Hex hex)
        {
            hex.RegionId = RegionIndex;

            // Region edge masks depend on RegionId - flag for an incremental rebuild.
            MapHexFile.RegionDirtyHexes.Add(hex.Index);
        }
    }

    public class TownSlotSwatch : Swatch
    {
        public sbyte SlotIndex { get; protected set; }

        public TownSlotSwatch(sbyte slotIndex) : base()
        {
            SlotIndex   = slotIndex;
            Colour      = ColourTable.GetTownSlotColour(slotIndex);

            if (slotIndex == Hex.INVALID_SLOT_INDEX)
            {
                Name    = "Remove slot hex";
            }
            else
            if (slotIndex == Hex.MAIN_SLOT_INDEX)
            {
                Name    = "Main slot";
            }
            else
            if (slotIndex == Hex.PORT_SLOT_INDEX)
            {
                Name    = "Port slot";
            }
            else
            {
                Name    = $"Slot #{slotIndex}";
            }
        }

        public override void Apply(Hex hex)
        {
            hex.TownSlotIndex = SlotIndex;
        }
    }

    public class TownSprawlSwatch : Swatch
    {
        public bool Value { get; protected set; }

        public TownSprawlSwatch(bool isSprawl) : base()
        {
            Value   = isSprawl;
            Colour  = ColourTable.GetTownSprawlColour(isSprawl);
            Name    = isSprawl ? "Sprawl hex" : "None";
        }

        public override void Apply(Hex hex)
        {
            hex.IsTownSprawl = Value;
        }
    }

    public class BridgeSwatch : Swatch
    {
        public bool IsBridge { get; protected set; }

        public BridgeSwatch(bool isBridge) : base()
        {
            IsBridge    = isBridge;
            Colour      = ColourTable.GetBridgeColour(isBridge);
            Name        = isBridge ? "Bridge" : "Remove bridge cell";
        }

        public override void Apply(Hex hex)
        {
            hex.IsBridge = IsBridge;
        }
    }

    public class TradeRouteSwatch : Swatch
    {
        public bool IsTradeRoute { get; protected set; }

        public TradeRouteSwatch(bool isTradeRoute) : base()
        {
            IsTradeRoute    = isTradeRoute;
            Colour          = ColourTable.GetTradeRouteColour(isTradeRoute);
            Name            = isTradeRoute ? "Trade route" : "Remove trade route";
        }

        public override void Apply(Hex hex)
        {
            hex.IsTradeRoute = IsTradeRoute;

            if (IsTradeRoute == false)
            {
                hex.TradeRouteMask = 0;
            }

            // Flag this hex for an incremental mask rebuild on save/export.
            MapHexFile.TradeDirtyHexes.Add(hex.Index);
        }
    }

    public class RestrictionSwatch : Swatch
    {
        public byte RestrictionLevel { get; protected set; }

        public RestrictionSwatch(byte restrictionLevel) : base()
        {
            RestrictionLevel    = restrictionLevel;
            Colour              = ColourTable.GetRestrictionColour(restrictionLevel);
            Name                = $"Restriction level {restrictionLevel}";
        }

        public override void Apply(Hex hex)
        {
            hex.RestrictionLvl = RestrictionLevel;
        }
    }

    public class RegionBorderSwatch : Swatch
    {
        public bool IsBorder { get; protected set; }

        public RegionBorderSwatch(bool isBorder) : base()
        {
            IsBorder    = isBorder;
            Colour      = ColourTable.GetRegionBorderColour(isBorder);
            Name        = isBorder ? "Region border" : "Remove region border";
        }

        public override void Apply(Hex hex)
        {
            hex.IsBorder = IsBorder;

            if (IsBorder == false)
            {
                hex.RegionEdgeMask = 0;
            }

            // Region edge masks depend on IsBorder - flag for an incremental rebuild.
            MapHexFile.RegionDirtyHexes.Add(hex.Index);
        }
    }
}
