using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LVP_WPF.Util
{
    /// <summary>
    /// WPF visual-tree and dispatcher helpers used to thread the cache-build
    /// and remote-input code through the UI. Previously sat on GuiModel as
    /// static methods alongside view-model state; relocated here so the view
    /// model only holds view-model things.
    /// </summary>
    public static class WpfTreeHelpers
    {
        /// <summary>
        /// WinForms-style Application.DoEvents() equivalent for WPF: pumps the
        /// dispatcher at background priority so layout/render finishes before
        /// the caller continues. Used after mutating the visual tree from
        /// worker threads.
        /// </summary>
        public static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        /// <summary>
        /// Recursively searches the visual tree under <paramref name="visualElement"/>
        /// for a control of <paramref name="typeElement"/> whose Name property
        /// equals <paramref name="nameElement"/>. Returns the first match or null.
        /// (Originally from https://stackoverflow.com/questions/37247724/find-controls-placed-inside-listview-wpf)
        /// </summary>
        public static Visual? GetChildrenByType(Visual visualElement, Type typeElement, string nameElement)
        {
            if (visualElement == null) return null;

            if (visualElement.GetType() == typeElement
                && visualElement is FrameworkElement fe
                && fe.Name == nameElement)
            {
                return fe;
            }

            // Apply the template if we can - some children may not exist yet
            // otherwise. (ItemsControl items, ContentControl content, etc.)
            (visualElement as FrameworkElement)?.ApplyTemplate();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visualElement); i++)
            {
                Visual visual = (Visual)VisualTreeHelper.GetChild(visualElement, i);
                Visual? found = GetChildrenByType(visual, typeElement, nameElement);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively searches the visual tree under <paramref name="o"/> for
        /// the first ScrollViewer descendant. Returns null if none found.
        /// </summary>
        public static DependencyObject? GetScrollViewer(DependencyObject o)
        {
            if (o is System.Windows.Controls.ScrollViewer) return o;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                DependencyObject? result = GetScrollViewer(VisualTreeHelper.GetChild(o, i));
                if (result != null) return result;
            }
            return null;
        }
    }
}
