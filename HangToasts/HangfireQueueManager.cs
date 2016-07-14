using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Timers;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using RuskinDantra.Extensions;
using Timer = System.Timers.Timer;

namespace HangToasts
{
    public class HangfireQueueManager : IDisposable
    {
        private readonly Timer _timer;
        public event EventHandler<HangfireQueuesChangedEventArgs> Change;

        private readonly Dictionary<string, long> _queuesWithEnqueuedJobs;
        
        private static readonly Semaphore Semaphore = new Semaphore(1, 1);
        private IMonitoringApi _monitoringApi;
        private long _processingJobs;
        private bool _fireChangeEvent;

        public HangfireQueueManager(double pollingInterval)
        {
            _timer = new Timer(pollingInterval);
            _timer.Elapsed += TimerOnElapsed;
            _timer.AutoReset = true;

            _queuesWithEnqueuedJobs = new Dictionary<string, long>();
            _processingJobs = 0;
        }

        public void Initialise()
        {
            ConnectionStringSettings connectionStringSettings = ConfigurationManager.ConnectionStrings["LocalHangfire"];
            JobStorage.Current = new SqlServerStorage(connectionStringSettings.ConnectionString);
            _monitoringApi = JobStorage.Current.GetMonitoringApi();

            _timer.Start();
        }

        private long ProcessingJobs
        {
            get { return _processingJobs; }
            set
            {
                if (_processingJobs == value) return;

                _processingJobs = value;
                _fireChangeEvent = true;
            }
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (Semaphore.WaitOne(0))
            {
                try
                {
                    ProcessingJobs = _monitoringApi.ProcessingCount();

                    IList<QueueWithTopEnqueuedJobsDto> allQueues = _monitoringApi.Queues();

                    foreach (var queue in allQueues)
                    {
                        if (!_queuesWithEnqueuedJobs.ContainsKey(queue.Name))
                            _queuesWithEnqueuedJobs.Add(queue.Name, 0);

                        long queueCount = queue.Length;

                        if (queueCount != _queuesWithEnqueuedJobs[queue.Name])
                        {
                            _queuesWithEnqueuedJobs[queue.Name] = queueCount;
                            _fireChangeEvent = true;
                        }
                    }

                    OnChange(new HangfireQueuesChangedEventArgs(ProcessingJobs, _queuesWithEnqueuedJobs));
                }
                finally
                {
                    Semaphore.Release(1);
                }
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        protected virtual void OnChange(HangfireQueuesChangedEventArgs e)
        {
            if (_fireChangeEvent)
            {
                _fireChangeEvent = false;
                Change.Raise(this, e);
            }
        }

        public string ToastRepresentation()
        {
            var queuedJobs = new StringBuilder();

            if (ProcessingJobs > 0)
                queuedJobs.AppendLine($"[Processing] [{ProcessingJobs}]");
            else
            {
                queuedJobs.AppendLine($"[Processing jobs cleared]");
            }

            foreach (KeyValuePair<string, long> queue in _queuesWithEnqueuedJobs)
            {
                long queueCount = queue.Value;
                if (queueCount > 0)
                    queuedJobs.AppendLine($"[{queue.Key} queue] [{queueCount}]");
                else
                    queuedJobs.AppendLine($"[{queue.Key} cleared]");
            }

            return queuedJobs.ToString();
        }
    }
}