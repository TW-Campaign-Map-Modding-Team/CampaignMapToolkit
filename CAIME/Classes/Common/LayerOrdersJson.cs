using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    /// <summary>
    /// Converts the per-game layers stack orders kept in preferences.json, stored as
    /// <c>{ "Warhammer3": ["Impassable", "Roads", ...] }</c> with the topmost layer first
    /// </summary>
    public static class LayerOrdersJson
    {
        public static JObject ToJson(IReadOnlyDictionary<GameTemplate, List<LayerType>> orders)
        {
            var json = new JObject();
            foreach (var order in orders)
            {
                json[order.Key.ToString()] = new JArray(order.Value.Select(layer => layer.ToString()));
            }

            return json;
        }

        // Hand-edited or older files may name games or layers this build doesn't know; those entries
        // are dropped rather than failing the whole preferences load.
        public static Dictionary<GameTemplate, List<LayerType>> FromJson(JObject json)
        {
            var orders = new Dictionary<GameTemplate, List<LayerType>>();
            foreach (var property in json.Properties())
            {
                var layerNames = property.Value as JArray;
                if (layerNames == null || TryParseName(property.Name, out GameTemplate game) == false ||
                    game == GameTemplate.Invalid || game == GameTemplate.Count)
                {
                    continue;
                }

                var order = new List<LayerType>();
                foreach (var layerName in layerNames)
                {
                    if (TryParseName(layerName.ToString(), out LayerType layer) && layer != LayerType.Count && order.Contains(layer) == false)
                    {
                        order.Add(layer);
                    }
                }

                orders[game] = order;
            }

            return orders;
        }

        // Enum.TryParse alone also accepts numeric strings and undefined values.
        private static bool TryParseName<TEnum>(string name, out TEnum value) where TEnum : struct
        {
            return Enum.TryParse(name, out value) && Enum.IsDefined(typeof(TEnum), value) && char.IsLetter(name.FirstOrDefault());
        }
    }
}
