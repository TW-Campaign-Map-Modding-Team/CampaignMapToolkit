using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal static class CliNames
    {
        public static IEnumerable<LayerType> AllLayers =>
            Enumerable.Range(0, (int)LayerType.Count).Select(i => (LayerType)i);

        public static IEnumerable<GameTemplate> AllGames =>
            Enumerable.Range(0, (int)GameTemplate.Count).Select(i => (GameTemplate)i);

        public static string Layer(LayerType layer)
        {
            return ToKebabCase(layer.ToString());
        }

        public static string Game(GameTemplate game)
        {
            return ToKebabCase(game.ToString());
        }

        public static bool TryParseLayer(string name, out LayerType layer)
        {
            return TryParse(name, AllLayers, out layer);
        }

        public static bool TryParseGame(string name, out GameTemplate game)
        {
            return TryParse(name, AllGames, out game);
        }

        public static string LayerList(IEnumerable<LayerType> layers)
        {
            return string.Join(", ", layers.Select(Layer));
        }

        private static bool TryParse<TEnum>(string name, IEnumerable<TEnum> candidates, out TEnum value)
        {
            var wanted = Normalise(name);

            foreach (var candidate in candidates)
            {
                if (Normalise(candidate.ToString()) == wanted)
                {
                    value = candidate;
                    return true;
                }
            }

            value = default(TEnum);
            return false;
        }

        private static string Normalise(string name)
        {
            return new string((name ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static string ToKebabCase(string identifier)
        {
            var kebab = new StringBuilder();

            for (int i = 0; i < identifier.Length; i++)
            {
                var c = identifier[i];

                if (c == '_')
                {
                    kebab.Append('-');
                    continue;
                }

                var startsWord = i > 0 && char.IsUpper(c) && identifier[i - 1] != '_';
                if (startsWord)
                {
                    kebab.Append('-');
                }

                kebab.Append(char.ToLowerInvariant(c));
            }

            return kebab.ToString();
        }
    }
}
