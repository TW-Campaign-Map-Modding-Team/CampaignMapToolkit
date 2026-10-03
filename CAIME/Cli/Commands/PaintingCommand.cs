using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal abstract class PaintingCommand : MapCommand
    {
        protected const string LayerOption     = "--layer";
        protected const string SwatchOption    = "--swatch";
        protected const string BrushSizeOption = "--brush-size";

        public override bool ModifiesMap => true;

        protected static void DeclareLayerAndSwatch(CliOptions options)
        {
            options.Value(LayerOption, "-l").Value(SwatchOption, "-s");
        }

        protected static void DeclareHexes(CliOptions options)
        {
            options.List(CliSession.HexOption).List(CliSession.HexesFileOption);
        }

        protected static bool RequireLayer(CliArguments args)
        {
            if (args.Has(LayerOption))
            {
                return true;
            }

            CliConsole.Error($"Missing required option '{LayerOption} <layer>'.");
            return false;
        }

        protected static bool TryGetBrushSize(CliArguments args, out int brushSize)
        {
            brushSize = 1;

            var value = args.Value(BrushSizeOption);
            if (value == null)
            {
                return true;
            }

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out brushSize) && brushSize >= 1)
            {
                return true;
            }

            CliConsole.Error($"'{BrushSizeOption}' must be a whole number of 1 or more, not '{value}'.");
            return false;
        }

        protected static void DescribeLayerAndSwatch(StringBuilder help)
        {
            Option(help, "--layer, -l <layer>", "Layer to paint on, e.g. regions, ground-types, roads. Required.");
            Option(help, "--swatch, -s <name-or-id>", "Swatch to paint with, by name or by id. May be left out\non layers with a single swatch (roads, rivers, ...).\nList them with: query --map <map> --swatches");
        }

        protected static void DescribeHexes(StringBuilder help, string purpose)
        {
            Option(help, "--hex <x,y>", $"{purpose} Repeat for several hexes. x and y are\nthe X and Y the editor's status bar shows.");
            Option(help, "--hexes-file <file>", "Read more hexes from a text file: x,y pairs separated\nby spaces or new lines; '#' starts a comment.");
        }

        protected static void ReportPainted(string verb, LayerType layer, Swatch swatch, IEnumerable<int> painted)
        {
            var count = painted.Distinct().Count();
            CliConsole.Info($"{verb} {count} hex(es) on the {CliNames.Layer(layer)} layer with '{swatch.Name}'.");
        }
    }
}
