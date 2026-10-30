using System.Collections.Generic;
using System.Text;

namespace CAIME
{
    internal sealed class PaintCommand : PaintingCommand
    {
        public override string Name    => "paint";
        public override string Summary => "Paint hexes with a swatch, like the Brush tool.";

        public override string[] Usage => new[]
        {
            "--map <path> --layer <layer> [--swatch <name-or-id>] --hex <x,y> [--hex <x,y> ...] [--brush-size <n>]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            DeclareLayerAndSwatch(options);
            DeclareHexes(options);
            options.Value(BrushSizeOption, "-b");
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            return RequireLayer(args) && TryGetBrushSize(args, out _);
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (session.TryGetLayer(args.Value(LayerOption), out var layer) == false ||
                session.TryGetSwatch(layer, args.Value(SwatchOption), out var swatch) == false ||
                session.TryGetHexes(args, out var hexes) == false)
            {
                return CliConsole.ExitUsageError;
            }

            TryGetBrushSize(args, out var brushSize);

            var painted = new List<int>();
            foreach (var hex in hexes)
            {
                painted.AddRange(session.Painter.PaintBrush(hex, brushSize, swatch));
            }

            ReportPainted("Painted", layer, swatch, painted);
            return CliConsole.ExitSuccess;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            DescribeLayerAndSwatch(help);
            DescribeHexes(help, "Hex to centre the brush on.");
            Option(help, "--brush-size, -b <n>", "Brush size: 1 paints a single hex, 2 adds the ring\naround it, and so on. Default 1.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --layer regions --swatch my_region --hex 120,45 --hex 121,45");
            help.AppendLine("-m map.hex --layer roads --hexes-file road_hexes.txt");
            help.AppendLine("-m map.hex --layer ground-types --swatch forest --hex 300,200 --brush-size 5");
        }
    }
}
