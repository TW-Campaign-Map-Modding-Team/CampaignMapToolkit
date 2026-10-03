using System.Collections.Generic;
using System.Text;

namespace CAIME
{
    internal sealed class LineCommand : PaintingCommand
    {
        private const string FromOption = "--from";
        private const string ToOption   = "--to";

        public override string Name    => "line";
        public override string Summary => "Paint a straight hex line between two hexes, like the Line tool.";

        public override string[] Usage => new[]
        {
            "--map <path> --layer <layer> [--swatch <name-or-id>] --from <x,y> --to <x,y> [--to <x,y> ...]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            DeclareLayerAndSwatch(options);
            options.Value(FromOption).List(ToOption);
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            if (RequireLayer(args) == false)
            {
                return false;
            }

            if (args.Has(FromOption) == false || args.Has(ToOption) == false)
            {
                CliConsole.Error($"A line needs '{FromOption} <x,y>' and at least one '{ToOption} <x,y>'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (session.TryGetLayer(args.Value(LayerOption), out var layer) == false ||
                session.TryGetSwatch(layer, args.Value(SwatchOption), out var swatch) == false ||
                session.TryGetHex(args.Value(FromOption), out var from) == false)
            {
                return CliConsole.ExitUsageError;
            }

            var points = new List<Hex> { from };
            foreach (var coordinate in args.Values(ToOption))
            {
                if (session.TryGetHex(coordinate, out var to) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                if (to == points[points.Count - 1])
                {
                    CliConsole.Error($"A line segment must join two different hexes, but {coordinate} repeats the previous point.");
                    return CliConsole.ExitUsageError;
                }

                points.Add(to);
            }

            var painted = new List<int>();
            for (int i = 1; i < points.Count; i++)
            {
                painted.AddRange(session.Painter.PaintLine(points[i - 1], points[i], swatch));
            }

            ReportPainted("Painted", layer, swatch, painted);
            return CliConsole.ExitSuccess;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            DescribeLayerAndSwatch(help);
            Option(help, "--from <x,y>", "Hex the line starts at. Required.");
            Option(help, "--to <x,y>", "Hex the line ends at. Required. Repeat to draw a\npolyline: each --to continues from the previous point.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --layer roads --from 10,10 --to 40,25");
            help.AppendLine("-m map.hex --layer rivers --from 5,5 --to 20,8 --to 32,20 --to 35,40");
        }
    }
}
