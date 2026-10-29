using System.Linq;

namespace CAIME
{
    public partial class MapHexEditor
    {
        /// <summary>
        /// Adds a campaign mask that masks no hexes yet. Returns null when the name is empty, is
        /// already another mask's, or is not ASCII, which is how map.hex stores the names.
        /// </summary>
        public CampaignMask CreateCampaignMask(string name)
        {
            if (string.IsNullOrEmpty(name) ||
                name.Any(character => character > 127) ||
                MapHexFile.CampaignMasks.Any(existing => existing.Name == name))
            {
                return null;
            }

            var mask = new CampaignMask(name);
            MapHexFile.CampaignMasks.Add(mask);

            MapHexFile.SetDirty();
            return mask;
        }
    }
}
