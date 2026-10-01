using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for LoggerWindow.xaml
    /// </summary>
    public partial class LoggerWindow : Window
    {
        private bool scrollToNewestMessagePending;

        public LoggerWindow()
        {
            InitializeComponent();
            DataContext = LoggerViewModel.Instance;

            ((INotifyCollectionChanged)logsContainerListBox.Items).CollectionChanged += Logs_CollectionChanged;
        }

        private void Logs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || scrollToNewestMessagePending)
            {
                return;
            }

            scrollToNewestMessagePending = true;
            Dispatcher.BeginInvoke(new Action(ScrollToNewestMessage), DispatcherPriority.Background);
        }

        private void ScrollToNewestMessage()
        {
            scrollToNewestMessagePending = false;

            var items = logsContainerListBox.Items;
            if (items.Count > 0)
            {
                logsContainerListBox.ScrollIntoView(items[items.Count - 1]);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LoggerViewModel.Clear();
        }

        private void CopySelected_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CopyItemsToClipboard(logsContainerListBox.SelectedItems.Cast<MessageItem>());
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            CopyItemsToClipboard(logsContainerListBox.SelectedItems.Cast<MessageItem>());
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            CopyItemsToClipboard(LoggerViewModel.Instance.MessagesStack);
        }

        private static void CopyItemsToClipboard(IEnumerable<MessageItem> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"[{item.Time}] {item.Message}");
            }

            if (sb.Length == 0)
            {
                return;
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                // The clipboard is a shared resource - another process holding its lock makes
                // SetText throw. Never take the app down over a failed copy.
                LoggerViewModel.Log($"Could not copy to the clipboard: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
