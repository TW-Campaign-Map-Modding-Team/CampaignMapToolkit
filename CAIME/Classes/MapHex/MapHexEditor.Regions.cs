using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME
{
    public class RenameRegionEvent : RoutedEventArgs
    {
        public string   OldRegionName   { get; private set; }
        public string   NewRegionName   { get; private set; }
        public int      OldRegionIndex  { get; private set; }
        public int      NewRegionIndex  { get; private set; }

        public RenameRegionEvent(string oldRegionName, string newRegionName, int oldRegionIndex, int newRegionIndex)
        {
            OldRegionName   = oldRegionName;
            NewRegionName   = newRegionName;
            OldRegionIndex  = oldRegionIndex;
            NewRegionIndex  = newRegionIndex;
        }
    }

    public class CreateRegionEvent : RoutedEventArgs
    {
        public string   NewRegionName   { get; private set; }
        public int      NewRegionIndex  { get; private set; }
        public bool     IsSea           { get; private set; }

        public CreateRegionEvent(string newRegionName, int newRegionIndex, bool isSea)
        {
            NewRegionName   = newRegionName;
            NewRegionIndex  = newRegionIndex;
            IsSea           = isSea;
        }
    }

    public class RemoveRegionEvent : RoutedEventArgs
    {
        public string   RegionName      { get; private set; }
        public int      RegionIndex     { get; private set; }

        public RemoveRegionEvent(string regionName, int regionIndex)
        {
            RegionName  = regionName;
            RegionIndex = regionIndex;
        }
    }

    public delegate void RenameRegionHandler(object sender, RenameRegionEvent e);
    public delegate void CreateRegionHandler(object sender, CreateRegionEvent e);
    public delegate void RemoveRegionHandler(object sender, RemoveRegionEvent e);

    public partial class MapHexEditor
    {
        public event RenameRegionHandler OnRegionRenamed;
        public event CreateRegionHandler OnRegionCreated;
        public event RemoveRegionHandler OnRegionRemoved;

        public int CreateNewRegion(string name, bool isSea)
        {
            var landIndex   = MapHexFile.LandRegions.IndexOf(name);
            var seaIndex    = MapHexFile.SeaRegions.IndexOf(name);

            if (landIndex != Hex.INVALID_REGION_INDEX)
            {
                return Hex.INVALID_REGION_INDEX;
            }

            if (seaIndex != Hex.INVALID_REGION_INDEX)
            {
                return Hex.INVALID_REGION_INDEX;
            }

            var list            = isSea ? MapHexFile.SeaRegions    : MapHexFile.LandRegions;
            var colourContainer = isSea ? MapHexFile.ColoursSea     : MapHexFile.ColoursLand;

            list.Add(name);
            list.Sort(StringComparer.Ordinal);

            // The combined index in the [land..., sea...] flat space used by hex.RegionId.
            var newRegionIndex = isSea
                ? list.IndexOf(name) + MapHexFile.LandRegions.Count
                : list.IndexOf(name);

            // The position within the land-only or sea-only colour array.
            var colourArrayIndex = isSea
                ? list.IndexOf(name)
                : newRegionIndex;

            // Regenerate until we find a colour that is not already in the array. The attempt cap
            // stops the loop spinning forever once the palette is effectively saturated.
            const int MAX_COLOUR_ATTEMPTS = 1000;

            bool inserted = false;
            for (int attempt = 0; attempt < MAX_COLOUR_ATTEMPTS && !inserted; ++attempt)
            {
                var newColour = isSea
                    ? MapHexFile.GenerateSeaColours(1)[0]
                    : MapHexFile.GenerateLandColours(1)[0];

                inserted = colourContainer.InsertColour(newColour, colourArrayIndex);
            }

            if (!inserted)
            {
                //The name was added to the region list above; undo that so the lists and the
                //colour array stay the same length.
                list.Remove(name);

                LoggerViewModel.Log(
                    $"Could not find an unused colour for region '{name}' after {MAX_COLOUR_ATTEMPTS} attempts. " +
                    "The region was not created.",
                    LogLevel.Error);
                return Hex.INVALID_REGION_INDEX;
            }

            WarnIfPastFileLimit(MapHexFile.LandRegions.Count + MapHexFile.SeaRegions.Count, MapHexFile.REGION_INDEX_BITS, "regions");

            this.UpdateRegionIndices(newRegionIndex, ChangeOperation.Create);
            OnRegionCreated?.Invoke(this, new CreateRegionEvent(name, newRegionIndex, isSea));

            MapHexFile.SetDirty();
            return newRegionIndex;
        }

        public bool DeleteRegion(string name)
        {
            int landRegionIndex = MapHexFile.LandRegions.IndexOf(name);
            int seaRegionIndex  = MapHexFile.SeaRegions.IndexOf(name);

            if (landRegionIndex != Hex.INVALID_REGION_INDEX)
            {
                if (MapHexFile.LandRegions.Remove(name) == false)
                {
                    return false;
                }

                MapHexFile.ColoursLand.DeleteColour(landRegionIndex);

                this.UpdateRegionIndices(landRegionIndex, ChangeOperation.Delete);
                OnRegionRemoved?.Invoke(this, new RemoveRegionEvent(name, landRegionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            if (seaRegionIndex != Hex.INVALID_REGION_INDEX)
            {
                if (MapHexFile.SeaRegions.Remove(name) == false)
                {
                    return false;
                }

                MapHexFile.ColoursSea.DeleteColour(seaRegionIndex);

                seaRegionIndex += MapHexFile.LandRegions.Count;
                this.UpdateRegionIndices(seaRegionIndex, ChangeOperation.Delete);
                OnRegionRemoved?.Invoke(this, new RemoveRegionEvent(name, seaRegionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool RenameRegion(string oldName, string newName)
        {
            int landRegionIndex = MapHexFile.LandRegions.IndexOf(oldName);
            int seaRegionIndex  = MapHexFile.SeaRegions.IndexOf(oldName);

            if (landRegionIndex != Hex.INVALID_REGION_INDEX)
            {
                MapHexFile.LandRegions[landRegionIndex] = newName;
                MapHexFile.LandRegions.Sort(StringComparer.Ordinal);

                var newRegionIndex = MapHexFile.LandRegions.IndexOf(newName);
                this.UpdateRegionIndices(newRegionIndex, ChangeOperation.Rename, landRegionIndex);
                OnRegionRenamed?.Invoke(this, new RenameRegionEvent(oldName, newName, landRegionIndex, newRegionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            if (seaRegionIndex != Hex.INVALID_REGION_INDEX)
            {
                MapHexFile.SeaRegions[seaRegionIndex] = newName;
                MapHexFile.SeaRegions.Sort(StringComparer.Ordinal);

                var newRegionIndex = MapHexFile.SeaRegions.IndexOf(newName) + MapHexFile.LandRegions.Count;
                this.UpdateRegionIndices(newRegionIndex, ChangeOperation.Rename, seaRegionIndex + MapHexFile.LandRegions.Count);
                OnRegionRenamed?.Invoke(this, new RenameRegionEvent(oldName, newName, seaRegionIndex, newRegionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        private void UpdateRegionIndices(int regionIndex, ChangeOperation op, int oldRegionIndex = Hex.INVALID_REGION_INDEX)
        {
            if (op == ChangeOperation.Rename && regionIndex == oldRegionIndex)
            {
                // No actions required
                return;
            }

            for (int hexIndex = 0; hexIndex < MapHexFile.Capacity; ++hexIndex)
            {
                var hex = MapHexFile.HexData[hexIndex];

                if (op == ChangeOperation.Delete)
                {
                    if (hex.RegionId == regionIndex)
                    {
                        hex.RegionId = Hex.INVALID_REGION_INDEX;
                    }
                    else
                    if (hex.RegionId > regionIndex)
                    {
                        hex.RegionId -= 1;
                    }
                }
                else
                if (op == ChangeOperation.Create)
                {
                    if (hex.RegionId >= regionIndex)
                    {
                        hex.RegionId += 1;
                    }
                }
                else
                if (op == ChangeOperation.Rename)
                {
                    if (hex.RegionId == oldRegionIndex)
                    {
                        hex.RegionId = regionIndex;
                    }
                    else
                    if (hex.RegionId >= oldRegionIndex && hex.RegionId <= regionIndex)
                    {
                        hex.RegionId -= 1;
                    }
                    else
                    if (hex.RegionId >= regionIndex && hex.RegionId <= oldRegionIndex)
                    {
                        hex.RegionId += 1;
                    }
                }
            }

            // Region ids changed in bulk - region border masks must be rebuilt on save/export.
            MapHexFile.RegionMasksNeedRecalculation = true;
        }
    }
}
