using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME
{
    public class RenameAreaOfInterestEvent : RoutedEventArgs
    {
        public string OldAreaOfInterestName { get; private set; }
        public string NewAreaOfInterestName { get; private set; }
        public sbyte  OldAreaOfInterestIndex { get; private set; }
        public sbyte  NewAreaOfInterestIndex { get; private set; }

        public RenameAreaOfInterestEvent(string oldAreaOfInterestName, string newAreaOfInterestName, sbyte oldAreaOfInterestIndex, sbyte newAreaOfInterestIndex)
        {
            OldAreaOfInterestName = oldAreaOfInterestName;
            NewAreaOfInterestName = newAreaOfInterestName;
            OldAreaOfInterestIndex = oldAreaOfInterestIndex;
            NewAreaOfInterestIndex = newAreaOfInterestIndex;
        }
    }
    public class CreateAreaOfInterestEvent : RoutedEventArgs
    {
        public string   NewAreaOfInterestName  { get; private set; }
        public sbyte    NewAreaOfInterestIndex { get; private set; }

        public CreateAreaOfInterestEvent(string newAreaOfInterestName, sbyte newAreaOfInterestIndex)
        {
            NewAreaOfInterestName  = newAreaOfInterestName;
            NewAreaOfInterestIndex = newAreaOfInterestIndex;
        }
    }
    public class RemoveAreaOfInterestEvent : RoutedEventArgs
    {
        public string AreaOfInterestName  { get; private set; }
        public sbyte  AreaOfInterestIndex { get; private set; }

        public RemoveAreaOfInterestEvent(string areaOfInterestName, sbyte areaOfInterestIndex)
        {
            AreaOfInterestName  = areaOfInterestName;
            AreaOfInterestIndex = areaOfInterestIndex;
        }
    }

    public delegate void RenameAreaOfInterestHandler(object sender, RenameAreaOfInterestEvent e);
    public delegate void CreateAreaOfInterestHandler(object sender, CreateAreaOfInterestEvent e);
    public delegate void RemoveAreaOfInterestHandler(object sender, RemoveAreaOfInterestEvent e);

    public partial class MapHexEditor
    {
        public event RenameAreaOfInterestHandler OnAreaOfInterestRenamed;
        public event CreateAreaOfInterestHandler OnAreaOfInterestCreated;
        public event RemoveAreaOfInterestHandler OnAreaOfInterestRemoved;

        public sbyte CreateNewAreaOfInterest(string name)
        {
            var areaOfInterestIndex = MapHexFile.AreasOfInterest.IndexOf(name);

            if (areaOfInterestIndex != Hex.INVALID_AREA_OF_INT_INDEX)
            {
                return Hex.INVALID_AREA_OF_INT_INDEX;
            }

            sbyte newAreaOfInterestIndex;

            MapHexFile.AreasOfInterest.Add(name);

            this.SortAreasOfInterest();

            WarnIfPastFileLimit(MapHexFile.AreasOfInterest.Count, MapHexFile.AREA_OF_INTEREST_INDEX_BITS, "areas of interest");

            newAreaOfInterestIndex = (sbyte)MapHexFile.AreasOfInterest.IndexOf(name);

            this.UpdateAreaOfInterestIndices(newAreaOfInterestIndex, ChangeOperation.Create);
            OnAreaOfInterestCreated?.Invoke(this, new CreateAreaOfInterestEvent(name, newAreaOfInterestIndex));

            MapHexFile.SetDirty();
            return newAreaOfInterestIndex;
        }

        public bool DeleteAreaOfInterest(string name)
        {
            var areaOfInterestIndex = (sbyte)MapHexFile.AreasOfInterest.IndexOf(name);

            if (areaOfInterestIndex != Hex.INVALID_AREA_OF_INT_INDEX)
            {
                if (MapHexFile.AreasOfInterest.Remove(name) == false)
                {
                    return false;
                }

                this.UpdateAreaOfInterestIndices(areaOfInterestIndex, ChangeOperation.Delete);
                OnAreaOfInterestRemoved?.Invoke(this, new RemoveAreaOfInterestEvent(name, areaOfInterestIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool DeleteAreaOfInterest(sbyte areaOfInterestIndex)
        {
            if (areaOfInterestIndex >= MapHexFile.AreasOfInterest.Count || areaOfInterestIndex == Hex.INVALID_AREA_OF_INT_INDEX)
            {
                return false;
            }

            var areaOfInterestName = MapHexFile.AreasOfInterest[areaOfInterestIndex];
            MapHexFile.AreasOfInterest.RemoveAt(areaOfInterestIndex);

            this.UpdateAreaOfInterestIndices(areaOfInterestIndex, ChangeOperation.Delete);
            OnAreaOfInterestRemoved?.Invoke(this, new RemoveAreaOfInterestEvent(areaOfInterestName, areaOfInterestIndex));

            MapHexFile.SetDirty();
            return true;
        }

        public bool RenameAreaOfInterest(string oldName, string newName)
        {
            sbyte areaOfInterestIndex = (sbyte)MapHexFile.AreasOfInterest.IndexOf(oldName);

            if (areaOfInterestIndex != Hex.INVALID_AREA_OF_INT_INDEX)
            {
                MapHexFile.AreasOfInterest[areaOfInterestIndex] = newName;

                this.SortAreasOfInterest();

                var newAreaOfInterestIndex = (sbyte)MapHexFile.AreasOfInterest.IndexOf(newName);
                this.UpdateAreaOfInterestIndices(newAreaOfInterestIndex, ChangeOperation.Rename, areaOfInterestIndex);
                OnAreaOfInterestRenamed?.Invoke(this, new RenameAreaOfInterestEvent(oldName, newName, areaOfInterestIndex, newAreaOfInterestIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool RenameAreaOfInterest(sbyte areaOfInterestIndex, string newName)
        {
            if (areaOfInterestIndex == Hex.INVALID_AREA_OF_INT_INDEX || areaOfInterestIndex >= MapHexFile.AreasOfInterest.Count)
            {
                return false;
            }

            string oldName;
            sbyte newAreaOfInterestIndex;

            oldName = MapHexFile.AreasOfInterest[areaOfInterestIndex];

            MapHexFile.AreasOfInterest[areaOfInterestIndex] = newName;

            this.SortAreasOfInterest();

            newAreaOfInterestIndex = (sbyte)MapHexFile.AreasOfInterest.IndexOf(newName);

            this.UpdateAreaOfInterestIndices(newAreaOfInterestIndex, ChangeOperation.Rename, areaOfInterestIndex);
            OnAreaOfInterestRenamed?.Invoke(this, new RenameAreaOfInterestEvent(oldName, newName,areaOfInterestIndex, newAreaOfInterestIndex));

            MapHexFile.SetDirty();
            return true;
        }

        private void UpdateAreaOfInterestIndices(sbyte areaOfInterestIndex, ChangeOperation op, sbyte oldAreaOfInterestIndex = Hex.INVALID_AREA_OF_INT_INDEX)
        {
            if (op == ChangeOperation.Rename && areaOfInterestIndex == oldAreaOfInterestIndex)
            {
                // No actions required
                return;
            }

            for (int hexIndex = 0; hexIndex < MapHexFile.Capacity; ++hexIndex)
            {
                var hex = MapHexFile.HexData[hexIndex];

                if (op == ChangeOperation.Delete)
                {
                    if (hex.InterestIndex == areaOfInterestIndex)
                    {
                        hex.InterestIndex = Hex.INVALID_AREA_OF_INT_INDEX;
                    }
                    else
                    if (hex.InterestIndex > areaOfInterestIndex)
                    {
                        hex.InterestIndex -= 1;
                    }
                }
                else
                if (op == ChangeOperation.Create)
                {
                    if (hex.InterestIndex >= areaOfInterestIndex)
                    {
                        hex.InterestIndex += 1;
                    }
                }
                else
                if (op == ChangeOperation.Rename)
                {
                    if (hex.InterestIndex == oldAreaOfInterestIndex)
                    {
                        hex.InterestIndex = areaOfInterestIndex;
                    }
                    else
                    if (hex.InterestIndex >= oldAreaOfInterestIndex && hex.InterestIndex <= areaOfInterestIndex)
                    {
                        hex.InterestIndex -= 1;
                    }
                    else
                    if (hex.InterestIndex >= areaOfInterestIndex && hex.InterestIndex <= oldAreaOfInterestIndex)
                    {
                        hex.InterestIndex += 1;
                    }
                }
            }
        }
    
        private void SortAreasOfInterest()
        {
             MapHexFile.AreasOfInterest.Sort(StringComparer.Ordinal);
        }
    }
}
