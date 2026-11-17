using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME
{
    public class RenameClimateEvent : RoutedEventArgs
    {
        public string OldClimateName { get; private set; }
        public string NewClimateName { get; private set; }
        public sbyte OldClimateIndex { get; private set; }
        public sbyte NewClimateIndex { get; private set; }

        public RenameClimateEvent(string oldClimateName, string newClimateName, sbyte oldClimateIndex, sbyte newClimateIndex)
        {
            OldClimateName = oldClimateName;
            NewClimateName = newClimateName;
            OldClimateIndex = oldClimateIndex;
            NewClimateIndex = newClimateIndex;
        }
    }
    public class CreateClimateEvent : RoutedEventArgs
    {
        public string NewClimateName { get; private set; }
        public sbyte NewClimateIndex { get; private set; }

        public CreateClimateEvent(string newClimateName, sbyte newClimateIndex)
        {
            NewClimateName = newClimateName;
            NewClimateIndex = newClimateIndex;
        }
    }
    public class RemoveClimateEvent : RoutedEventArgs
    {
        public string ClimateName { get; private set; }
        public sbyte ClimateIndex { get; private set; }

        public RemoveClimateEvent(string climateName, sbyte climateIndex)
        {
            ClimateName = climateName;
            ClimateIndex = climateIndex;
        }
    }

    public delegate void RenameClimateHandler(object sender, RenameClimateEvent e);
    public delegate void CreateClimateHandler(object sender, CreateClimateEvent e);
    public delegate void RemoveClimateHandler(object sender, RemoveClimateEvent e);

    public partial class MapHexEditor
    {
        public event RenameClimateHandler OnClimateRenamed;
        public event CreateClimateHandler OnClimateCreated;
        public event RemoveClimateHandler OnClimateRemoved;

        public sbyte CreateNewClimate(string name)
        {
            var climateIndex = MapHexFile.Climates.IndexOf(name);
            if (climateIndex != Hex.INVALID_CLIMATE_INDEX)
            {
                // Already exists
                return Hex.INVALID_CLIMATE_INDEX;
            }

            MapHexFile.Climates.Add(name);

            MapHexFile.Climates.Sort(StringComparer.Ordinal);

            WarnIfPastFileLimit(MapHexFile.Climates.Count, MapHexFile.CLIMATE_INDEX_BITS, "climates");

            var newClimateIndex = (sbyte)MapHexFile.Climates.IndexOf(name);
            this.UpdateClimateIndices(newClimateIndex, ChangeOperation.Create);
            OnClimateCreated?.Invoke(this, new CreateClimateEvent(name, newClimateIndex));

            MapHexFile.SetDirty();
            return newClimateIndex;
        }

        public bool DeleteClimate(string name)
        {
            var climateIndex = (sbyte)MapHexFile.Climates.IndexOf(name);
            if (climateIndex != Hex.INVALID_CLIMATE_INDEX)
            {
                if (MapHexFile.Climates.Remove(name) == false)
                {
                    return false;
                }

                this.UpdateClimateIndices(climateIndex, ChangeOperation.Delete);
                OnClimateRemoved?.Invoke(this, new RemoveClimateEvent(name, climateIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool DeleteClimate(sbyte climateIndex)
        {
            if (climateIndex >= MapHexFile.Climates.Count || climateIndex == Hex.INVALID_CLIMATE_INDEX)
            {
                return false;
            }

            var climateName = MapHexFile.Climates[climateIndex];
            MapHexFile.Climates.RemoveAt(climateIndex);

            this.UpdateClimateIndices(climateIndex, ChangeOperation.Delete);
            OnClimateRemoved?.Invoke(this, new RemoveClimateEvent(climateName, climateIndex));

            MapHexFile.SetDirty();
            return true;
        }

        public bool RenameClimate(string oldName, string newName)
        {
            var climateIndex = (sbyte)MapHexFile.Climates.IndexOf(oldName);
            if (climateIndex != Hex.INVALID_CLIMATE_INDEX)
            {
                MapHexFile.Climates[climateIndex] = newName;

                MapHexFile.Climates.Sort(StringComparer.Ordinal);

                var newClimateIndex = (sbyte)MapHexFile.Climates.IndexOf(newName);
                this.UpdateClimateIndices(newClimateIndex, ChangeOperation.Rename, climateIndex);
                OnClimateRenamed?.Invoke(this, new RenameClimateEvent(oldName, newName, climateIndex, newClimateIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool RenameClimate(sbyte climateIndex, string newName)
        {
            if (climateIndex == Hex.INVALID_CLIMATE_INDEX || climateIndex >= MapHexFile.Climates.Count)
            {
                return false;
            }

            var oldName = MapHexFile.Climates[climateIndex];

            MapHexFile.Climates[climateIndex] = newName;

            MapHexFile.Climates.Sort(StringComparer.Ordinal);

            var newClimateIndex = (sbyte)MapHexFile.Climates.IndexOf(newName);

            this.UpdateClimateIndices(newClimateIndex, ChangeOperation.Rename, climateIndex);
            OnClimateRenamed?.Invoke(this, new RenameClimateEvent(oldName, newName, climateIndex, newClimateIndex));

            MapHexFile.SetDirty();
            return true;
        }

        private void UpdateClimateIndices(sbyte climateIndex, ChangeOperation op, sbyte oldClimateIndex = Hex.INVALID_CLIMATE_INDEX)
        {
            if (op == ChangeOperation.Rename && climateIndex == oldClimateIndex)
            {
                // No actions required
                return;
            }

            for (int hexIndex = 0; hexIndex < MapHexFile.Capacity; ++hexIndex)
            {
                var hex = MapHexFile.HexData[hexIndex];

                if (op == ChangeOperation.Delete)
                {
                    if (hex.ClimateIndex == climateIndex)
                    {
                        hex.ClimateIndex = Hex.INVALID_CLIMATE_INDEX;
                    }
                    else
                    if (hex.ClimateIndex > climateIndex)
                    {
                        hex.ClimateIndex -= 1;
                    }
                }
                else
                if (op == ChangeOperation.Create)
                {
                    if (hex.ClimateIndex >= climateIndex)
                    {
                        hex.ClimateIndex += 1;
                    }
                }
                else
                if (op == ChangeOperation.Rename)
                {
                    if (hex.ClimateIndex == oldClimateIndex)
                    {
                        hex.ClimateIndex = climateIndex;
                    }
                    else
                    if (hex.ClimateIndex >= oldClimateIndex && hex.ClimateIndex <= climateIndex)
                    {
                        hex.ClimateIndex -= 1;
                    }
                    else
                    if (hex.ClimateIndex >= climateIndex && hex.ClimateIndex <= oldClimateIndex)
                    {
                        hex.ClimateIndex += 1;
                    }
                }
            }
        }
    }
}
