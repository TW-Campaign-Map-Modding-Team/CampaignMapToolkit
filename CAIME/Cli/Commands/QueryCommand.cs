using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    internal sealed class QueryCommand : MapCommand
    {
        private const string InfoOption     = "--info";
        private const string NameOption     = "--name";
        private const string GameOption     = "--game";
        private const string SizeOption     = "--size";
        private const string LayersOption   = "--layers";
        private const string SwatchesOption = "--swatches";
        private const string LayerOption    = "--layer";
        private const string JsonOption     = "--json";

        private static readonly string[] SingleValueOptions = { NameOption, GameOption, SizeOption };

        private sealed class HexAttribute
        {
            public string Key;
            public object Value;
            public string Detail;
        }

        protected override bool PrintsDataToStandardOutput => true;

        public override string Name    => "query";
        public override string Summary => "Print information about a map: name, game, size, layers, swatches and hexes.";

        public override string[] Usage => new[]
        {
            "--map <path> [--info] [--name] [--game] [--size] [--layers] [--swatches [--layer <layer> ...]] [--hex <x,y> ...] [--json]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.Flag(InfoOption)
                   .Flag(NameOption)
                   .Flag(GameOption)
                   .Flag(SizeOption)
                   .Flag(LayersOption)
                   .Flag(SwatchesOption)
                   .List(LayerOption, "-l")
                   .List(CliSession.HexOption)
                   .List(CliSession.HexesFileOption)
                   .Flag(JsonOption);
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            if (args.Has(LayerOption) && args.Has(SwatchesOption) == false)
            {
                CliConsole.Error($"'{LayerOption}' picks which layers '{SwatchesOption}' lists, so it needs '{SwatchesOption}'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            var wantsHexes = args.Has(CliSession.HexOption) || args.Has(CliSession.HexesFileOption);
            var showInfo   = args.Has(InfoOption) || (wantsHexes == false && new[] { NameOption, GameOption, SizeOption, LayersOption, SwatchesOption }.Any(args.Has) == false);

            var swatchLayers = new List<LayerType>();
            foreach (var name in args.Values(LayerOption))
            {
                if (session.TryGetLayer(name, out var layer) == false)
                {
                    return CliConsole.ExitUsageError;
                }

                swatchLayers.Add(layer);
            }

            if (swatchLayers.Count == 0)
            {
                swatchLayers.AddRange(session.Layers);
            }

            var hexes = new List<Hex>();
            if (wantsHexes && session.TryGetHexes(args, out hexes) == false)
            {
                return CliConsole.ExitUsageError;
            }

            if (args.Has(JsonOption))
            {
                CliConsole.Info(BuildJson(session, args, showInfo, swatchLayers, hexes).ToString(Formatting.Indented));
                return CliConsole.ExitSuccess;
            }

            var requestedSingleValues = SingleValueOptions.Where(args.Has).ToList();
            var onlyOneValue = requestedSingleValues.Count == 1 && showInfo == false && wantsHexes == false
                            && args.Has(LayersOption) == false && args.Has(SwatchesOption) == false;

            if (onlyOneValue)
            {
                CliConsole.Info(SingleValue(session, requestedSingleValues[0]));
                return CliConsole.ExitSuccess;
            }

            if (showInfo)
            {
                PrintInfo(session);
            }
            else
            {
                foreach (var option in requestedSingleValues)
                {
                    CliConsole.Info($"{option.TrimStart('-')}: {SingleValue(session, option)}");
                }
            }

            if (args.Has(LayersOption))
            {
                PrintLayers(session);
            }

            if (args.Has(SwatchesOption))
            {
                PrintSwatches(session, swatchLayers);
            }

            foreach (var hex in hexes)
            {
                PrintHex(session, hex);
            }

            return CliConsole.ExitSuccess;
        }

        private static string SingleValue(CliSession session, string option)
        {
            switch (option)
            {
                case NameOption: return session.Project.MapName;
                case GameOption: return CliNames.Game(session.Project.Game);
                default:         return $"{session.Map.MapWidth}x{session.Map.MapHeight}";
            }
        }

        private static void PrintInfo(CliSession session)
        {
            var map = session.Map;

            CliConsole.Info($"Map name:  {session.Project.MapName}");
            CliConsole.Info($"Game:      {CliNames.Game(session.Project.Game)}");
            CliConsole.Info($"Map size:  {map.MapWidth} x {map.MapHeight} ({map.Capacity} hexes)");
            CliConsole.Info($"Map file:  {session.Project.FileName}");
        }

        private static void PrintLayers(CliSession session)
        {
            CliConsole.Info(string.Empty);
            CliConsole.Info("Layers (swatch types):");

            var nameWidth = session.Layers.Max(layer => CliNames.Layer(layer).Length);
            foreach (var layer in session.Layers)
            {
                CliConsole.Info($"  {CliNames.Layer(layer).PadRight(nameWidth)}  {session.Swatches.Swatches[layer].Count} swatch(es)");
            }
        }

        private static void PrintSwatches(CliSession session, IEnumerable<LayerType> layers)
        {
            foreach (var layer in layers)
            {
                var swatches = session.Swatches.Swatches[layer];

                CliConsole.Info(string.Empty);
                CliConsole.Info($"{CliNames.Layer(layer)} swatches ({swatches.Count}):");

                var idWidth = (swatches.Count - 1).ToString().Length;
                for (int id = 0; id < swatches.Count; id++)
                {
                    var sea = IsSeaSwatch(session, layer, id) ? "  (sea)" : string.Empty;
                    CliConsole.Info($"  {id.ToString().PadLeft(idWidth)}  {swatches[id].Name}{sea}");
                }
            }
        }

        private static void PrintHex(CliSession session, Hex hex)
        {
            var attributes = DescribeHex(session, hex);
            var keyWidth   = attributes.Max(a => a.Key.Length);

            CliConsole.Info(string.Empty);
            CliConsole.Info($"Hex {hex.Q},{hex.R}:");

            foreach (var attribute in attributes.Skip(2))
            {
                var detail = attribute.Detail == null ? string.Empty : $"  {attribute.Detail}";
                CliConsole.Info($"  {attribute.Key.PadRight(keyWidth)}  {FormatValue(attribute.Value)}{detail}");
            }
        }

        private static string FormatValue(object value)
        {
            return value is bool flag ? (flag ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static List<HexAttribute> DescribeHex(CliSession session, Hex hex)
        {
            hex.UpdateHexType();

            var attributes = new List<HexAttribute>();

            void Add(string key, object value, string detail = null)
            {
                attributes.Add(new HexAttribute { Key = key, Value = value, Detail = detail });
            }

            string SwatchName(LayerType layer)
            {
                return session.Swatches.Swatches.ContainsKey(layer) ? session.Swatches.GetSwatchForHex(layer, hex)?.Name : null;
            }

            string Mask(byte mask)
            {
                return mask == 0 ? null : "0b" + Convert.ToString(mask, 2).PadLeft(HexGridUtility.NEIGHBOURS_COUNT, '0');
            }

            Add("x",                hex.Q);
            Add("y",                hex.R);
            Add("index",            hex.Index);
            Add("type",             hex.HexType.ToString());
            Add("region",           hex.RegionId,        SwatchName(LayerType.Regions));
            Add("groundType",       hex.GroundTypeIndex, SwatchName(LayerType.GroundTypes));
            Add("climate",          hex.ClimateIndex,    SwatchName(LayerType.Climates));
            Add("attrition",        hex.AttritionIndex,  SwatchName(LayerType.Attritions));
            Add("areaOfInterest",   hex.InterestIndex,   SwatchName(LayerType.AreasOfInterest));
            Add("restrictionLevel", hex.RestrictionLvl);
            Add("townSlot",         hex.TownSlotIndex,   SwatchName(LayerType.TownSlots));
            Add("isTownSprawl",     hex.IsTownSprawl);
            Add("isSea",            hex.IsSea);
            Add("isLand",           hex.IsLand);
            Add("isPassable",       hex.IsPassable);
            Add("isImpassable",     hex.IsImpassable);
            Add("isPassableLand",   hex.IsPassableLand);
            Add("isPassableSea",    hex.IsPassableSea);
            Add("isBeach",          hex.IsBeach);
            Add("isCliff",          hex.IsCliff);
            Add("isCoast",          hex.IsCoast);
            Add("isBridge",         hex.IsBridge);
            Add("isBridgeCliff",    hex.IsBridgeCliff);
            Add("isRiver",          hex.IsRiver);
            Add("isRoad",           hex.IsRoad);
            Add("isTradeRoute",     hex.IsTradeRoute);
            Add("isBorder",         hex.IsBorder);
            Add("roadEdgeMask",     hex.RoadEdgeMask,    Mask(hex.RoadEdgeMask));
            Add("riverEdgeMask",    hex.RiverEdgeMask,   Mask(hex.RiverEdgeMask));
            Add("regionEdgeMask",   hex.RegionEdgeMask,  Mask(hex.RegionEdgeMask));
            Add("tradeRouteMask",   hex.TradeRouteMask,  Mask(hex.TradeRouteMask));

            return attributes;
        }

        private static bool IsSeaSwatch(CliSession session, LayerType layer, int id)
        {
            switch (layer)
            {
                case LayerType.Regions:     return id >= session.Map.LandRegions.Count;
                case LayerType.GroundTypes: return id >= session.Map.LandGroundTypes.Count;
                default:                    return false;
            }
        }

        private static JObject BuildJson(CliSession session, CliArguments args, bool showInfo, IEnumerable<LayerType> swatchLayers, IEnumerable<Hex> hexes)
        {
            var json = new JObject();
            var map  = session.Map;

            if (showInfo || args.Has(NameOption))
            {
                json["name"] = session.Project.MapName;
            }

            if (showInfo || args.Has(GameOption))
            {
                json["game"] = CliNames.Game(session.Project.Game);
            }

            if (showInfo || args.Has(SizeOption))
            {
                json["width"]    = map.MapWidth;
                json["height"]   = map.MapHeight;
                json["hexCount"] = map.Capacity;
            }

            if (showInfo)
            {
                json["file"] = session.Project.FileName;
            }

            if (args.Has(LayersOption))
            {
                json["layers"] = new JArray(session.Layers.Select(layer => new JObject
                {
                    ["name"]        = CliNames.Layer(layer),
                    ["swatchCount"] = session.Swatches.Swatches[layer].Count,
                }));
            }

            if (args.Has(SwatchesOption))
            {
                var swatches = new JObject();
                foreach (var layer in swatchLayers)
                {
                    var list = session.Swatches.Swatches[layer];
                    swatches[CliNames.Layer(layer)] = new JArray(list.Select((swatch, id) =>
                    {
                        var entry = new JObject { ["id"] = id, ["name"] = swatch.Name };
                        if (layer == LayerType.Regions || layer == LayerType.GroundTypes)
                        {
                            entry["isSea"] = IsSeaSwatch(session, layer, id);
                        }

                        return entry;
                    }));
                }

                json["swatches"] = swatches;
            }

            var hexArray = new JArray();
            foreach (var hex in hexes)
            {
                var entry = new JObject();
                foreach (var attribute in DescribeHex(session, hex))
                {
                    entry[attribute.Key] = JToken.FromObject(attribute.Value);
                    if (attribute.Detail != null && attribute.Key.EndsWith("Mask", StringComparison.Ordinal) == false)
                    {
                        entry[attribute.Key + "Name"] = attribute.Detail;
                    }
                }

                hexArray.Add(entry);
            }

            if (hexArray.Count > 0)
            {
                json["hexes"] = hexArray;
            }

            return json;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--info", "Map name, game, size and file. The default when nothing\nelse is asked for.");
            Option(help, "--name", "Campaign map name.");
            Option(help, "--game", "Game the map is for.");
            Option(help, "--size", "Map size as <width>x<height>.");
            Option(help, "--layers", "Layers this map has - the swatch types - and how many\nswatches each holds.");
            Option(help, "--swatches", "Every swatch's id and name, per layer. The id is what\npaint, line and fill accept for --swatch.");
            Option(help, "--layer, -l <layer>", "With --swatches, list only this layer. Repeatable.");
            Option(help, "--hex <x,y>", "Every attribute of this hex: type, region, ground type,\nclimate, flags, edge masks... Repeatable.");
            Option(help, "--hexes-file <file>", "Read more hexes from a text file, as for paint.");
            Option(help, "--json", "Print the answer as JSON, for scripts.");
            help.AppendLine();
            help.AppendLine("  Asking for just one of --name, --game or --size prints the bare value.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex");
            help.AppendLine("-m map.hex --name");
            help.AppendLine("-m map.hex --swatches --layer regions");
            help.AppendLine("-m map.hex --hex 120,45 --hex 0,0");
            help.AppendLine("-m map.hex --layers --swatches --json");
        }
    }
}
