using System.Collections.Generic;
using System.Linq;
using System.Text;
using CAIME.Painters;

namespace CAIME
{
    internal sealed class FillCommand : PaintingCommand
    {
        private const string SourceLayerOption = "--source-layer";

        private static readonly LayerType[] FillSourceLayers = CliNames.AllLayers
            .Where(layer => HexShapes.TryGetFloodFillMatch(layer, out _))
            .ToArray();

        public override string Name    => "fill";
        public override string Summary => "Flood fill a connected area with a swatch, like the Flood Fill tool.";

        public override string[] Usage => new[]
        {
            "--map <path> --layer <layer> [--swatch <name-or-id>] --hex <x,y> [--hex <x,y> ...] [--source-layer <layer>]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            DeclareLayerAndSwatch(options);
            DeclareHexes(options);
            options.Value(SourceLayerOption);
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            return RequireLayer(args);
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (session.TryGetLayer(args.Value(LayerOption), out var layer) == false ||
                session.TryGetSwatch(layer, args.Value(SwatchOption), out var swatch) == false ||
                TryGetSourceLayer(session, args, layer, out var sourceLayer) == false ||
                session.TryGetHexes(args, out var seeds) == false)
            {
                return CliConsole.ExitUsageError;
            }

            HexShapes.TryGetFloodFillMatch(sourceLayer, out var match);

            var painted = new List<int>();
            foreach (var seed in seeds)
            {
                painted.AddRange(session.Painter.FloodFill(seed, swatch, match));
            }

            ReportPainted("Filled", layer, swatch, painted);
            return CliConsole.ExitSuccess;
        }

        private static bool TryGetSourceLayer(CliSession session, CliArguments args, LayerType layer, out LayerType sourceLayer)
        {
            sourceLayer = layer;

            if (args.Has(SourceLayerOption) && session.TryGetLayer(args.Value(SourceLayerOption), out sourceLayer) == false)
            {
                return false;
            }

            if (FillSourceLayers.Contains(sourceLayer) == false)
            {
                CliConsole.Error($"Flood fill cannot use the {CliNames.Layer(sourceLayer)} layer to find the area to fill. " +
                                 $"Pass {SourceLayerOption} with one of: {CliNames.LayerList(FillSourceLayers.Where(session.Layers.Contains))}.");
                return false;
            }

            return true;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            DescribeLayerAndSwatch(help);
            DescribeHexes(help, "Hex to start the fill from.");
            Option(help, "--source-layer <layer>", "Layer that decides the area: the fill spreads to\n" +
                                                   "neighbours with the same value on this layer.\n" +
                                                   "Defaults to --layer. One of:\n" +
                                                   CliNames.LayerList(FillSourceLayers) + ".");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --layer climates --swatch climate_temperate --hex 200,150");
            help.AppendLine("-m map.hex --layer attritions --swatch 2 --hex 200,150 --source-layer regions");
        }
    }
}
