using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class CliCommandsTests
    {
        private string _dir;
        private string _mapPath;
        private TextWriter _stdout;
        private TextWriter _stderr;
        private StringWriter _captured;

        [TestInitialize]
        public void Setup()
        {
            _dir     = MapHexHarness.CreateTempDir("caime_cli_commands");
            _mapPath = Path.Combine(_dir, "map.hex");
            File.WriteAllText(_mapPath, "not a real map - every case here is rejected before it is opened");

            _captured = new StringWriter();
            _stdout   = Console.Out;
            _stderr   = Console.Error;
            Console.SetOut(_captured);
            Console.SetError(_captured);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Console.SetOut(_stdout);
            Console.SetError(_stderr);
            _captured?.Dispose();
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Options_ReadValuesListsFlagsAndAliases()
        {
            var options = new CliOptions().Value("--layer", "-l").List("--hex").Flag("--json");

            var parsed = Parse(options, "-l", "regions", "--hex", "1,2", "--hex", "3,4", "--json");

            Assert.AreEqual("regions", parsed.Value("--layer"));
            CollectionAssert.AreEqual(new[] { "1,2", "3,4" }, parsed.Values("--hex").ToArray());
            Assert.IsTrue(parsed.Has("--json"));
            Assert.IsFalse(parsed.Has("--missing"));
        }

        [TestMethod]
        public void Options_ASingleValueOptionGivenTwice_IsRejected()
        {
            var options = new CliOptions().Value("--layer");

            Assert.IsFalse(options.TryParse(new[] { "--layer", "a", "--layer", "b" }, out _));
            StringAssert.Contains(_captured.ToString(), "--layer");
        }

        [TestMethod]
        public void Options_AValueOptionFollowedByAnotherOption_IsRejected()
        {
            var options = new CliOptions().Value("--layer").Flag("--json");

            Assert.IsFalse(options.TryParse(new[] { "--layer", "--json" }, out _));
        }

        [TestMethod]
        public void Options_UnknownOptionsAndUnexpectedArguments_AreRejected()
        {
            Assert.IsFalse(new CliOptions().Flag("--json").TryParse(new[] { "--jsno" }, out _));
            Assert.IsFalse(new CliOptions().Flag("--json").TryParse(new[] { "stray" }, out _));
        }

        [TestMethod]
        public void Options_NegativeNumbersAreValuesNotOptions()
        {
            var parsed = Parse(new CliOptions().Value("--size").Positionals(), "--size", "-1", "-2");

            Assert.AreEqual("-1", parsed.Value("--size"));
            CollectionAssert.AreEqual(new[] { "-2" }, parsed.Positionals.ToArray());
        }

        [TestMethod]
        public void Names_AreKebabCaseAndRoundTrip()
        {
            Assert.AreEqual("ground-types", CliNames.Layer(LayerType.GroundTypes));
            Assert.AreEqual("areas-of-interest", CliNames.Layer(LayerType.AreasOfInterest));
            Assert.AreEqual("thrones-of-britannia", CliNames.Game(GameTemplate.Thrones_Of_Britannia));
            Assert.AreEqual("warhammer3", CliNames.Game(GameTemplate.Warhammer3));

            foreach (var layer in CliNames.AllLayers)
            {
                Assert.IsTrue(CliNames.TryParseLayer(CliNames.Layer(layer), out var parsed) && parsed == layer, layer.ToString());
            }

            foreach (var game in CliNames.AllGames)
            {
                Assert.IsTrue(CliNames.TryParseGame(CliNames.Game(game), out var parsed) && parsed == game, game.ToString());
            }
        }

        [TestMethod]
        public void Names_AreMatchedIgnoringCaseAndSeparators()
        {
            Assert.IsTrue(CliNames.TryParseLayer("Ground_Types", out var layer) && layer == LayerType.GroundTypes);
            Assert.IsTrue(CliNames.TryParseGame("PHARAOH DYNASTIES", out var game) && game == GameTemplate.Pharaoh_Dynasties);
            Assert.IsFalse(CliNames.TryParseLayer("count", out _));
            Assert.IsFalse(CliNames.TryParseGame("shogun2", out _));
        }

        [TestMethod]
        public void BatchLines_SplitOnSpacesAndKeepQuotedValuesWhole()
        {
            Assert.IsTrue(BatchCommand.TryTokenise("import-layer --file \"C:\\my layers\\roads.hex_layer\"  --as roads", out var tokens));

            CollectionAssert.AreEqual(new[] { "import-layer", "--file", @"C:\my layers\roads.hex_layer", "--as", "roads" }, tokens.ToArray());
        }

        [TestMethod]
        public void BatchLines_WithAnUnclosedQuote_AreRejected()
        {
            Assert.IsFalse(BatchCommand.TryTokenise("paint --swatch \"open", out _));
        }

        [TestMethod]
        public void MapCommands_WithoutAMap_AreRejected()
        {
            foreach (var command in new[] { "paint", "erase", "line", "fill", "pick", "query", "fill-swatches", "import-layer", "export-layer", "batch" })
            {
                Assert.AreEqual(1, Dispatch(command, "--layer", "roads"), command);
            }
        }

        [TestMethod]
        public void MapCommands_WithIncompleteArguments_AreRejectedBeforeTheMapIsOpened()
        {
            Assert.AreEqual(1, Dispatch("paint", "-m", _mapPath, "--hex", "1,1"), "paint without --layer");
            Assert.AreEqual(1, Dispatch("paint", "-m", _mapPath, "--layer", "roads", "--brush-size", "0"), "paint with brush size 0");
            Assert.AreEqual(1, Dispatch("line", "-m", _mapPath, "--layer", "roads", "--from", "1,1"), "line without --to");
            Assert.AreEqual(1, Dispatch("export-layer", "-m", _mapPath, "--dir", _dir, "--all", "--layer", "roads"), "export-layer with --all and --layer");
            Assert.AreEqual(1, Dispatch("export-layer", "-m", _mapPath, "--dir", _dir, "--all", "--format", "gif"), "export-layer with an unknown format");
            Assert.AreEqual(1, Dispatch("import-layer", "-m", _mapPath), "import-layer with nothing to import");
            Assert.AreEqual(1, Dispatch("import-layer", "-m", _mapPath, "--dir", _dir, "--as", "roads"), "import-layer --as with --dir");
            Assert.AreEqual(1, Dispatch("fill-swatches", "-m", _mapPath), "fill-swatches without --all or --layer");
            Assert.AreEqual(1, Dispatch("query", "-m", _mapPath, "--layer", "regions"), "query --layer without --swatches");
            Assert.AreEqual(1, Dispatch("batch", "-m", _mapPath, "--file", Path.Combine(_dir, "absent.txt")), "batch with a missing file");
            Assert.AreEqual(1, Dispatch("paint", "-m", _mapPath, "--layer", "roads", "--hex", "1,1", "--output", Path.Combine(_dir, "out.txt")), "paint --output that is not a .hex");
        }

        [TestMethod]
        public void Create_RejectsInvalidRequests()
        {
            Assert.AreEqual(1, Dispatch("create", "--game", "rome2"), "no name");
            Assert.AreEqual(1, Dispatch("create", "--name", "my_map"), "no game or template");
            Assert.AreEqual(1, Dispatch("create", "--name", "my_map", "--game", "shogun2"), "unknown game");
            Assert.AreEqual(1, Dispatch("create", "--name", "my_map", "--game", "rome2", "--width", "101"), "odd width");
            Assert.AreEqual(1, Dispatch("create", "--name", "my_map", "--game", "rome2", "--height", "0"), "no hexes");
            Assert.AreEqual(1, Dispatch("create", "--name", "my_map", "--template", "anything", "--game", "rome2"), "template with a game");
            Assert.AreEqual(1, Dispatch("create", "--name", "!!!", "--game", "rome2", "--output", _dir), "a name with nothing usable in it");
        }

        [TestMethod]
        public void Create_RefusesToReplaceAnExistingProjectWithoutForce()
        {
            var existing = Path.Combine(_dir, "my_map");
            Directory.CreateDirectory(existing);
            File.WriteAllText(Path.Combine(existing, "keep.txt"), "user data");

            var exitCode = Dispatch("create", "--name", "my_map", "--game", "rome2", "--output", _dir);

            Assert.AreEqual(1, exitCode);
            Assert.IsTrue(File.Exists(Path.Combine(existing, "keep.txt")), "The existing project must be left alone.");
            StringAssert.Contains(_captured.ToString(), "--force");
        }

        [TestMethod]
        public void Help_ListsEveryCommandAndEachCommandHasItsOwnHelp()
        {
            Assert.AreEqual(0, Dispatch("--help"));
            var globalHelp = _captured.ToString();

            foreach (var command in new[] { "process", "validate", "create", "paint", "erase", "line", "fill", "pick",
                                            "fill-swatches", "import-layer", "export-layer", "query", "config", "batch" })
            {
                StringAssert.Contains(globalHelp, $"  {command} ", $"Global help should list {command}.");
            }

            Assert.AreEqual(0, Dispatch("help", "paint"));
            StringAssert.Contains(_captured.ToString(), "--brush-size");

            Assert.AreEqual(0, Dispatch("config", "--help"));
            StringAssert.Contains(_captured.ToString(), "assembly-kit-path");
        }

        private CliArguments Parse(CliOptions options, params string[] args)
        {
            Assert.IsTrue(options.TryParse(args, out var parsed), _captured.ToString());
            return parsed;
        }

        private static int Dispatch(params string[] args)
        {
            var method = typeof(CliRunner).GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "CliRunner.Dispatch was not found.");

            return (int)method.Invoke(null, new object[] { args });
        }
    }
}
