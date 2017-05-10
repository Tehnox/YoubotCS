using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace YoubotCS.YoubotHandler
{
    #region HiPerfTimer
    public class HiPerfTimer
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        private long _startTime, _stopTime;
        private readonly long _freq;

        // Constructor
        public HiPerfTimer()
        {
            _startTime = 0; _stopTime = 0;
            if (QueryPerformanceFrequency(out _freq) == false)
            {
                // high-performance counter not supported
                throw new System.ComponentModel.Win32Exception();
            }
        }

        // Start the timer
        public void Start()
        {
            // lets do the waiting threads their work
            Thread.Sleep(0);
            QueryPerformanceCounter(out _startTime);
        }

        // Stop the timer
        public void Stop()
        {
            QueryPerformanceCounter(out _stopTime);
        }

        // Returns the duration of the timer (in seconds)
        public double Duration => (_stopTime - _startTime) / (double)_freq;
    }
    #endregion
}
