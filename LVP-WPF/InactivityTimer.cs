using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

// https://social.msdn.microsoft.com/Forums/sqlserver/en-US/454d300f-a8ab-4bae-aa9a-2b96493865f5/inactive-user-timeout-recommended-way-?forum=wpf

namespace LVP_WPF
{
    /// <summary>
    /// Measures the time of user inactivity in a WPF application.
    /// </summary>
    public class InactivityTimer : IDisposable
    {
        #region Events

        /// <summary>
        /// Occurs when no input has been received from the user for the duration of the
        /// <see cref="TimeOut"/> interval.
        /// </summary>
        public event EventHandler Inactivity;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the period of time after with the <see cref="Inactivity"/> event is raised
        /// if not input from the user has been received during that period.
        /// </summary>
        /// <value>
        /// The period of time after with the <see cref="Inactivity"/> event is raised
        /// if not input from the user has been received during that period.
        /// </value>
        public TimeSpan TimeOut { get; private set; }

        /// <summary>
        /// Gets the amount of time the user has been inactive.
        /// </summary>
        /// <value>The amount of time the user has been inactive.</value>
        public TimeSpan InactivityTime
        {
            get
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount - _lastActivityTime);
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// The time of the last activity of the user.
        /// </summary>
        int _lastActivityTime;

        /// <summary>
        /// The timer to use to find-out if the user is inactive.
        /// </summary>
        DispatcherTimer _timer;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="InactivityTimer"/> class.
        /// </summary>
        /// <param name="timeOut">The period of time after with the <see cref="Inactivity"/> event is raised
        /// if not input from the user has been received during that period.</param>
        public InactivityTimer(TimeSpan timeOut)
        {
            if (timeOut <= TimeSpan.Zero || timeOut.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("timeOut", timeOut, string.Format("Must be a positive number less than {0}.", int.MaxValue));
            }

            TimeOut = timeOut;

            _lastActivityTime = Environment.TickCount;

            InputManager.Current.PreNotifyInput += new NotifyInputEventHandler(Current_PreNotifyInput);

            _timer = new DispatcherTimer()
            {
                Interval = timeOut,
            };

            _timer.Tick += new EventHandler(_timer_Tick);

            _timer.Start();
        }

        /// <summary>
        /// Handles the PreNotifyInput event of the Current control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.NotifyInputEventArgs"/>
        /// instance containing the event data.</param>
        void Current_PreNotifyInput(object sender, NotifyInputEventArgs e)
        {
            if (e.StagingItem.Input is MouseEventArgs
                || e.StagingItem.Input is KeyboardEventArgs
                || e.StagingItem.Input is TextCompositionEventArgs
                || e.StagingItem.Input is StylusEventArgs)
            {
                _lastActivityTime = Environment.TickCount;
            }
            else
            {
                // The event is an internal event not caused by user intervention
            }
        }

        /// <summary>
        /// Handles the Tick event of the _timer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void _timer_Tick(object sender, EventArgs e)
        {
            int time = Environment.TickCount;

            // The time when the inactivity event should be raised
            int inactivityTime = _lastActivityTime + (int)TimeOut.TotalMilliseconds;

            if (inactivityTime - time <= 0)
            {
                Inactivity?.Invoke(this, EventArgs.Empty);
                _lastActivityTime = time;
                _timer.Interval = TimeOut;
            }
            else
            {
                _timer.Interval = TimeSpan.FromMilliseconds(inactivityTime - time);
            }
        }

        /// <summary>
        /// Resets the interval of time of user inactivity.
        /// </summary>
        /// <remarks>
        /// Use this method to begin waiting for user inactivity from the present moment.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The timer is not access by the thread that created it.</exception>
        public void Reset()
        {
            _timer.Dispatcher.VerifyAccess();

            _lastActivityTime = Environment.TickCount;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <exception cref="InvalidOperationException">The timer is not access by the thread that created it.</exception>
        public void Dispose()
        {
            _timer.Dispatcher.VerifyAccess();

            _timer.Tick -= new EventHandler(_timer_Tick);

            _timer.Stop();

            InputManager.Current.PreNotifyInput -= new NotifyInputEventHandler(Current_PreNotifyInput);
        }
    }
}
