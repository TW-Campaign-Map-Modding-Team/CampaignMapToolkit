using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class EraseCommand : PaintingCommand
    {
        public override string Name    => "erase";
        public override string Summary => "Clear a layer's value from hexes, like the Eraser tool.";

        public override string[] Usage => new[]
        {
            "--map <path> --layer <layer> --hex <x,y> [--hex <x,y> ...] [--brush-size <n>]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.Value(LayerOption, "-l");
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
                session.TryGetHexes(args, out var hexes) == false)
            {
                return CliConsole.ExitUsageError;
            }

            TryGetBrushSize(args, out var brushSize);

            var eraser  = EraserToolCommand.CLEAR_SWATCHES[layer];
            var erased  = new List<int>();
            foreach (var hex in hexes)
            {
                erased.AddRange(session.Painter.PaintBrush(hex, brushSize, eraser));
            }

            CliConsole.Info($"Erased {erased.Distinct().Count()} hex(es) on the {CliNames.Layer(layer)} layer.");
            return CliConsole.ExitSuccess;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--layer, -l <layer>", "Layer to erase from. Required.");
            DescribeHexes(help, "Hex to centre the eraser on.");
            Option(help, "--brush-size, -b <n>", "Eraser size, as for paint. Default 1.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --layer rivers --hex 80,12 --hex 81,12");
            help.AppendLine("-m map.hex --layer town-sprawl --hex 50,50 --brush-size 3");
        }
    }
}
