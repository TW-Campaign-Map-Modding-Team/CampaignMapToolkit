using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class FillSwatchesCommand : MapCommand
    {
        private const string LayerOption  = "--layer";
        private const string AllOption    = "--all";
        private const string KeyOption    = "--key";
        private const string DryRunOption = "--dry-run";

        private static readonly LayerType[] FillableLayers =
        {
            LayerType.Regions,
            LayerType.GroundTypes,
            LayerType.Climates,
            LayerType.Attritions,
            LayerType.AreasOfInterest,
        };

        private sealed class DatabaseEntry
        {
            public LayerType Layer;
            public string    Key;
            public bool      IsSea;
        }

        public override string Name    => "fill-swatches";
        public override string Summary => "Create a swatch for every database entry the map is missing.";
        public override bool ModifiesMap => true;

        public override string[] Usage => new[]
        {
            "--map <path> (--all | --layer <layer> [--layer <layer> ...]) [--key <key> ...] [--dry-run]",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.List(LayerOption, "-l").Flag(AllOption).List(KeyOption, "-k").Flag(DryRunOption);
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            if (args.Has(AllOption) == args.Has(LayerOption))
            {
                CliConsole.Error($"Pass either '{AllOption}' or one or more '{LayerOption} <layer>'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (TryGetLayers(session, args, out var layers) == false)
            {
                return CliConsole.ExitUsageError;
            }

            var database = session.Project.Database;
            if (database.DataSet.Tables.Count == 0)
            {
                CliConsole.Error($"The {session.Project.Game} database isn't loaded, so there is nothing to fill from. " +
                                 $"Check the Assembly Kit path: config get assembly-kit-path --game {CliNames.Game(session.Project.Game)}");
                return CliConsole.ExitProcessingError;
            }

            var entries = layers.SelectMany(layer => DatabaseEntries(database, layer)).ToList();

            var keys = args.Values(KeyOption);
            if (keys.Count > 0)
            {
                var unknown = keys.FirstOrDefault(key => entries.Any(e => e.Key == key) == false);
                if (unknown != null)
                {
                    CliConsole.Error($"'{unknown}' is not in the database for the {CliNames.LayerList(layers)} layer(s) of this map.");
                    return CliConsole.ExitUsageError;
                }

                entries = entries.Where(e => keys.Contains(e.Key)).ToList();
            }

            var missing = entries.Where(e => ExistingKeys(session.Map, e.Layer).Contains(e.Key) == false).ToList();
            if (missing.Count == 0)
            {
                CliConsole.Info("The map already has a swatch for every database entry asked for.");
                return CliConsole.ExitSuccess;
            }

            var dryRun = args.Has(DryRunOption);
            foreach (var entry in missing)
            {
                var description = $"{CliNames.Layer(entry.Layer)}: {entry.Key}{(entry.IsSea ? " (sea)" : string.Empty)}";

                if (dryRun)
                {
                    CliConsole.Info($"  would create {description}");
                    continue;
                }

                if (Create(session.Project.MapHexEditor, entry) == false)
                {
                    CliConsole.Error($"Could not create {description}. See the messages above for details.");
                    return CliConsole.ExitProcessingError;
                }

                CliConsole.Info($"  created {description}");
            }

            session.RefreshSwatches();

            var verb = dryRun ? "Would create" : "Created";
            CliConsole.Info($"{verb} {missing.Count} swatch(es).");
            return CliConsole.ExitSuccess;
        }

        private static bool TryGetLayers(CliSession session, CliArguments args, out List<LayerType> layers)
        {
            layers = new List<LayerType>();

            if (args.Has(AllOption))
            {
                layers.AddRange(FillableLayers.Where(session.Layers.Contains));
                return true;
            }

            foreach (var name in args.Values(LayerOption))
            {
                if (session.TryGetLayer(name, out var layer) == false)
                {
                    return false;
                }

                if (FillableLayers.Contains(layer) == false)
                {
                    CliConsole.Error($"The {CliNames.Layer(layer)} layer's swatches are fixed, not read from the database. " +
                                     $"Layers that can be filled: {CliNames.LayerList(FillableLayers.Where(session.Layers.Contains))}.");
                    return false;
                }

                if (layers.Contains(layer) == false)
                {
                    layers.Add(layer);
                }
            }

            return true;
        }

        private static IEnumerable<DatabaseEntry> DatabaseEntries(DatabaseViewModel database, LayerType layer)
        {
            switch (layer)
            {
                case LayerType.Regions:
                    return database.CachedRegions.Select(r => new DatabaseEntry { Layer = layer, Key = r.Key, IsSea = r.IsSea });
                case LayerType.GroundTypes:
                    return database.CachedGroundTypes.Select(g => new DatabaseEntry { Layer = layer, Key = g.Key, IsSea = g.IsSea });
                case LayerType.Climates:
                    return database.CachedClimates.Select(c => new DatabaseEntry { Layer = layer, Key = c.Key });
                case LayerType.Attritions:
                    return database.CachedAttritions.Select(a => new DatabaseEntry { Layer = layer, Key = a.Key });
                case LayerType.AreasOfInterest:
                    return database.CachedAreasOfInterest.Select(a => new DatabaseEntry { Layer = layer, Key = a.Key });
            }

            return Enumerable.Empty<DatabaseEntry>();
        }

        private static HashSet<string> ExistingKeys(MapHexFile map, LayerType layer)
        {
            switch (layer)
            {
                case LayerType.Regions:         return new HashSet<string>(map.LandRegions.Concat(map.SeaRegions));
                case LayerType.GroundTypes:     return new HashSet<string>(map.LandGroundTypes.Concat(map.SeaGroundTypes));
                case LayerType.Climates:        return new HashSet<string>(map.Climates);
                case LayerType.Attritions:      return new HashSet<string>(map.Attritions);
                case LayerType.AreasOfInterest: return new HashSet<string>(map.AreasOfInterest);
            }

            return new HashSet<string>();
        }

        private static bool Create(MapHexEditor editor, DatabaseEntry entry)
        {
            switch (entry.Layer)
            {
                case LayerType.Regions:         return editor.CreateNewRegion(entry.Key, entry.IsSea)      != Hex.INVALID_REGION_INDEX;
                case LayerType.GroundTypes:     return editor.CreateNewGroundType(entry.Key, entry.IsSea)  != Hex.INVALID_GROUND_TYPE_INDEX;
                case LayerType.Climates:        return editor.CreateNewClimate(entry.Key)                  != Hex.INVALID_CLIMATE_INDEX;
                case LayerType.Attritions:      return editor.CreateNewAttrition(entry.Key)                != Hex.INVALID_ATTRITION_INDEX;
                case LayerType.AreasOfInterest: return editor.CreateNewAreaOfInterest(entry.Key)           != Hex.INVALID_AREA_OF_INT_INDEX;
            }

            return false;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--layer, -l <layer>", "Layer to fill. Repeatable. One of:\n" + CliNames.LayerList(FillableLayers) + ".");
            Option(help, "--all", "Fill every one of those layers this map has.");
            Option(help, "--key, -k <key>", "Only create the swatch for this database key.\nRepeatable. Default: every missing key.");
            Option(help, "--dry-run", "List what would be created without changing the map.");
            help.AppendLine();
            help.AppendLine("  Regions are read for this campaign map only; ground types, climates and");
            help.AppendLine("  attritions are game-wide. Needs the game's database (the Assembly Kit, or");
            help.AppendLine("  RPFM when that is the database source). Swatches the map already has are");
            help.AppendLine("  left alone.");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --all --dry-run");
            help.AppendLine("-m map.hex --layer regions");
            help.AppendLine("-m map.hex --layer ground-types --key forest --key sea_ocean");
        }
    }
}
