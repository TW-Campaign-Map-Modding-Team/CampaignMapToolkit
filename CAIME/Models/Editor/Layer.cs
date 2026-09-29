using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace CAIME
{
    public enum LayerType
    {
        Impassable = 0,
        TradeRoutes,
        Roads,
        TownSlots,
        TownSprawl,
        Bridges,
        Rivers,
        Beaches,
        RegionBorders,
        Regions,
        Attritions,
        Climates,
        GroundTypes,
        Restrictions,
        AreasOfInterest,

        Count
    }

    public class Layer : ObservableObject
    {
        private bool raiseActive;
        private bool raiseVisible;
        private bool isActive;
        public bool IsActive
        {
            get
            {
                return isActive;
            }
            set
            {
                isActive = value;
                OnPropertyChanged(nameof(IsActive));
                if (isActive && raiseActive)
                {
                    ActiveLayerChanged?.Invoke(this, null);
                }
                raiseActive = true;
            }
        }
        private bool isVisible;
        public bool IsVisible
        {
            get
            {
                return isVisible;
            }
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                if (raiseVisible)
                {
                    VisibilityChanged?.Invoke(this, null);
                }
                raiseVisible = true;
            }
        }

        public const byte FullyOpaque = 255;

        private byte opacity = FullyOpaque;
        public byte Opacity
        {
            get
            {
                return opacity;
            }
            set
            {
                if (opacity == value)
                {
                    return;
                }

                opacity = value;
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(IsOpaque));
                OpacityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsOpaque => opacity == FullyOpaque;

        public int[] Colours { get; private set; }

        public readonly LayerType Type;
        public string Name { get; private set; }

        public ICommand SetOpacityPercentCommand { get; }

        public event RoutedEventHandler VisibilityChanged;
        public event RoutedEventHandler ActiveLayerChanged;
        public event EventHandler OpacityChanged;

        public Layer(LayerType type, bool isVisible = false, bool isActive = false)
        {
            Type            = type;
            Name            = Utility.InsertSpacesBetweenCapitals(type.ToString());
            IsVisible       = isVisible;
            IsActive        = isActive;
            raiseActive     = true;
            raiseVisible    = true;

            SetOpacityPercentCommand = new RelayCommand<object>(percent => Opacity = OpacityFromPercent(Convert.ToDouble(percent, CultureInfo.InvariantCulture)));
        }

        public static byte OpacityFromPercent(double percent)
        {
            return (byte)Math.Round(Math.Max(0.0, Math.Min(percent, 100.0)) * FullyOpaque / 100.0);
        }

        public static double OpacityToPercent(byte opacity)
        {
            return opacity * 100.0 / FullyOpaque;
        }

        /// <summary>
        /// Changes layer visibility
        /// </summary>
        /// <param name="isVisible">Should be visible?</param>
        /// <param name="raiseEvent">Should raise <see cref="VisibilityChanged"/> event?</param>
        public void SetVisible(bool isVisible, bool raiseEvent = false)
        {
            raiseVisible = raiseEvent;
            IsVisible = isVisible;
        }

        /// <summary>
        /// Changes active layer
        /// </summary>
        /// <param name="isActive">Should be set to active?</param>
        /// <param name="raiseEvent">Should raise <see cref="ActiveLayerChanged"/> event?</param>
        public void SetActive(bool isActive, bool raiseEvent = false)
        {
            raiseActive = raiseEvent;
            IsActive = isActive;
        }

        /// <summary>
        /// Changes layer colours
        /// </summary>
        /// <param name="colours">New colours</param>
        /// <param name="raiseEvent">Should raise <see cref="VisibilityChanged"/> event?</param>
        public void SetColours(int[] colours, bool raiseEvent = false)
        {
            Colours = colours;
            if (raiseEvent)
            {
                VisibilityChanged?.Invoke(this, null);
            }
        }
    }
}
