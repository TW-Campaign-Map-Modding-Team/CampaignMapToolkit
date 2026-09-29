using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX.Direct3D11;

namespace CAIME
{
    using Vector3D = System.Windows.Media.Media3D.Vector3D;
    using Point3D  = System.Windows.Media.Media3D.Point3D;
    using MeshBuilder = HelixToolkit.Geometry.MeshBuilder;

    public class ViewportMouseEventArgs : RoutedEventArgs
    {
        public System.Windows.Point MousePos { get; private set; }
        public ViewportViewModel ViewportViewModel { get; private set; }

        public ViewportMouseEventArgs(System.Windows.Point mousePos, ViewportViewModel viewModel)
        {
            MousePos = mousePos;
            ViewportViewModel = viewModel;
        }
    }

    public delegate void ViewportMouseEvent(object sender, ViewportMouseEventArgs e);

    public class ViewportViewModel : BaseViewModel, IDisposable
    {
        private static string GRID_SECTION_TAG      = "GridSection";
        private static string BACKGROUND_IMAGE_TAG  = "BackgroundImage";

        // Hit-testing runs on every mouse move - reuse one layout instead of allocating one per hit.
        private static readonly HexLayout HIT_TEST_LAYOUT = new HexLayout(HexStyle.FlatTop, OffsetType.Odd, 10);

        private ICommand leftClick;
        public ICommand LeftClick
        {
            get
            {
                return leftClick;
            }
            set
            {
                leftClick = value;
                OnPropertyChanged(nameof(LeftClick));
            }
        }

        public IEffectsManager  EffectsManager      { get; private set; }
        public Camera           Camera              { get; private set; }
        public Material         HexGridMaterial     { get; private set; }
        public Material         GridOutlineMaterial { get; private set; }
        public MeshGeometry3D   GridOutlineGeometry { get; private set; }
        public DiffuseMaterial  BackImageMaterial   { get; private set; }
        public MeshGeometry3D   BackImageGeometry   { get; private set; }
        public Viewport3DX      Viewport            { get; private set; }

        private GridSubdivider GridSubdivider;

        /// <summary>
        /// List of grid chunks for ease of access.
        /// </summary>
        private MeshGeometryModel3D[] gridSections;
        /// <summary>
        /// Minimap view model
        /// </summary>
        private MinimapViewModel minimapVM;
        /// <summary>
        /// Background image viewport element reference.
        /// </summary>
        private MeshGeometryModel3D backgImageElement;
        /// <summary>
        /// Last opacity set via the background image slider, reapplied whenever a new background
        /// image is loaded so it doesn't snap back to fully opaque.
        /// </summary>
        private float backgroundImageOpacity = 1f;

        public EventHandler GridCreated;

        public ViewportMouseEvent OnLeftMouseDown;
        public ViewportMouseEvent OnLeftMouseUp;
        public ViewportMouseEvent OnLeftMouseMove;
        public ViewportMouseEvent OnMouseOver;

        public ViewportViewModel()
        {
            GridOutlineMaterial = PhongMaterials.Red;
            HexGridMaterial     = new VertColorMaterial();
            BackImageMaterial   = new DiffuseMaterial();
            EffectsManager      = new DefaultEffectsManager();
            GridSubdivider      = new GridSubdivider();

            // Settings up the orthographic camera;
            Camera = new OrthographicCamera
            {
                Position            = new Point3D(0, 0, 1),
                LookDirection       = new Vector3D(0, 0, -1),
                UpDirection         = new Vector3D(0, 1, 0),
                FarPlaneDistance    = 100_000,
                NearPlaneDistance   = 0.01,
            };

            var nearestNeighbourSampler = default(SamplerStateDescription);
            nearestNeighbourSampler.Filter = Filter.MinMagMipPoint;
            nearestNeighbourSampler.AddressU = TextureAddressMode.Clamp;
            nearestNeighbourSampler.AddressV = TextureAddressMode.Clamp;
            nearestNeighbourSampler.AddressW = TextureAddressMode.Clamp;
            nearestNeighbourSampler.MinimumLod = float.MinValue;
            nearestNeighbourSampler.MaximumLod = float.MaxValue;
            nearestNeighbourSampler.MipLodBias = 0f;
            nearestNeighbourSampler.MaximumAnisotropy = 16;
            nearestNeighbourSampler.ComparisonFunction = Comparison.Never;
            nearestNeighbourSampler.BorderColor = default;

            BackImageMaterial.DiffuseMapSampler = nearestNeighbourSampler;
            BackImageMaterial.EnableUnLit = true;
        }

        public void SetMinimapViewModel(MinimapViewModel mvm)
        {
            minimapVM = mvm;
        }

        private void ZoomToGrid()
        {
            var bounds = GridOutlineGeometry.Bound;
            var center = new Point3D(bounds.Center.X, bounds.Center.Y, bounds.Center.Z);
            var ratio  = 0.5 * (Viewport.ActualWidth / Viewport.ActualHeight);
            var radius = bounds.Width > bounds.Height ? center.X * ratio : center.Y * ratio;

            // Zoom viewport to match grid geometry extents
            Viewport.ZoomExtents(center, radius, 800);

            Viewport.ZoomDistanceLimitFar  = (float)(Math.Max(bounds.Width, bounds.Height) * 3.0);
            Viewport.ZoomDistanceLimitNear = 50;

            // ZoomExtents only animates the camera towards this width over the 800ms above, so
            // the baseline is derived the same way Helix derives it rather than read back off
            // the camera, which is still mid-animation at this point.
            DefaultCameraWidth = Viewport.ActualWidth > Viewport.ActualHeight
                ? radius * 2.0 * Viewport.ActualWidth / Viewport.ActualHeight
                : radius * 2.0;
        }

        /// <summary>
        /// World-space width the camera covered when the map was first framed. This is the
        /// fully zoomed out view a map opens at, and is the 100% baseline for <see cref="ZoomScale"/>.
        /// </summary>
        public double DefaultCameraWidth { get; private set; }

        /// <summary>
        /// How far the view is zoomed in relative to the map-open view: 1.0 at the framing a
        /// map opens with, 2.0 when hexes are drawn twice that size, and so on.
        /// </summary>
        public double ZoomScale
        {
            get
            {
                var camera = Camera as OrthographicCamera;
                if (camera == null || DefaultCameraWidth <= 0.0 || camera.Width <= 0.0)
                {
                    return 1.0;
                }

                // An orthographic camera zooms by narrowing the world-space width it covers,
                // so a narrower width than the baseline means we are zoomed further in.
                return DefaultCameraWidth / camera.Width;
            }
        }

        public void DestroyGrid()
        {
            if (GridOutlineGeometry == null)
            {
                return;
            }

            RemoveBackgroundImage();

            for (int index = 0; index < gridSections.Length; ++index)
            {
                Viewport.Items.Remove(gridSections[index]);
                gridSections[index].Dispose();
                gridSections[index] = null;
            }

            Viewport.Items.Remove(backgImageElement);

            backgImageElement.Dispose();
            backgImageElement = null;

            GridOutlineGeometry = null;
            BackImageGeometry = null;
            gridSections = null;

            OnPropertyChanged(nameof(GridOutlineGeometry));
            OnPropertyChanged(nameof(Viewport));
        }

        public void SetBackgroundImage(string path)
        {
            if (path == null)
            {
                return;
            }

            try
            {
                var backgroundImage = Image.FromFile(path);
                using (var ms = new MemoryStream())
                {
                    backgroundImage.Save(ms, backgroundImage.RawFormat);
                    var diffuseMap = new TextureModel(ms);

                    BackImageMaterial.DiffuseColor = new Color4(1, 1, 1, backgroundImageOpacity);
                    BackImageMaterial.DiffuseMap = diffuseMap;
                    OnPropertyChanged(nameof(BackImageMaterial));

                    backgImageElement.IsRendering = true;
                    AppStateContext.Instance.SetBackImageSlider(enabled: true);
                }

                backgroundImage.Dispose();
                backgroundImage = null;
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log(ex.ToString(), LogLevel.Error);
            }
        }

        /// <summary>
        /// Generates Hex Grid and displays it in the viewport.
        /// </summary>
        public void ConstructGridMesh(int width, int height, HexLayout hexLayout)
        {
            GridSubdivider.SplitGrid(width, height, hexLayout, out gridSections, out MeshBuilder[] meshBuilders);

            var subdivideGrid = new Action(() =>
            {
                for (int index = 0; index < gridSections.Length; ++index)
                {
                    var subMesh     = meshBuilders[index].ToMeshGeometry3D();
                    subMesh.Colors  = new Color4Collection(new Color4[subMesh.Positions.Count]);

                    var meshModel   = new MeshGeometryModel3D()
                    {
                        Geometry            = subMesh,
                        Material            = HexGridMaterial,
                        IsHitTestVisible    = false,
                        Tag                 = GRID_SECTION_TAG,
                    };

                    Viewport.Items.Add(meshModel);
                    gridSections[index] = Viewport.Items[Viewport.Items.Count - 1] as MeshGeometryModel3D;
                }

                if (GridOutlineGeometry == null)
                {
                    var builder = new MeshBuilder();
                    var min     = gridSections[0].Bounds.Minimum;
                    var max     = gridSections[gridSections.Length - 1].Bounds.Maximum;

                    Vector3[] corners =
                    {
                        new Vector3(min.X, max.Y, max.Z),
                        new Vector3(max.X, max.Y, max.Z),
                        new Vector3(max.X, min.Y, max.Z),
                        new Vector3(min.X, min.Y, max.Z),
                    };

                    builder.AddQuad(corners[0], corners[1], corners[2], corners[3]);

                    BackImageGeometry = builder.ToMeshGeometry3D();
                    backgImageElement = new MeshGeometryModel3D()
                    {
                        Geometry            = BackImageGeometry,
                        Material            = BackImageMaterial,
                        IsHitTestVisible    = false,
                        Tag                 = BACKGROUND_IMAGE_TAG,
                        IsTransparent       = true,
                        IsDepthClipEnabled  = false,
                        IsRendering         = false,
                    };

                    Viewport.Items.Add(backgImageElement);
                    
                    GridOutlineGeometry = builder.ToMeshGeometry3D();
                    OnPropertyChanged(nameof(GridOutlineGeometry));
                }

                ZoomToGrid();

                OnPropertyChanged(nameof(Viewport));
                LoggerViewModel.Log("Grid hex data have been created", LogLevel.Info);
                GridCreated?.Invoke(this, new EventArgs());
            });

            var op = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, subdivideGrid);

            // Also stop waiting when the operation is aborted (e.g. dispatcher shutdown) -
            // otherwise this background thread would spin forever.
            var status = op.Status;
            while (status != DispatcherOperationStatus.Completed && status != DispatcherOperationStatus.Aborted)
            {
                status = op.Wait(TimeSpan.FromMilliseconds(10));
            }
        }

        public void RemoveBackgroundImage()
        {
            BackImageMaterial.DiffuseMap = null;
            BackImageMaterial.DiffuseColor = new Color4(1, 1, 1, 0);
            OnPropertyChanged(nameof(BackImageMaterial));

            AppStateContext.Instance.SetBackImageSlider(enabled: false);
        }

        public void SetBackgroundImageOpacity(float opacity)
        {
            backgroundImageOpacity = opacity;
            BackImageMaterial.DiffuseColor = new Color4(1, 1, 1, opacity);
            OnPropertyChanged(nameof(BackImageMaterial));
        }

        /// <summary>
        /// Updates the opacity that will be applied the next time a background image is loaded,
        /// without touching the (currently invisible/removed) live material.
        /// </summary>
        public void SetDesiredBackgroundImageOpacity(float opacity)
        {
            backgroundImageOpacity = opacity;
        }

        /// <summary>
        /// Update colours for all hexes in the grid
        /// </summary>
        /// <param name="layer">Current layer to display</param>
        public void UpdateGridColours(int[] colours, int width, int height)
        {
            // Write every vertex colour first and upload each section's buffer once at the
            // end - calling UpdateColors() per hex re-uploads whole sections hundreds of
            // thousands of times on a full-map refresh.
            var sectionColours = SectionColours();
            Parallel.ForEach(Partitioner.Create(0, colours.Length), range =>
            {
                for (int index = range.Item1; index < range.Item2; ++index)
                {
                    int chunkIndex = GridSubdivider.FindSectionIndex(index, width, height);
                    int localIndex = GridSubdivider.GlobalIndexToSectionIndex(index, chunkIndex, width, height);

                    WriteHexColour(sectionColours[chunkIndex], localIndex * 6, new Color4(colours[index]));
                }
            });

            for (int index = 0; index < gridSections.Length; ++index)
            {
                gridSections[index].Geometry.UpdateColors();
            }

            dirtyColourSections.Clear();
        }

        // Sections whose vertex colours have been written but not yet uploaded. UpdateColors()
        // re-uploads a whole section's colour buffer, so doing it per hex means a single brush
        // stroke uploads the same sections hundreds of times per mouse-move.
        private readonly HashSet<int> dirtyColourSections = new HashSet<int>();
        private bool colourFlushScheduled;

        /// <summary>
        /// Update a single cell colour
        /// </summary>
        /// <param name="newColour">New colour</param>
        /// <param name="index">Cell index</param>
        public void UpdateCellColour(int newColour, int index, int width, int height)
        {
            int chunkIndex = GridSubdivider.FindSectionIndex(index, width, height);
            int localIndex = GridSubdivider.GlobalIndexToSectionIndex(index, chunkIndex, width, height);

            WriteHexColour(gridSections[chunkIndex].Geometry.Colors.GetInternalArray(), localIndex * 6, new Color4(newColour));

            dirtyColourSections.Add(chunkIndex);
            ScheduleColourFlush();
        }

        /// <summary>
        /// Queues <see cref="FlushCellColours"/> for just before the next frame composes, so all
        /// the hexes a painter touches in one gesture cost one upload per affected section.
        /// </summary>
        private void ScheduleColourFlush()
        {
            if (colourFlushScheduled)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                // No WPF dispatcher (tests, CLI): upload straight away so nothing is left pending.
                FlushCellColours();
                return;
            }

            colourFlushScheduled = true;
            dispatcher.BeginInvoke((Action)FlushCellColours, DispatcherPriority.Render);
        }

        /// <summary>Uploads the colour buffer of every section written since the last upload.</summary>
        public void FlushCellColours()
        {
            colourFlushScheduled = false;

            if (dirtyColourSections.Count == 0)
            {
                return;
            }

            foreach (int chunkIndex in dirtyColourSections)
            {
                gridSections[chunkIndex].Geometry.UpdateColors();
            }

            dirtyColourSections.Clear();
        }

        public sealed class CellBatch
        {
            internal readonly int[] SectionStarts;
            internal readonly int[] HexIndices;
            internal readonly int[] FirstVertices;

            internal CellBatch(int[] sectionStarts, int[] hexIndices, int[] firstVertices)
            {
                SectionStarts   = sectionStarts;
                HexIndices      = hexIndices;
                FirstVertices   = firstVertices;
            }
        }

        public CellBatch CreateCellBatch(int[] hexIndices, int width, int height)
        {
            var sections        = new int[hexIndices.Length];
            var localVertices   = new int[hexIndices.Length];
            var sectionStarts   = new int[gridSections.Length + 1];

            Parallel.ForEach(Partitioner.Create(0, hexIndices.Length), range =>
            {
                for (int index = range.Item1; index < range.Item2; ++index)
                {
                    sections[index]         = GridSubdivider.FindSectionIndex(hexIndices[index], width, height);
                    localVertices[index]    = GridSubdivider.GlobalIndexToSectionIndex(hexIndices[index], sections[index], width, height) * 6;
                }
            });

            for (int index = 0; index < hexIndices.Length; ++index)
            {
                ++sectionStarts[sections[index] + 1];
            }

            for (int section = 0; section < gridSections.Length; ++section)
            {
                sectionStarts[section + 1] += sectionStarts[section];
            }

            var nextSlots       = (int[])sectionStarts.Clone();
            var orderedHexes    = new int[hexIndices.Length];
            var firstVertices   = new int[hexIndices.Length];

            for (int index = 0; index < hexIndices.Length; ++index)
            {
                int slot = nextSlots[sections[index]]++;

                orderedHexes[slot]  = hexIndices[index];
                firstVertices[slot] = localVertices[index];
            }

            return new CellBatch(sectionStarts, orderedHexes, firstVertices);
        }

        public void UpdateCellColours(CellBatch batch, int[] colours)
        {
            var sectionColours  = SectionColours();
            var isChanged       = new bool[gridSections.Length];

            Parallel.For(0, gridSections.Length, section =>
            {
                var vertexColours = sectionColours[section];
                for (int index = batch.SectionStarts[section]; index < batch.SectionStarts[section + 1]; ++index)
                {
                    var colour      = new Color4(colours[batch.HexIndices[index]]);
                    int firstVertex = batch.FirstVertices[index];

                    if (vertexColours[firstVertex] != colour)
                    {
                        WriteHexColour(vertexColours, firstVertex, colour);
                        isChanged[section] = true;
                    }
                }
            });

            for (int section = 0; section < gridSections.Length; ++section)
            {
                if (isChanged[section])
                {
                    gridSections[section].Geometry.UpdateColors();
                }
            }
        }

        private Color4[][] SectionColours()
        {
            var sectionColours = new Color4[gridSections.Length][];
            for (int section = 0; section < gridSections.Length; ++section)
            {
                sectionColours[section] = gridSections[section].Geometry.Colors.GetInternalArray();
            }

            return sectionColours;
        }

        private static void WriteHexColour(Color4[] vertexColours, int firstVertex, Color4 colour)
        {
            for (int dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                vertexColours[firstVertex + dir] = colour;
            }
        }

        public void SetViewport(Viewport3DX viewport)
        {
            if (Viewport != null && Viewport.Equals(viewport))
            {
                return;
            }

            Viewport = viewport;
        }

        public void Dispose()
        {
            EffectsManager.Dispose();
        }

        public void Zoom(int delta)
        {
            minimapVM.ScaleViewFrame(delta);
        }

        public int FindHitHexIndex(System.Windows.Point mousePos)
        {
            var hitRes = Viewport.FindHits(mousePos);
            if (hitRes.Count <= 0)
            {
                return -1;
            }

            // Loop through an array of hit objects
            foreach (var hitObj in hitRes)
            {
                // If object hit equals to grid outline geometry then proceed otherwise skip
                var mesh = ((MeshGeometryModel3D)hitObj.ModelHit).Geometry;
                if (!mesh.Equals(GridOutlineGeometry))
                {
                    break;
                }

                // Get hit position in world coordinates
                var pointHit = new Vector2(hitObj.PointHit.X, hitObj.PointHit.Y);

                // Convert hit position from world (cube) to offset coords
                var hitPos = HIT_TEST_LAYOUT.PixelToHex(pointHit);
                var bounds = HIT_TEST_LAYOUT.PixelToHex(new Vector2(mesh.Bound.Maximum.X, mesh.Bound.Maximum.Y));

                int col        = (int)hitPos.X; // Hex column index
                int row        = (int)hitPos.Y; // Hex row index
                int boundsMaxX = (int)bounds.X; // Map width
                int boundsMaxY = (int)bounds.Y; // Map height

                // If found cell is within map bounds then proceed otherwise skip
                if (col < 0 || col >= boundsMaxX || row < 0 || row >= boundsMaxY)
                {
                    LoggerViewModel.Log("Mouse click was outside of the grid bounds", LogLevel.Warning);
                    return -1;
                }

                // Calculate cell index in hex grid array
                int index = row * boundsMaxX + col;
                return index; // No need to continue iterating
            }

            return -1;
        }

        public Hex FindHitHex(System.Windows.Point mousePos, MapHexFile mapHexFile)
        {
            int index = FindHitHexIndex(mousePos);
            if (index == -1)
            {
                return null;
            }

            return mapHexFile.HexData[index];
        }
    }
}
