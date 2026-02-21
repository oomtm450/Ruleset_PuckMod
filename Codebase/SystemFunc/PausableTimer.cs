using System;
using System.Diagnostics;
using System.Threading;

namespace Codebase {
    public class PausableTimer : IDisposable {
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Action _callback;
        private bool _callbackCalled = false;
        private readonly long _intervalMilliseconds;
        private bool _isRunning = false;

        public PausableTimer(Action callback, long intervalMilliseconds) {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _intervalMilliseconds = intervalMilliseconds;
            // Initialize the internal timer, but don't start its period yet.
            _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start() {
            if (_isRunning)
                return;

            _stopwatch.Start();
            // Start the internal timer with the specified interval
            _timer.Change(_intervalMilliseconds - _stopwatch.ElapsedMilliseconds, Timeout.Infinite);
            _isRunning = true;
        }

        public void Pause() {
            if (!_isRunning)
                return;

            _stopwatch.Stop();
            // Stop the internal timer from firing again by setting period to Infinite
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
        }

        public void Reset() {
            _stopwatch.Reset();
            Pause(); // Pause also sets isRunning to false
        }

        private void TimerCallback(object state) {
            // This is where your custom logic goes.
            // It's called when the interval elapses.
            _callbackCalled = true;
            _callback.Invoke();

            /*// If the timer is still running (wasn't paused in the callback), restart the internal timer
            if (_isRunning) {
                // Note: The elapsed time is not used here for the *next* tick, 
                // the System.Threading.Timer handles the periodic calls if the period is not Infinite.
                // For a simple pausable, non-periodic timer with a single callback, 
                // we'd handle the 'isRunning' flag differently within the callback.
                // This implementation assumes a *periodic* pausable timer.

                // To implement a precise *single-shot* pausable timer, more complex logic 
                // involving remaining time calculation on pause/resume would be needed.
                // The provided implementation is for a simple periodic pausable timer 
                // that uses the internal timer's own period management.
            }*/
        }

        public bool TimerEnded() {
            return _callbackCalled;
        }

        public void Dispose() {
            _timer?.Dispose();
            _stopwatch?.Stop();
        }
    }
}
