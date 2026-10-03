using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class ExportLayerCommand : LayerFileCommand
    {
        private const string DirOption    = "--dir";
        private const string FormatOption = "--format";
        private const string LayerOption  = "--layer";
        private const string AllOption    = "--all";

        private static readonly Dictionary<string, LayerExportMode[]> Formats = new Dictionary<string, LayerExportMode[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"]  = new[] { LayerExportMode.ToImage },
            ["binary"] = new[] { LayerExportMode.ToBinary },
            ["both"]   = new[] { LayerExportMode.ToImage, LayerExportMode.ToBinary },
        };

        public override string Name    => "export-layer";
        public override string Summary => "Export layers to images or .hex_layer files, like Tools > Export > Layer data.";

        public override string[] Usage => new[]
        {
            "--map <path> --dir <folder> (--all | --layer <layer> [--layer <layer> ...]) [--format image|binary|both]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.Value(DirOption, "-d").Value(FormatOption, "-f").List(LayerOption, "-l").Flag(AllOption);
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            if (args.Has(DirOption) == false)
            {
                CliConsole.Error($"Missing required option '{DirOption} <folder>'.");
                return false;
            }

            if (args.Has(FormatOption) && Formats.ContainsKey(args.Value(FormatOption)) == false)
            {
                CliConsole.Error($"Unknown format '{args.Value(FormatOption)}'. Use image, binary or both.");
                return false;
            }

            if (args.Has(AllOption) == args.Has(LayerOption))
            {
                CliConsole.Error($"Pass either '{AllOption}' or one or more '{LayerOption} <layer>'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (TryGetLayers(session, args, out var layers) == false ||
                CliPaths.TryGetFullPath(args.Value(DirOption), "folder", out var folder) == false)
            {
                return CliConsole.ExitUsageError;
            }

            var modes = Formats[args.Value(FormatOption) ?? "image"];
            var exportPath = folder.TrimEnd('\\', '/') + @"\";
            Directory.CreateDirectory(exportPath);

            foreach (var layer in layers)
            {
                foreach (var mode in modes)
                {
                    LayerToImageExporter.Export(mode, exportPath, session.Project, layer);
                }
            }

            CliConsole.Info($"Exported {layers.Count} layer(s) to {exportPath}");
            return CliConsole.ExitSuccess;
        }

        private static bool TryGetLayers(CliSession session, CliArguments args, out List<LayerType> layers)
        {
            if (args.Has(AllOption))
            {
                layers = LayerFileLayersIn(session).ToList();
                return true;
            }

            layers = new List<LayerType>();
            foreach (var name in args.Values(LayerOption))
            {
                if (CliNames.TryParseLayer(name, out var layer) == false)
                {
                    CliConsole.Error($"Unknown layer '{name}'. Layers that can be exported: {CliNames.LayerList(LayerFileLayersIn(session))}.");
                    return false;
                }

                if (CheckLayerFileLayer(session, layer) == false)
                {
                    return false;
                }

                if (layers.Contains(layer) == false)
                {
                    layers.Add(layer);
                }
            }

            return true;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--dir, -d <folder>", "Folder to write the files to. Created if missing. Required.");
            Option(help, "--layer, -l <layer>", "Layer to export. Repeat for several layers.\nOne of: " + CliNames.LayerList(LayerFileLayers) + ".");
            Option(help, "--all", "Export every layer this map has that can be exported.");
            Option(help, "--format, -f <format>", "image  - a PNG per layer (layer_<name>.png). Default.\n" +
                                                  "binary - a .hex_layer file per layer, for import-layer.\n" +
                                                  "both   - one of each.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --dir exports --all");
            help.AppendLine("-m map.hex --dir exports --layer regions --layer roads --format binary");
        }
    }
}
