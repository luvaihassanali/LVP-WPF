using Serilog;
using System;
using System.Windows;

namespace LVP_WPF.Services
{
    /// <summary>
    /// WPF-backed implementation of <see cref="IUserPrompts"/>. NotificationDialog
    /// and InputDialog already dispatch their own ShowDialog onto the UI thread;
    /// OptionDialog doesn't, so we wrap it.
    ///
    /// Every prompt is also logged here (Error / Warning / Information) before
    /// the dialog opens. Two reasons:
    ///   1. The file sink + the load-screen TextBox sink both pick those up,
    ///      so dialog interactions are reconstructable from logs after the
    ///      fact - useful when something fires and you've already dismissed
    ///      it.
    ///   2. The load-screen TextBox shows the dialog content the moment
    ///      MediaEnricher tries to pop it. Centralizing here means we don't
    ///      have to remember to Log.X before every _prompts.Show* call.
    /// </summary>
    internal sealed class WpfUserPrompts : IUserPrompts
    {
        public void ShowError(string caption, string message)
        {
            Log.Error("[Dialog] {Caption} - {Message}", caption, message);
            NotificationDialog.Show(caption, message);
        }

        public void ShowNotice(string caption, string message, TvShow? tvShow = null, int currSeason = 0, string? episodePath = null)
        {
            Log.Warning("[Dialog] {Caption} - {Message}", caption, message);
            InputDialog.Show(caption, message, tvShow, currSeason, episodePath);
        }

        public int ChooseOption(string title, string path, string[][] info, DateTime?[] dates)
        {
            Log.Information("[Dialog] Choose: {Title} ({OptionCount} options) for {Path}",
                title, info?[0]?.Length ?? 0, path);
            int result = 0;
            Application.Current.Dispatcher.Invoke(delegate
            {
                result = OptionDialog.Show(title, path, info, dates);
            });
            Log.Information("[Dialog] Chose id={Result} for {Title}", result, title);
            return result;
        }
    }
}
