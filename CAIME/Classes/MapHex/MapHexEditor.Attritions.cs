using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME
{
    public class RenameAttritionEvent : RoutedEventArgs
    {
        public string OldAttritionName { get; private set; }
        public string NewAttritionName { get; private set; }
        public sbyte OldAttritionIndex { get; private set; }
        public sbyte NewAttritionIndex { get; private set; }

        public RenameAttritionEvent(string oldAttritionName, string newAttritionName, sbyte oldAttritionIndex, sbyte newAttritionIndex)
        {
            OldAttritionName = oldAttritionName;
            NewAttritionName = newAttritionName;
            OldAttritionIndex = oldAttritionIndex;
            NewAttritionIndex = newAttritionIndex;
        }
    }
    public class CreateAttritionEvent : RoutedEventArgs
    {
        public string NewAttritionName { get; private set; }
        public sbyte NewAttritionIndex { get; private set; }

        public CreateAttritionEvent(string newAttritionName, sbyte newAttritionIndex)
        {
            NewAttritionName = newAttritionName;
            NewAttritionIndex = newAttritionIndex;
        }
    }
    public class RemoveAttritionEvent : RoutedEventArgs
    {
        public string AttritionName { get; private set; }
        public sbyte AttritionIndex { get; private set; }

        public RemoveAttritionEvent(string attritionName, sbyte attritionIndex)
        {
            AttritionName = attritionName;
            AttritionIndex = attritionIndex;
        }
    }

    public delegate void RenameAttritionHandler(object sender, RenameAttritionEvent e);
    public delegate void CreateAttritionHandler(object sender, CreateAttritionEvent e);
    public delegate void RemoveAttritionHandler(object sender, RemoveAttritionEvent e);

    public partial class MapHexEditor
    {
        public event RenameAttritionHandler OnAttritionRenamed;
        public event CreateAttritionHandler OnAttritionCreated;
        public event RemoveAttritionHandler OnAttritionRemoved;

        public sbyte CreateNewAttrition(string name)
        {
            var attritionIndex = MapHexFile.Attritions.IndexOf(name);
            if (attritionIndex != Hex.INVALID_ATTRITION_INDEX)
            {
                // Already exists
                return Hex.INVALID_ATTRITION_INDEX;
            }

            MapHexFile.Attritions.Add(name);

            var keys = new List<string>();
            keys.AddRange(MapHexFile.Attritions);

            MapHexFile.Attritions.Sort(StringComparer.Ordinal);

            WarnIfPastFileLimit(MapHexFile.Attritions.Count, MapHexFile.ATTRITION_INDEX_BITS, "attritions");

            var newAttritionIndex = (sbyte)MapHexFile.Attritions.IndexOf(name);
            this.UpdateAttritionIndices(newAttritionIndex, ChangeOperation.Create);
            OnAttritionCreated?.Invoke(this, new CreateAttritionEvent(name, newAttritionIndex));

            MapHexFile.SetDirty();
            return newAttritionIndex;
        }

        public bool DeleteAttrition(string name)
        {
            var attritionIndex = (sbyte)MapHexFile.Attritions.IndexOf(name);
            if (attritionIndex != Hex.INVALID_ATTRITION_INDEX)
            {
                if (MapHexFile.Attritions.Remove(name) == false)
                {
                    return false;
                }

                this.UpdateAttritionIndices(attritionIndex, ChangeOperation.Delete);
                OnAttritionRemoved?.Invoke(this, new RemoveAttritionEvent(name, attritionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool DeleteAttrition(sbyte attritionIndex)
        {
            if (attritionIndex >= MapHexFile.Attritions.Count || attritionIndex == Hex.INVALID_ATTRITION_INDEX)
            {
                return false;
            }

            var attritionName = MapHexFile.Attritions[attritionIndex];
            MapHexFile.Attritions.RemoveAt(attritionIndex);

            this.UpdateAttritionIndices(attritionIndex, ChangeOperation.Delete);
            OnAttritionRemoved?.Invoke(this, new RemoveAttritionEvent(attritionName, attritionIndex));

            MapHexFile.SetDirty();
            return true;
        }

        public bool RenameAttrition(string oldName, string newName)
        {
            var attritionIndex = (sbyte)MapHexFile.Attritions.IndexOf(oldName);
            if (attritionIndex != Hex.INVALID_ATTRITION_INDEX)
            {
                MapHexFile.Attritions[attritionIndex] = newName;

                var keys = new List<string>();
                keys.AddRange(MapHexFile.Attritions);

                MapHexFile.Attritions.Sort(StringComparer.Ordinal);

                var newAttritionIndex = (sbyte)MapHexFile.Attritions.IndexOf(newName);
                this.UpdateAttritionIndices(newAttritionIndex, ChangeOperation.Rename, attritionIndex);
                OnAttritionRenamed?.Invoke(this, new RenameAttritionEvent(oldName, newName, attritionIndex, newAttritionIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool RenameAttrition(sbyte attritionIndex, string newName)
        {
            if (attritionIndex == Hex.INVALID_ATTRITION_INDEX || attritionIndex >= MapHexFile.Attritions.Count)
            {
                return false;
            }

            var oldName = MapHexFile.Attritions[attritionIndex];

            MapHexFile.Attritions[attritionIndex] = newName;

            var keys = new List<string>();
            keys.AddRange(MapHexFile.Attritions);

            MapHexFile.Attritions.Sort(StringComparer.Ordinal);

            var newAttritionIndex = (sbyte)MapHexFile.Attritions.IndexOf(newName);

            this.UpdateAttritionIndices(newAttritionIndex, ChangeOperation.Rename, attritionIndex);
            OnAttritionRenamed?.Invoke(this, new RenameAttritionEvent(oldName, newName, attritionIndex, newAttritionIndex));

            MapHexFile.SetDirty();
            return true;
        }

        private void UpdateAttritionIndices(sbyte attritionIndex, ChangeOperation op, sbyte oldAttritionIndex = Hex.INVALID_ATTRITION_INDEX)
        {
            if (op == ChangeOperation.Rename && attritionIndex == oldAttritionIndex)
            {
                // No actions required
                return;
            }

            for (int hexIndex = 0; hexIndex < MapHexFile.Capacity; ++hexIndex)
            {
                var hex = MapHexFile.HexData[hexIndex];

                if (op == ChangeOperation.Delete)
                {
                    if (hex.AttritionIndex == attritionIndex)
                    {
                        hex.AttritionIndex = Hex.INVALID_ATTRITION_INDEX;
                    }
                    else
                    if (hex.AttritionIndex > attritionIndex)
                    {
                        hex.AttritionIndex -= 1;
                    }
                }
                else
                if (op == ChangeOperation.Create)
                {
                    if (hex.AttritionIndex >= attritionIndex)
                    {
                        hex.AttritionIndex += 1;
                    }
                }
                else
                if (op == ChangeOperation.Rename)
                {
                    if (hex.AttritionIndex == oldAttritionIndex)
                    {
                        hex.AttritionIndex = attritionIndex;
                    }
                    else
                    if (hex.AttritionIndex >= oldAttritionIndex && hex.AttritionIndex <= attritionIndex)
                    {
                        hex.AttritionIndex -= 1;
                    }
                    else
                    if (hex.AttritionIndex >= attritionIndex && hex.AttritionIndex <= oldAttritionIndex)
                    {
                        hex.AttritionIndex += 1;
                    }
                }
            }
        }
    }
}
