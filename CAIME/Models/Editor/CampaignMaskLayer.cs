namespace CAIME
{
    /// <summary>
    /// The campaign masks layer. It shows one of the map's masks at a time, and painting it masks or
    /// unmasks that mask's hexes. <see cref="Mask"/> is null when the map has no masks.
    /// </summary>
    public class CampaignMaskLayer : Layer
    {
        private MapHexFile mapHexFile;

        public CampaignMask         Mask            { get; private set; }
        public CampaignMaskSwatch   MaskSwatch      { get; private set; }
        public CampaignMaskSwatch   UnmaskSwatch    { get; private set; }

        public CampaignMaskLayer() : base(LayerType.CampaignMasks)
        {
        }

        public void ShowMask(MapHexFile mapHexFile, CampaignMask mask)
        {
            this.mapHexFile = mapHexFile;
            Mask            = mask;
            MaskSwatch      = mask == null ? null : new CampaignMaskSwatch(mapHexFile, mask, isMasked: true);
            UnmaskSwatch    = mask == null ? null : new CampaignMaskSwatch(mapHexFile, mask, isMasked: false);
        }

        public bool IsMasked(Hex hex)
        {
            return Mask != null && Mask.IsMasked(hex.Q, hex.R, mapHexFile.MapWidth);
        }

        /// <summary>
        /// The shown mask's colour: undoing a stroke painted on another mask changes that mask, not
        /// what this layer shows.
        /// </summary>
        public override int ColourAfterApplying(Swatch swatch, int hexIndex)
        {
            return ColourTable.GetCampaignMaskColour(IsMasked(mapHexFile.HexData[hexIndex]));
        }
    }
}
