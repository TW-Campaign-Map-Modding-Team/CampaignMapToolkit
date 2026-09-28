using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using CAIME;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="BaselineTilemapExporter"/>'s bitmap stage - the part that lays the
    /// hex grid's tile indices out as the pixels of the tilemap image.
    ///
    /// <para>
    /// The public entry point opens a save dialog, so these drive the private static stage directly.
    /// </para>
    /// </summary>
    [TestClass]
    public class BaselineTilemapExporterTests
    {
        private static readonly Type ExporterType = typeof(BaselineTilemapExporter);

        // Hex row 0 is the bottom of the map while an image's row 0 is the top, so the northern row
        // must come out on top. Odd columns sit half a hex north of even ones (their up-right and
        // down-right neighbours are rows R+1 and R), so their stagger must point up in the image;
        // flipping before doubling instead of after would point it down.
        [TestMethod]
        public void CreateBitmap_PutsNorthAtTheTopWithOddColumnsStaggeredNorth()
        {
            var hexGrid = new byte[]
            {
                1, 2,   // row 0, south
                3, 4,   // row 1, north
            };

            using (var bitmap = CreateBitmap(2, 2, hexGrid, new MapHexFile()))
            {
                CollectionAssert.AreEqual(
                    new byte[]
                    {
                        3, 3, 4, 4,
                        3, 3, 4, 4,
                        3, 3, 2, 2,
                        1, 1, 2, 2,
                        1, 1, 2, 2,
                    },
                    ReadPaletteIndices(bitmap),
                    "The tilemap must have north at the top with odd columns half a hex higher.");
            }
        }

        private static byte[] ReadPaletteIndices(Bitmap bitmap)
        {
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                          ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var indices = new byte[bitmap.Width * bitmap.Height];

                for (int row = 0; row < bitmap.Height; ++row)
                    Marshal.Copy(IntPtr.Add(bmpData.Scan0, row * bmpData.Stride), indices, row * bitmap.Width, bitmap.Width);

                return indices;
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }

        private static Bitmap CreateBitmap(int hexMapWidth, int hexMapHeight, byte[] imageData, MapHexFile mapHexFile)
            => (Bitmap)Invoke("CreateBitmap", hexMapWidth, hexMapHeight, imageData, mapHexFile);

        private static object Invoke(string methodName, params object[] args)
        {
            var method = ExporterType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"BaselineTilemapExporter.{methodName} was not found.");

            return method.Invoke(null, args);
        }
    }
}
