using System;
using System.Windows;

namespace CAIME.Painters
{
    public enum PaintState
    {
        NotPainting,
        PaintStart,
        Painting,
    }

    public enum PainterState
    {
        Active,
        Inactive,
    }

    public interface IViewportPainter
    {
        bool Initialise();
        void Shutdown();

        void OnActivated();
        void OnDeactivated();

        /// <summary>
        /// Called once, when left mouse button was pressed
        /// </summary>
        /// <param name="mousePos">Mouse position at the time left button was pressed</param>
        bool PaintStart(System.Windows.Point mousePos);
        /// <summary>
        /// Called for as long as left mouse button is held and mouse is being moved
        /// </summary>
        bool Paint(PaintData data);
        /// <summary>
        /// Called when left mouse button was released
        /// </summary>
        /// <param name="mousePos">Mouse position at the time left button was released</param>
        bool PaintEnd(System.Windows.Point mousePos);

        PaintState GetPaintState();
        PainterState GetState();
    }

    public class PaintData
    {
        public Layer    Layer;
        public Swatch   Swatch;
        public Hex      HitHex;
        public int      HitHexIndex;
        public int      BrushSize;
        public bool     CanDisplay;
    }

    public class ViewportToolParameters : EventArgs
    {
        public Hex      Hex;
        public int      HexIndex;
        public Layer    Layer;
        public Swatch   Swatch;
        public bool     CanDisplay;
    }

    public abstract class AbstractViewportPainter : IViewportPainter, IUndoRedoHandler
    {
        protected abstract class PaintStateSnapshot : StateSnapshot
        {
            /// <summary>
            /// Layer that was painted on. Undo/Redo must target this layer,
            /// not the layer that is active at the time of the undo/redo.
            /// </summary>
            public Layer Layer;
        }

        private PaintData delayedPaintData;

        protected readonly EditorViewModel editorVM;
        protected readonly ViewportViewModel viewportVM;
        protected readonly UndoRedoManager undoRedoManager;

        protected Project project;
        protected Hex lastHex;

        protected PainterState state;
        protected PaintState paintingState;

        protected bool isInit;
        protected bool customBrushSize;

        protected StateSnapshot oldState;
        protected StateSnapshot newState;

        public AbstractViewportPainter(ViewportViewModel vvm, EditorViewModel evm)
        {
            viewportVM      = vvm;
            editorVM        = evm;
            undoRedoManager = evm.UndoRedoManager;
            isInit          = false;
            customBrushSize = false;
            paintingState   = PaintState.NotPainting;

            evm.ProjectManager.OnOpenProject += (sender, e) =>
            {
                project = e.Project;
            };
        }

        public virtual bool PaintStart(System.Windows.Point mousePos)
        {
            paintingState = PaintState.PaintStart;
            return true;
        }

        public virtual bool Paint(PaintData data)
        {
            // Check if layer is visible otherwise no reason to proceed
            if (!data.Layer.IsVisible)
            {
                LoggerViewModel.Log("You cannot paint over an invisible layer!", LogLevel.Warning);
                return false;
            }

            if (paintingState == PaintState.NotPainting)
            {
                delayedPaintData = data;
                return false;
            }

            if (data.HitHex == lastHex)
            {
                return false;
            }

            lastHex = data.HitHex;

            paintingState = PaintState.Painting;

            InitialiseSnapshotStates();

            return true;
        }

        public virtual bool PaintEnd(System.Windows.Point mousePos)
        {
            if (paintingState != PaintState.Painting && delayedPaintData != null)
            {
                Paint(delayedPaintData);
                delayedPaintData = null;
            }

            paintingState = PaintState.NotPainting;

            project.MapHexFile.SetDirty();
            return true;
        }

        public PaintState GetPaintState()
        {
            return paintingState;
        }

        public PainterState GetState()
        {
            return state;
        }

        public virtual void OnActivated()
        {
            state = PainterState.Active;

            AppStateContext.Instance.BrushSizeSliderVisibility = customBrushSize ? Visibility.Visible : Visibility.Collapsed;
        }

        public virtual void OnDeactivated()
        {
            state = PainterState.Inactive;

            AppStateContext.Instance.BrushSizeSliderVisibility = Visibility.Collapsed;
        }

        public virtual bool Initialise()
        {
            isInit = true;
            return isInit;
        }

        public virtual void Shutdown()
        {
            paintingState   = PaintState.NotPainting;
            state           = PainterState.Inactive;

            isInit          = false;

            project         = null;
            lastHex         = null;

            oldState        = null;
            newState        = null;
        }

        /// <summary>
        /// Gates painting the given swatch onto a hex. Currently only restricts beach painting to
        /// coastal hexes - removing a beach and every other swatch type is unrestricted. Only meant
        /// for fresh user paint strokes; undo/redo replay recorded swatches unconditionally.
        /// </summary>
        protected bool CanPaintHex(Swatch swatch, int hexIndex)
        {
            return MapPainter.CanPaintHex(project.MapHexFile, swatch, hexIndex);
        }

        protected abstract void InitialiseSnapshotStates();

        public abstract void RestoreState(StateSnapshot snapshot);

        public abstract void ApplyState(StateSnapshot snapshot);

        protected void CreateRestoreAction(string name)
        {
            if (newState == null || oldState == null)
            {
                // Nothing was painted during this gesture
                return;
            }

            var curLayer = editorVM.GetActiveLayer();
            if (state == PainterState.Active && curLayer.IsVisible)
            {
                (newState as PaintStateSnapshot).Layer = curLayer;
                (oldState as PaintStateSnapshot).Layer = curLayer;

                undoRedoManager.PushUndoAction(new UndoRedoAction(this, newState, oldState, name));

                newState = null;
                oldState = null;
            }
        }
    }
}
