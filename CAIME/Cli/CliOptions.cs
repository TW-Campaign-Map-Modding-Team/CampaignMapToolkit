using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CAIME
{
    internal sealed class CliOptions
    {
        private enum OptionKind
        {
            Flag,
            Value,
            List,
        }

        private sealed class OptionSpec
        {
            public string     Name;
            public OptionKind Kind;
        }

        private readonly Dictionary<string, OptionSpec> _specsByAlias = new Dictionary<string, OptionSpec>(StringComparer.OrdinalIgnoreCase);
        private int _maxPositionals;

        public CliOptions Flag(string name, params string[] aliases)
        {
            return Declare(name, OptionKind.Flag, aliases);
        }

        public CliOptions Value(string name, params string[] aliases)
        {
            return Declare(name, OptionKind.Value, aliases);
        }

        public CliOptions List(string name, params string[] aliases)
        {
            return Declare(name, OptionKind.List, aliases);
        }

        public CliOptions Positionals(int maxCount = int.MaxValue)
        {
            _maxPositionals = maxCount;
            return this;
        }

        public bool TryParse(IReadOnlyList<string> args, out CliArguments parsed)
        {
            parsed = new CliArguments();

            for (int i = 0; i < args.Count; i++)
            {
                var arg = args[i];

                if (_specsByAlias.TryGetValue(arg, out var spec) == false)
                {
                    if (CliConsole.IsOption(arg) && LooksLikeNumber(arg) == false)
                    {
                        CliConsole.Error($"Unknown option '{arg}'.");
                        return false;
                    }

                    if (parsed.PositionalList.Count >= _maxPositionals)
                    {
                        CliConsole.Error($"Unexpected argument '{arg}'.");
                        return false;
                    }

                    parsed.PositionalList.Add(arg);
                    continue;
                }

                if (spec.Kind == OptionKind.Flag)
                {
                    parsed.FlagSet.Add(spec.Name);
                    continue;
                }

                if (i + 1 >= args.Count || IsOptionValueMissing(args[i + 1]))
                {
                    CliConsole.Error($"Option '{arg}' requires a value.");
                    return false;
                }

                var value = args[++i];

                if (parsed.ValuesByName.TryGetValue(spec.Name, out var values) == false)
                {
                    values = new List<string>();
                    parsed.ValuesByName[spec.Name] = values;
                }
                else if (spec.Kind == OptionKind.Value)
                {
                    CliConsole.Error($"Option '{spec.Name}' can only be given once.");
                    return false;
                }

                values.Add(value);
            }

            return true;
        }

        private CliOptions Declare(string name, OptionKind kind, string[] aliases)
        {
            var spec = new OptionSpec { Name = name, Kind = kind };

            foreach (var alias in new[] { name }.Concat(aliases))
            {
                _specsByAlias[alias] = spec;
            }

            return this;
        }

        private bool IsOptionValueMissing(string candidate)
        {
            return _specsByAlias.ContainsKey(candidate)
                || CliConsole.IsHelpFlag(candidate)
                || candidate.StartsWith("--", StringComparison.Ordinal);
        }

        private static bool LooksLikeNumber(string arg)
        {
            return double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }
    }

    internal sealed class CliArguments
    {
        internal readonly Dictionary<string, List<string>> ValuesByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> FlagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<string> PositionalList = new List<string>();

        public IReadOnlyList<string> Positionals => PositionalList;

        public bool Has(string name)
        {
            return FlagSet.Contains(name) || ValuesByName.ContainsKey(name);
        }

        public string Value(string name)
        {
            return ValuesByName.TryGetValue(name, out var values) ? values[0] : null;
        }

        public IReadOnlyList<string> Values(string name)
        {
            return ValuesByName.TryGetValue(name, out var values) ? (IReadOnlyList<string>)values : Array.Empty<string>();
        }
    }
}
