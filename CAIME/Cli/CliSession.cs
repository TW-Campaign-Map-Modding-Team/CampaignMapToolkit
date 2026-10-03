using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CAIME.Painters;

namespace CAIME
{
    internal sealed class CliSession : IDisposable
    {
        public const string HexOption       = "--hex";
        public const string HexesFileOption = "--hexes-file";

        private SwatchesViewModel _swatches;
        private MapPainter _painter;

        public ProjectManager ProjectManager { get; private set; }
        public Project Project => ProjectManager.Project;
        public MapHexFile Map => Project.MapHexFile;

        public SwatchesViewModel Swatches
        {
            get
            {
                if (_swatches == null)
                {
                    _swatches = new SwatchesViewModel();
                    _swatches.UpdateSwatchesSet(Project);
                }

                return _swatches;
            }
        }

        public MapPainter Painter => _painter ?? (_painter = new MapPainter(Map));

        public IEnumerable<LayerType> Layers => CliNames.AllLayers.Where(Swatches.Swatches.ContainsKey);

        private CliSession(ProjectManager projectManager)
        {
            ProjectManager = projectManager;
        }

        public static CliSession Open(string mapPath)
        {
            CliConsole.Diagnostic($"Loading map: {mapPath}");

            var projectManager = new ProjectManager();
            if (projectManager.Open(mapPath) == null)
            {
                CliConsole.Error("Failed to load the map. See the messages above for details.");
                return null;
            }

            return new CliSession(projectManager);
        }

        public void RefreshSwatches()
        {
            _swatches = null;
        }

        public bool TryGetLayer(string name, out LayerType layer)
        {
            if (CliNames.TryParseLayer(name, out layer) == false)
            {
                CliConsole.Error($"Unknown layer '{name}'. Layers in this map: {CliNames.LayerList(Layers)}.");
                return false;
            }

            if (Swatches.Swatches.ContainsKey(layer) == false)
            {
                CliConsole.Error($"{Project.Game} maps have no {CliNames.Layer(layer)} layer. Layers in this map: {CliNames.LayerList(Layers)}.");
                return false;
            }

            return true;
        }

        public bool TryGetSwatch(LayerType layer, string nameOrId, out Swatch swatch)
        {
            var swatches = Swatches.Swatches[layer];
            swatch = null;

            if (string.IsNullOrEmpty(nameOrId))
            {
                if (swatches.Count == 1)
                {
                    swatch = swatches[0];
                    return true;
                }

                CliConsole.Error($"The {CliNames.Layer(layer)} layer has {swatches.Count} swatches - choose one with --swatch <name-or-id>.");
                return false;
            }

            swatch = swatches.FirstOrDefault(s => string.Equals(s.Name, nameOrId, StringComparison.Ordinal))
                  ?? swatches.FirstOrDefault(s => string.Equals(s.Name, nameOrId, StringComparison.OrdinalIgnoreCase));

            if (swatch == null && int.TryParse(nameOrId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id < swatches.Count)
            {
                swatch = swatches[id];
            }

            if (swatch == null)
            {
                CliConsole.Error($"The {CliNames.Layer(layer)} layer has no swatch '{nameOrId}'. " +
                                 $"List them with: query --map <map> --swatches --layer {CliNames.Layer(layer)}");
                return false;
            }

            return true;
        }

        public bool TryGetHex(string coordinate, out Hex hex)
        {
            hex = null;

            var parts = (coordinate ?? string.Empty).Split(',');
            if (parts.Length != 2 ||
                int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) == false ||
                int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) == false)
            {
                CliConsole.Error($"'{coordinate}' is not a hex coordinate. Write it as x,y - for example 120,45.");
                return false;
            }

            if (x < 0 || x >= Map.MapWidth || y < 0 || y >= Map.MapHeight)
            {
                CliConsole.Error($"Hex {x},{y} is outside the map, which is {Map.MapWidth} x {Map.MapHeight} " +
                                 $"(x 0-{Map.MapWidth - 1}, y 0-{Map.MapHeight - 1}).");
                return false;
            }

            hex = Map.HexData[HexGridUtility.IndexFromCoords(y, x, (int)Map.MapWidth)];
            return true;
        }

        public bool TryGetHexes(CliArguments args, out List<Hex> hexes)
        {
            hexes = new List<Hex>();

            var coordinates = new List<string>(args.Values(HexOption));
            foreach (var file in args.Values(HexesFileOption))
            {
                if (TryReadCoordinates(file, coordinates) == false)
                {
                    return false;
                }
            }

            if (coordinates.Count == 0)
            {
                CliConsole.Error($"No hexes given. Pass {HexOption} <x,y> or {HexesFileOption} <file>.");
                return false;
            }

            foreach (var coordinate in coordinates)
            {
                if (TryGetHex(coordinate, out var hex) == false)
                {
                    return false;
                }

                hexes.Add(hex);
            }

            return true;
        }

        public bool Save(string outputPath)
        {
            var savePath = Project.ProjectPath;
            var saveName = Project.MapHexFileName;

            if (outputPath != null)
            {
                savePath = Path.GetDirectoryName(outputPath);
                saveName = Path.GetFileNameWithoutExtension(outputPath);
                Directory.CreateDirectory(savePath);
            }

            if (Project.Save(SaveParameters.SaveFlags.All, saveName, savePath) == false)
            {
                CliConsole.Error("Saving the map failed. See the messages above for details.");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            ProjectManager.CloseProject();
        }

        private static bool TryReadCoordinates(string file, List<string> coordinates)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex)
            {
                CliConsole.Error($"Could not read hexes file '{file}': {ex.Message}");
                return false;
            }

            foreach (var line in lines)
            {
                var content = line.Split('#')[0];
                coordinates.AddRange(content.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            return true;
        }
    }
}
