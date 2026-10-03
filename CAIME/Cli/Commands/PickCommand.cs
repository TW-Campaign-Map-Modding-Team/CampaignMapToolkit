using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class PickCommand : MapCommand
    {
        private const string LayerOption = "--layer";

        public override string Name    => "pick";
        public override string Summary => "Show which swatch each layer holds at a hex, like the Color Picker tool.";

        public override string[] Usage => new[]
        {
            "--map <path> --hex <x,y> [--hex <x,y> ...] [--layer <layer> ...]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.List(LayerOption, "-l").List(CliSession.HexOption).List(CliSession.HexesFileOption);
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            var layers = new List<LayerType>();
            foreach (var name in args.Values(LayerOption))
            {
                if (session.TryGetLayer(name, out var layer) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                layers.Add(layer);
            }

            if (layers.Count == 0)
            {
                layers.AddRange(session.Layers);
            }

            if (session.TryGetHexes(args, out var hexes) == false)
            {
                return CliConsole.ExitUsageError;
            }

            var nameWidth = layers.Max(layer => CliNames.Layer(layer).Length);
            foreach (var hex in hexes)
            {
                CliConsole.Info($"Hex {hex.Q},{hex.R}:");
                foreach (var layer in layers)
                {
                    CliConsole.Info($"  {CliNames.Layer(layer).PadRight(nameWidth)}  {DescribeSwatchAt(session, layer, hex)}");
                }
            }

            return CliConsole.ExitSuccess;
        }

        public static string DescribeSwatchAt(CliSession session, LayerType layer, Hex hex)
        {
            var swatch = session.Swatches.GetSwatchForHex(layer, hex);
            if (swatch == null)
            {
                return "-  (none)";
            }

            var id = session.Swatches.Swatches[layer].IndexOf(swatch);
            return $"{id}  {swatch.Name}";
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--hex <x,y>", "Hex to inspect. Repeat for several hexes.");
            Option(help, "--hexes-file <file>", "Read more hexes from a text file, as for paint.");
            Option(help, "--layer, -l <layer>", "Only report this layer. Repeat for several layers.\nDefault: every layer in the map.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --hex 120,45");
            help.AppendLine("-m map.hex --hex 120,45 --hex 121,45 --layer regions --layer ground-types");
        }
    }
}
