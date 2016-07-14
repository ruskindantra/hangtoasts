using System;
using System.Collections.Generic;

namespace HangToasts
{
    public class HangfireQueuesChangedEventArgs : EventArgs
    {
        public long ProcessingCount { get; private set; }

        public IDictionary<string, long> QueuedCount { get; private set; }

        public HangfireQueuesChangedEventArgs(long processingCount, IDictionary<string, long> queuedCount)
        {
            ProcessingCount = processingCount;
            QueuedCount = queuedCount;
        }
    }
}