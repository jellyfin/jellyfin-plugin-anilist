using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Anime
{
    /// <summary>
    /// The RateLimiter class attempts to regulate the rate at which an event occurs, by delaying
    /// new occurances of the event.
    /// </summary>
    /// <remarks>
    /// The <see cref="RateLimiter" /> will allow bursts of activity (down to a minimum occurance interval),
    /// but attempts to maintain a minimum interval between occurances over a given time window.
    /// </remarks>
    public class RateLimiter
    {
        private readonly AsyncLock _lock;
        private readonly int _maxAllowedInWindow;
        private readonly TimeSpan _minimumInterval;
        private readonly TimeSpan _targetInterval;
        private readonly TimeSpan _timeWindowDuration;

        private readonly List<DateTime> _window;

        private DateTime _lastTake;

        /// <summary>
        /// Creates a new instance of the <see cref="RateLimiter" /> class.
        /// </summary>
        /// <param name="minimumInterval">The minimum time between events.</param>
        /// <param name="targetInterval">The target average time between events.</param>
        /// <param name="timeWindow">The time span over which the average rate is calculated.</param>
        public RateLimiter(TimeSpan minimumInterval, TimeSpan targetInterval, TimeSpan timeWindow)
        {
            _window = new List<DateTime>();
            _lock = new AsyncLock();
            _minimumInterval = minimumInterval;
            _targetInterval = targetInterval;
            _timeWindowDuration = timeWindow;

            _maxAllowedInWindow = (int)(timeWindow.Ticks / targetInterval.Ticks);

            _lastTake = DateTime.Now - minimumInterval;
        }

        /// <summary>
        /// Attempts to trigger an event, waiting if needed.
        /// </summary>
        /// <returns>A task which completes when it is safe to proceed.</returns>
        public async Task Tick()
        {
            using (await _lock.LockAsync())
            {
                TimeSpan wait = CalculateWaitDuration();
                if (wait.Ticks > 0)
                {
                    await Task.Delay(wait);
                }

                DateTime now = DateTime.Now;
                _window.Add(now);
                _lastTake = now;
            }
        }

        private TimeSpan CalculateWaitDuration()
        {
            FlushExpiredRecords();
            if (_window.Count == 0)
            {
                return TimeSpan.Zero;
            }

            DateTime now = DateTime.Now;
            TimeSpan minWait = (_lastTake + _minimumInterval) - now;

            float load = (float)_window.Count / _maxAllowedInWindow;

            float waitTicks = minWait.Ticks + (_targetInterval.Ticks - minWait.Ticks) * load;
            return new TimeSpan((long)waitTicks);
        }

        private void FlushExpiredRecords()
        {
            DateTime now = DateTime.Now;
            while (_window.Count > 0 && now - _window[0] > _timeWindowDuration)
            {
                _window.RemoveAt(0);
            }
        }
    }
}
