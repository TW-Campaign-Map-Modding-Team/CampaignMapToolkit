using System.Collections.Generic;
using System.Linq;

namespace CAIME
{
    internal abstract class LayerFileCommand : MapCommand
    {
        protected const string LayerFileExtension = ".hex_layer";

        protected static readonly LayerType[] LayerFileLayers =
        {
            LayerType.GroundTypes,
            LayerType.Rivers,
            LayerType.Climates,
            LayerType.Attritions,
            LayerType.Regions,
            LayerType.RegionBorders,
            LayerType.Beaches,
            LayerType.Bridges,
            LayerType.TownSprawl,
            LayerType.TownSlots,
            LayerType.Roads,
            LayerType.TradeRoutes,
            LayerType.Impassable,
        };

        protected static IEnumerable<LayerType> LayerFileLayersIn(CliSession session)
        {
            return LayerFileLayers.Where(session.Layers.Contains);
        }

        protected static bool CheckLayerFileLayer(CliSession session, LayerType layer)
        {
            if (session.Layers.Contains(layer) == false)
            {
                CliConsole.Error($"{session.Project.Game} maps have no {CliNames.Layer(layer)} layer.");
                return false;
            }

            if (LayerFileLayers.Contains(layer) == false)
            {
                CliConsole.Error($"The {CliNames.Layer(layer)} layer can't be imported or exported. " +
                                 $"Layers that can: {CliNames.LayerList(LayerFileLayersIn(session))}.");
                return false;
            }

            return true;
        }
    }
}
