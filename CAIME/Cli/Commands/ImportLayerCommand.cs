using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CAIME.Classes.Importers;

namespace CAIME
{
    internal sealed class ImportLayerCommand : LayerFileCommand
    {
        private const string FileOption = "--file";
        private const string AsOption   = "--as";
        private const string DirOption  = "--dir";

        private sealed class LayerFile
        {
            public string    Path;
            public byte[]    Data;
            public LayerType Layer;
        }

        public override string Name    => "import-layer";
        public override string Summary => "Import layers from .hex_layer files, like Tools > Import > Layer data.";
        public override bool ModifiesMap => true;

        public override string[] Usage => new[]
        {
            "--map <path> --file <layer-file> [--file <layer-file> ...]",
            "--map <path> --file <layer-file> --as <layer>",
            "--map <path> --dir <folder>",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.List(FileOption, "-f").Value(AsOption).Value(DirOption, "-d");
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            var fileCount = args.Values(FileOption).Count;

            if (fileCount == 0 && args.Has(DirOption) == false)
            {
                CliConsole.Error($"Nothing to import. Pass '{FileOption} <layer-file>' or '{DirOption} <folder>'.");
                return false;
            }

            if (args.Has(AsOption) && (fileCount != 1 || args.Has(DirOption)))
            {
                CliConsole.Error($"'{AsOption}' names the layer for one file, so it needs exactly one '{FileOption}' and no '{DirOption}'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (TryCollectPaths(args, out var paths) == false)
            {
                return CliConsole.ExitUsageError;
            }

            LayerType? forcedLayer = null;
            if (args.Has(AsOption))
            {
                if (session.TryGetLayer(args.Value(AsOption), out var asLayer) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                forcedLayer = asLayer;
            }

            var layerFiles = new List<LayerFile>();
            foreach (var path in paths)
            {
                if (TryReadLayerFile(session, path, forcedLayer, out var layerFile) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                layerFiles.Add(layerFile);
            }

            var duplicate = layerFiles.GroupBy(f => f.Layer).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                CliConsole.Error($"More than one file would import into the {CliNames.Layer(duplicate.Key)} layer: " +
                                 string.Join(", ", duplicate.Select(f => f.Path)));
                return CliConsole.ExitUsageError;
            }

            foreach (var layerFile in layerFiles)
            {
                CliConsole.Info($"Importing {Path.GetFileName(layerFile.Path)} into the {CliNames.Layer(layerFile.Layer)} layer.");
                LayerImporter.Import(session.Project, layerFile.Data, layerFile.Layer);
            }

            return CliConsole.ExitSuccess;
        }

        private static bool TryCollectPaths(CliArguments args, out List<string> paths)
        {
            paths = new List<string>();

            foreach (var file in args.Values(FileOption))
            {
                if (CliPaths.TryGetFullPath(file, "layer file", out var fullPath) == false)
                {
                    return false;
                }

                if (File.Exists(fullPath) == false)
                {
                    CliConsole.Error($"Layer file not found: '{fullPath}'.");
                    return false;
                }

                paths.Add(fullPath);
            }

            if (args.Has(DirOption))
            {
                if (CliPaths.TryGetFullPath(args.Value(DirOption), "folder", out var folder) == false)
                {
                    return false;
                }

                if (Directory.Exists(folder) == false)
                {
                    CliConsole.Error($"Folder not found: '{folder}'.");
                    return false;
                }

                var found = Directory.GetFiles(folder, "*" + LayerFileExtension).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                if (found.Count == 0)
                {
                    CliConsole.Error($"No {LayerFileExtension} files in '{folder}'.");
                    return false;
                }

                paths.AddRange(found);
            }

            return true;
        }

        private static bool TryReadLayerFile(CliSession session, string path, LayerType? forcedLayer, out LayerFile layerFile)
        {
            layerFile = new LayerFile { Path = path };

            try
            {
                layerFile.Data = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                CliConsole.Error($"Could not read '{path}': {ex.Message}");
                return false;
            }

            var headerLayer = LayerImporter.ReadLayerType(layerFile.Data);
            if (forcedLayer == null && (headerLayer == null || Enum.IsDefined(typeof(LayerType), headerLayer.Value) == false || headerLayer == LayerType.Count))
            {
                CliConsole.Error($"'{path}' is not a layer file CAIME exported - its header names no known layer.");
                return false;
            }

            layerFile.Layer = forcedLayer ?? headerLayer.Value;
            if (CheckLayerFileLayer(session, layerFile.Layer) == false)
            {
                return false;
            }

            var expectedLength = LayerImporter.ExpectedFileLength(session.Map, layerFile.Layer);
            if (layerFile.Data.Length != expectedLength)
            {
                CliConsole.Error($"'{path}' is {layerFile.Data.Length} bytes, but a {CliNames.Layer(layerFile.Layer)} layer file for this " +
                                 $"{session.Map.MapWidth} x {session.Map.MapHeight} map must be {expectedLength}. " +
                                 "Was it exported from a map of a different size, or for a different layer?");
                return false;
            }

            return true;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--file, -f <layer-file>", "A .hex_layer file to import. Its header says which layer\nit belongs to. Repeat to import several layers at once.");
            Option(help, "--as <layer>", "Import a single --file into this layer instead of the\none its header names.");
            Option(help, "--dir, -d <folder>", "Import every .hex_layer file in this folder.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --file layer_regions.hex_layer");
            help.AppendLine("-m map.hex --file layer_roads.hex_layer --file layer_rivers.hex_layer");
            help.AppendLine("-m map.hex --dir \"C:\\exports\\old_map\" --output map_v2.hex");
        }
    }
}
