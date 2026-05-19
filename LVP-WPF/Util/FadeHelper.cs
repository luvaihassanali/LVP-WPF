using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace LVP_WPF.Util
{
    /// <summary>
    /// Opacity-fade animations used by MainWindow and TvShowWindow when
    /// briefly dimming the UI while a child window or modal dialog is open.
    /// 250ms duration, fades between 1.0 (fully opaque) and 0.1 (nearly
    /// invisible).
    ///
    /// Was two near-identical `*_Fade(double direction)` methods that used
    /// 0.1 as a magic-number "this means fade out" marker - replaced with
    /// a bool `fadeOut` parameter.
    /// </summary>
    public static class FadeHelper
    {
        private const double FadedOutOpacity = 0.1;
        private const double FullyOpaqueOpacity = 1.0;
        private static readonly Duration FadeDuration = new Duration(TimeSpan.FromMilliseconds(250));

        /// <summary>
        /// Fades <paramref name="grid"/>'s Opacity to 0.1 (fadeOut=true) or
        /// back up to 1.0 (fadeOut=false) over 250ms.
        /// </summary>
        public static void Fade(Grid grid, bool fadeOut)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                Duration = FadeDuration,
                AutoReverse = false,
                RepeatBehavior = new RepeatBehavior(1),
                From = fadeOut ? FullyOpaqueOpacity : FadedOutOpacity,
                To = fadeOut ? FadedOutOpacity : FullyOpaqueOpacity
            };
            grid.BeginAnimation(UIElement.OpacityProperty, animation);
        }
    }
}
