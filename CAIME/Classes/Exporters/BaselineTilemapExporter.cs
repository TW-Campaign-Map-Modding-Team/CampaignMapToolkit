using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CAIME
{
    /// <summary>
    /// Writes a baseline tilemap image for a project. One instance owns one export's state.
    /// </summary>
    public sealed class BaselineTilemapExporter
    {
        private byte[]                  relevantIndices; //road, river, cliff, beach
        private string                  exportFilename;
        private Bitmap                  tilemapImage;

        public static bool Export(Project project)
        {
            return new BaselineTilemapExporter().Run(project);
        }

        private bool Run(Project project)
        {
            var mapHexFile = project.MapHexFile;

            if (Initialise(project) == false)
            {
                return false;
            }

            tilemapImage = CreateBitmap((int)mapHexFile.MapWidth, (int)mapHexFile.MapHeight, relevantIndices, mapHexFile);

            Finalise();

            return true;
        }

        private bool Initialise(Project project)
        {
            var dialog = new SaveFileDialog()
            {
                Filter = "Image file (*.png)|*.png",
                Title = "Save baseline tilemap file",
            };

            dialog.FileName = "tile_map.png";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                LoggerViewModel.Log("Baseline tilemap export cancelled by user.", LogLevel.Info);
                return false;
            }

            if (string.IsNullOrEmpty(dialog.FileName))
            {
                LoggerViewModel.Log("Failed to export baseline tilemap image due to invalid filename.", LogLevel.Error);
                return false;
            }

            exportFilename = dialog.FileName;

            return CacheColours(project);
        }

        private void Finalise()
        {
            tilemapImage.Save(exportFilename, ImageFormat.Png);
            tilemapImage.Dispose();
            tilemapImage = null;

            LoggerViewModel.Log("Successfuly exported baseline tilemap image.", LogLevel.Info);
        }

        private bool CacheColours(Project project)
        {
            var mapHexFile = project.MapHexFile;
            relevantIndices = new byte[mapHexFile.Capacity];

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                Hex hex = mapHexFile.HexData[hexIndex];


                bool isTilemapCliff;
                bool isTilemapCliffEnd;
                uint riverNeighbors = 0;
                if (hex.IsBeach || hex.IsSea)
                {
                    isTilemapCliff = false;
                    isTilemapCliffEnd = false;
                }
                else
                {
                    bool seaNeighbor = false;
                    bool beachNeighbor = false;
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var neighbour = mapHexFile.HexData[nbrIndex];
                        if (neighbour.IsSea)
                            seaNeighbor = true;
                        if (neighbour.IsBeach)
                            beachNeighbor = true;
                        if (neighbour.IsRiver)
                            riverNeighbors += 1;
                    }
                    isTilemapCliff = seaNeighbor;
                    isTilemapCliffEnd = isTilemapCliff && beachNeighbor;
                }

                if (hex.IsSea) //sea overrides all
                {
                    relevantIndices[hexIndex] = 6;
                }
                else if (isTilemapCliff)
                {
                    if (isTilemapCliffEnd)
                        relevantIndices[hexIndex] = 9; //cliff end
                    else
                       relevantIndices[hexIndex] = 3; //normal cliff
                }
                else if (hex.IsRoad)
                {
                    if (hex.IsRiver)
                        relevantIndices[hexIndex] = 7; //road over river
                    else
                        relevantIndices[hexIndex] = 1; //normal road
                }
                else if (hex.IsRiver)
                {
                    if (hex.IsBeach)
                        relevantIndices[hexIndex] = 8; //river ending at beach
                    else if (riverNeighbors == 1)
                        relevantIndices[hexIndex] = 10; //river source
                    else
                        relevantIndices[hexIndex] = 2; //normal river
                }
                else if(hex.IsBeach)
                {
                    relevantIndices[hexIndex] = 4; //normal beach
                }
                else //(hex.IsLand) everything else gets generic land
                {
                    relevantIndices[hexIndex] = 5;
                }
            }

            return true;
        }

        private static Bitmap CreateBitmap(int hexMapWidth, int hexMapHeight, byte[] imageData, MapHexFile mapHexFile)
        {
            var bitmap = new Bitmap(hexMapWidth * 2 , (hexMapHeight * 2) +1, PixelFormat.Format8bppIndexed);

            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                            ImageLockMode.WriteOnly, bitmap.PixelFormat);

            // Hex row 0 is the bottom of the map but image row 0 is the top. Flip after doubling,
            // not before: Doubled staggers odd columns towards the next hex row, which is north.
            var resampledData = Utility.FlipRawDataVert(Doubled(imageData, hexMapWidth, hexMapHeight), bitmap.Width);

            Utility.CopyRowsToBitmap(resampledData, bmpData, bitmap.Width, bitmap.Height);

            bitmap.UnlockBits(bmpData);

            var pal = bitmap.Palette;

            //These in common
            pal.Entries[4] = Color.FromArgb(255, 255, 255, 0); //beach

            if (mapHexFile.GameName == "warhammer3")
            {
                //These for Warhammer3
                pal.Entries[1] = Color.FromArgb(255, 93, 66, 24); //road
                pal.Entries[2] = Color.FromArgb(255, 223, 180, 145); //no rivers (using generic)
                pal.Entries[3] = Color.FromArgb(255, 253, 3, 1); //gen_cliff

                pal.Entries[5] = Color.FromArgb(255, 223, 180, 145); //generic
                pal.Entries[6] = Color.FromArgb(255, 83, 141, 213); //sea

                pal.Entries[7] = Color.FromArgb(255, 93, 66, 24); //no bridges over rivers (using road)
                pal.Entries[8] = Color.FromArgb(255, 255, 255, 0); //no river mouths (using beach)
                pal.Entries[9] = Color.FromArgb(255, 84, 230, 84); //cliff_gen_ends (cliff-beach transition)
                pal.Entries[10] = Color.FromArgb(255, 223, 180, 145); //no river sources (using generic)
            }
            else if (mapHexFile.GameName == "three_kingdoms")
            {
                //These for Three Kingdoms, from the tile sets in _tile_database/_settings.bin
                pal.Entries[1] = Color.FromArgb(255, 93, 0, 24); //roads_tracks, the most common of the three road sets
                pal.Entries[2] = Color.FromArgb(255, 0, 0, 255); //river
                pal.Entries[3] = Color.FromArgb(255, 249, 173, 105); //blockout_cliff

                pal.Entries[5] = Color.FromArgb(255, 150, 170, 100); //generic
                pal.Entries[6] = Color.FromArgb(255, 57, 113, 183); //generic_sea

                pal.Entries[7] = Color.FromArgb(255, 218, 67, 255); //river_crossing_track, to match roads_tracks
                pal.Entries[8] = Color.FromArgb(255, 204, 204, 255); //river_mouth
                pal.Entries[9] = Color.FromArgb(255, 159, 34, 42); //blockout_cliff_ends
                pal.Entries[10] = Color.FromArgb(255, 180, 180, 255); //river_start
            }
            else
            {
                //These for Attila, at least
                pal.Entries[1] = Color.FromArgb(255, 149, 104, 38); //road
                pal.Entries[2] = Color.FromArgb(255, 0, 0, 255); //river
                pal.Entries[3] = Color.FromArgb(255, 254, 0, 0); //cliff

                pal.Entries[5] = Color.FromArgb(255, 90, 118, 71); //some grass01 for land
                pal.Entries[6] = Color.FromArgb(255, 83, 141, 213); //sea

                pal.Entries[7] = Color.FromArgb(255, 127, 0, 255); //road over river
                pal.Entries[8] = Color.FromArgb(255, 204, 204, 255); //river ending at beach
                pal.Entries[9] = Color.FromArgb(255, 254, 0, 0); //no cliff-beach transition, use cliff
                pal.Entries[10] = Color.FromArgb(255, 180, 180,255); //river source
            }

            bitmap.Palette = pal;

            return bitmap;
        }

        //Takes in an array of numbers representing colored hexagons
        //Doubles the array in each dimension and then shifts every-other column down by one to better represent the hexagonal structure
        private static byte[] Doubled(byte[] input, int oldWidth, int oldHeight)
        {
            var newWidth = oldWidth * 2;
            var newHeight = (oldHeight * 2) + 1;

            var output = new byte[newWidth * newHeight];

            for (int row = 0; row < oldHeight; ++row)
            {
                for (int col = 0; col < oldWidth; ++col)
                {
                    if (row % 2 == 0)
                    {
                        if (col % 2 == 0) //"on" grid, nothing special
                        {
                            output[(col * 2) + (row * 2) * newWidth] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + ((row * 2) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                        }
                        else
                        {
                            if (row != 0)  //from row before
                            {
                                output[(col * 2) + (row * 2) * newWidth] = input[col + (row - 1) * oldWidth];
                                output[(col * 2) + 1 + ((row * 2) * newWidth)] = input[col + (row - 1) * oldWidth];
                            }
                            else //unless it's the first row, then just take it as-is
                            {
                                output[(col * 2) + (row * 2) * newWidth] = input[col + row * oldWidth];
                                output[(col * 2) + 1 + ((row * 2) * newWidth)] = input[col + row * oldWidth];
                            }

                            output[(col * 2) + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                        }
                    }
                    else
                    {
                        if (col % 2 == 0) //"on" grid, nothing special
                        {
                            output[(col * 2) + (row * 2) * newWidth] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + ((row * 2) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                        }
                        else
                        {
                            output[(col * 2) + (row * 2) * newWidth] = input[col + (row - 1) * oldWidth]; //from row before
                            output[(col * 2) + 1 + ((row * 2) * newWidth)] = input[col + (row - 1) * oldWidth]; //from row before
                            output[(col * 2) + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                            output[(col * 2) + 1 + (((row * 2) + 1) * newWidth)] = input[col + row * oldWidth];
                        }
                    }

                    //If it's the last row, then copy this row to the "next-next" row
                    if (row == (oldHeight - 1))
                    {
                        output[(col * 2) + (((row * 2) + 2) * newWidth)] = input[col + row * oldWidth];
                        output[(col * 2) + 1 + (((row * 2) + 2) * newWidth)] = input[col + row * oldWidth];
                    }
                }
            }

            return output;
        }
    }
}
