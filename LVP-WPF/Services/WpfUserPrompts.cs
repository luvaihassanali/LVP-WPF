using System;
using System.Windows;

namespace LVP_WPF.Services
{
    /// <summary>
    /// WPF-backed implementation of <see cref="IUserPrompts"/>. NotificationDialog
    /// and InputDialog already dispatch their own ShowDialog onto the UI thread;
    /// OptionDialog doesn't, so we wrap it.
    /// </summary>
    internal sealed class WpfUserPrompts : IUserPrompts
    {
        public void ShowError(string caption, string message)
            => NotificationDialog.Show(caption, message);

        public void ShowNotice(string caption, string message, TvShow? tvShow = null, int currSeason = 0)
            => InputDialog.Show(caption, message, tvShow, currSeason);

        public int ChooseOption(string title, string path, string[][] info, DateTime?[] dates)
        {
            int result = 0;
            Application.Current.Dispatcher.Invoke(delegate
            {
                result = OptionDialog.Show(title, path, info, dates);
            });
            return result;
        }
    }
}
