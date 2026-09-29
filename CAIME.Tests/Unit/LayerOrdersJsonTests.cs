using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Covers how the editor's per-game layers stack orders are written to and read back from
    /// preferences.json. The file outlives any one build and can be hand-edited, so reading it must
    /// drop what it doesn't recognise rather than fail the whole preferences load.
    /// </summary>
    [TestClass]
    public class LayerOrdersJsonTests
    {
        [TestMethod]
        public void RoundTrip_PreservesEachGamesOrder()
        {
            var orders = new Dictionary<GameTemplate, List<LayerType>>
            {
                [GameTemplate.Warhammer3] = new List<LayerType> { LayerType.GroundTypes, LayerType.Impassable, LayerType.Regions },
                [GameTemplate.Rome2]      = new List<LayerType> { LayerType.TradeRoutes, LayerType.Roads },
            };

            var json = JObject.Parse(LayerOrdersJson.ToJson(orders).ToString());
            var result = LayerOrdersJson.FromJson(json);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(orders[GameTemplate.Warhammer3], result[GameTemplate.Warhammer3]);
            CollectionAssert.AreEqual(orders[GameTemplate.Rome2], result[GameTemplate.Rome2]);
        }

        [TestMethod]
        public void ToJson_WritesGameAndLayerNames()
        {
            var orders = new Dictionary<GameTemplate, List<LayerType>>
            {
                [GameTemplate.Warhammer3] = new List<LayerType> { LayerType.GroundTypes, LayerType.Impassable },
            };

            var json = LayerOrdersJson.ToJson(orders);

            CollectionAssert.AreEqual(new[] { "GroundTypes", "Impassable" }, json["Warhammer3"].Values<string>().ToArray());
        }

        [TestMethod]
        public void FromJson_DropsUnknownGamesAndLayers_AndDuplicates()
        {
            var json = JObject.Parse(@"{
                ""Warhammer3"":   [""GroundTypes"", ""NotALayer"", ""3"", ""Count"", ""Impassable"", ""GroundTypes""],
                ""NotAGame"":     [""Roads""],
                ""4"":            [""Roads""],
                ""Invalid"":      [""Roads""],
                ""Attila"":       ""not an array""
            }");

            var result = LayerOrdersJson.FromJson(json);

            Assert.AreEqual(1, result.Count, "only Warhammer3 survives");
            CollectionAssert.AreEqual(new[] { LayerType.GroundTypes, LayerType.Impassable }, result[GameTemplate.Warhammer3]);
        }
    }
}
