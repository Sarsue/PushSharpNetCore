using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace PushSharp.Core
{
    public abstract class PushServiceBase : IPushService, IDisposable
    {
        private List<PushServiceBase.ChannelWorker> channels = new List<PushServiceBase.ChannelWorker>();
        private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        private List<PushServiceBase.WaitTimeMeasurement> measurements = new List<PushServiceBase.WaitTimeMeasurement>();
        private List<PushServiceBase.WaitTimeMeasurement> sendTimeMeasurements = new List<PushServiceBase.WaitTimeMeasurement>();
        private DateTime lastNotificationQueueTime = DateTime.MinValue;
        private readonly object measurementsLock = new object();
        private readonly object sendTimeMeasurementsLock = new object();
        private readonly object channelsLock = new object();
        private readonly object queuedNotificationsLock = new object();
        private ManualResetEvent waitQueuedNotifications = new ManualResetEvent(false);
        private Timer timerCheckScale;
        private int scaleSync;
        private volatile bool stopping;
        private NotificationQueue queuedNotifications;
        private long trackedNotificationCount;
        private long totalSendCount;

        protected PushServiceBase(IPushChannelFactory pushChannelFactory, IPushChannelSettings channelSettings)
          : this(pushChannelFactory, channelSettings, (IPushServiceSettings)null)
        {
        }

        protected PushServiceBase(IPushChannelFactory pushChannelFactory, IPushChannelSettings channelSettings, IPushServiceSettings serviceSettings)
        {
            this.PushChannelFactory = pushChannelFactory;
            this.ServiceSettings = serviceSettings ?? (IPushServiceSettings)new PushServiceSettings();
            this.ChannelSettings = channelSettings;
            this.queuedNotifications = new NotificationQueue();
            this.scaleSync = 0;
            this.timerCheckScale = new Timer(new TimerCallback(this.CheckScale), (object)null, TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0));
            this.CheckScale((object)null);
            this.stopping = false;
        }

        public event ChannelCreatedDelegate OnChannelCreated;

        public event ChannelDestroyedDelegate OnChannelDestroyed;

        public event NotificationSentDelegate OnNotificationSent;

        public event NotificationFailedDelegate OnNotificationFailed;

        public event NotificationRequeueDelegate OnNotificationRequeue;

        public event ChannelExceptionDelegate OnChannelException;

        public event ServiceExceptionDelegate OnServiceException;

        public event DeviceSubscriptionExpiredDelegate OnDeviceSubscriptionExpired;

        public event DeviceSubscriptionChangedDelegate OnDeviceSubscriptionChanged;

        protected void RaiseSubscriptionExpired(string expiredSubscriptionId, DateTime expirationDateUtc, INotification notification)
        {
            DeviceSubscriptionExpiredDelegate subscriptionExpired = this.OnDeviceSubscriptionExpired;
            if (subscriptionExpired == null)
                return;
            subscriptionExpired((object)this, expiredSubscriptionId, expirationDateUtc, notification);
        }

        public void RaiseServiceException(Exception error)
        {
            ServiceExceptionDelegate serviceException = this.OnServiceException;
            if (serviceException == null)
                return;
            serviceException((object)this, error);
        }

        public virtual bool BlockOnMessageResult
        {
            get
            {
                return true;
            }
        }

        public IPushChannelFactory PushChannelFactory { get; private set; }

        public IPushServiceSettings ServiceSettings { get; private set; }

        public IPushChannelSettings ChannelSettings { get; private set; }

        public bool IsStopping
        {
            get
            {
                return this.stopping;
            }
        }

        public void QueueNotification(INotification notification)
        {
            this.QueueNotification(notification, false, false, false);
        }

        private void QueueNotification(INotification notification, bool countsAsRequeue = true, bool ignoreStoppingChannel = false, bool queueToFront = false)
        {
            this.lastNotificationQueueTime = DateTime.UtcNow;
            if (this.cancelTokenSource.IsCancellationRequested)
                throw new ObjectDisposedException("Service", "Service has already been signaled to stop");
            if (this.ServiceSettings.MaxNotificationRequeues < 0 || notification.QueuedCount <= this.ServiceSettings.MaxNotificationRequeues)
            {
                Interlocked.Increment(ref this.trackedNotificationCount);
                notification.EnqueuedTimestamp = DateTime.UtcNow;
                if (countsAsRequeue)
                    ++notification.QueuedCount;
                if (queueToFront)
                    this.queuedNotifications.EnqueueAtStart(notification);
                else
                    this.queuedNotifications.Enqueue(notification);
                this.waitQueuedNotifications.Set();
            }
            else
            {
                NotificationFailedDelegate notificationFailed = this.OnNotificationFailed;
                if (notificationFailed != null)
                    notificationFailed((object)this, notification, (Exception)new MaxSendAttemptsReachedException());
                Log.Info("Notification ReQueued Too Many Times: {0}", (object)notification.QueuedCount);
            }
        }

        public void Stop(bool waitForQueueToFinish = true)
        {
            this.stopping = true;
            DateTime utcNow = DateTime.UtcNow;
            if (waitForQueueToFinish)
            {
                Log.Info("Waiting for Queue to Finish");
                while (this.queuedNotifications.Count > 0 || Interlocked.Read(ref this.trackedNotificationCount) > 0L)
                    Thread.Sleep(100);
                Log.Info("Queue Emptied.");
            }
            Log.Info("Stopping CheckScale Timer");
            if (this.timerCheckScale != null)
                this.timerCheckScale.Change(-1, -1);
            Log.Info("Stopping all Channels");
            lock (this.channelsLock)
            {
                Parallel.ForEach<PushServiceBase.ChannelWorker>((IEnumerable<PushServiceBase.ChannelWorker>)this.channels, (Action<PushServiceBase.ChannelWorker>)(c => c.Dispose()));
                this.channels.Clear();
            }
            this.cancelTokenSource.Cancel();
            Log.Info("PushServiceBase->DISPOSE.");
        }

        public void Dispose()
        {
            if (this.stopping)
                return;
            this.Stop(false);
        }

        private void CheckScale(object state = null)
        {
            int num = -1;
            try
            {
                num = Interlocked.CompareExchange(ref this.scaleSync, 1, 0);
                if (num != 0)
                    return;
                int totalMilliseconds1 = (int)this.AverageQueueWaitTime.TotalMilliseconds;
                int totalMilliseconds2 = (int)this.AverageSendTime.TotalMilliseconds;
                Log.Debug("{0} -> Avg Queue Wait Time {1} ms, Avg Send Time {2} ms", (object)this, (object)totalMilliseconds1, (object)totalMilliseconds2);
                Log.Debug("{0} -> Checking Scale ({1} Channels Currently)", (object)this, (object)this.ChannelCount);
                if (this.ServiceSettings.AutoScaleChannels && !this.cancelTokenSource.IsCancellationRequested)
                {
                    if (this.ChannelCount <= 0L && this.QueueLength > 0L)
                    {
                        Log.Info("{0} -> Creating Channel {1}", (object)this, (object)this.ChannelCount);
                        this.ScaleChannels(ChannelScaleAction.Create, 1);
                    }
                    else if (this.ServiceSettings.IdleTimeout > TimeSpan.Zero && this.ChannelCount > 0L && (this.QueueLength <= 0L && DateTime.UtcNow - this.lastNotificationQueueTime > this.ServiceSettings.IdleTimeout) && Interlocked.Read(ref this.trackedNotificationCount) <= 0L)
                    {
                        Log.Info("{0} -> Service Idle, Destroying all Channels", (object)this, (object)this.ChannelCount);
                        while (this.ChannelCount > 0L && !this.cancelTokenSource.IsCancellationRequested)
                            this.ScaleChannels(ChannelScaleAction.Destroy, 1);
                    }
                    else if ((long)totalMilliseconds1 < this.ServiceSettings.MinAvgTimeToScaleChannels && this.ChannelCount > 1L)
                    {
                        int count = 1;
                        if (totalMilliseconds1 <= 0)
                            count = 5;
                        if (this.ChannelCount - (long)count <= 0L)
                            count = 1;
                        Log.Info("{0} -> Destroying Channel", (object)this);
                        this.ScaleChannels(ChannelScaleAction.Destroy, count);
                    }
                    else
                    {
                        if (this.ChannelCount >= (long)this.ServiceSettings.MaxAutoScaleChannels)
                            return;
                        int count = 0;
                        if (totalMilliseconds1 > 5000)
                            count = 3;
                        else if (totalMilliseconds1 > 1000)
                            count = 2;
                        else if ((long)totalMilliseconds1 > this.ServiceSettings.MinAvgTimeToScaleChannels)
                            count = 1;
                        if (count <= 0)
                            return;
                        if (this.ChannelCount + (long)count > (long)this.ServiceSettings.MaxAutoScaleChannels)
                            count = (int)((long)this.ServiceSettings.MaxAutoScaleChannels - this.ChannelCount);
                        if (count <= 0)
                            return;
                        Log.Info("{0} -> Creating {1} Channel(s)", (object)this, (object)count);
                        this.ScaleChannels(ChannelScaleAction.Create, count);
                    }
                }
                else if (this.ServiceSettings.IdleTimeout > TimeSpan.Zero && this.ChannelCount > 0L && (this.QueueLength <= 0L && DateTime.UtcNow - this.lastNotificationQueueTime > this.ServiceSettings.IdleTimeout) && Interlocked.Read(ref this.trackedNotificationCount) <= 0L)
                {
                    Log.Info("{0} -> Service Idle, Destroying all Channels", (object)this, (object)this.ChannelCount);
                    while (this.ChannelCount > 0L && !this.cancelTokenSource.IsCancellationRequested)
                        this.ScaleChannels(ChannelScaleAction.Destroy, 1);
                }
                else
                {
                    while (this.ChannelCount > (long)this.ServiceSettings.Channels && !this.cancelTokenSource.IsCancellationRequested)
                    {
                        Log.Info("{0} -> Destroying Channel", (object)this);
                        this.ScaleChannels(ChannelScaleAction.Destroy, 1);
                    }
                    while (this.ChannelCount < (long)this.ServiceSettings.Channels && !this.cancelTokenSource.IsCancellationRequested && (DateTime.UtcNow - this.lastNotificationQueueTime <= this.ServiceSettings.IdleTimeout && Interlocked.Read(ref this.trackedNotificationCount) > 0L))
                    {
                        Log.Info("{0} -> Creating Channel", (object)this);
                        this.ScaleChannels(ChannelScaleAction.Create, 1);
                    }
                }
            }
            finally
            {
                if (num == 0)
                    this.scaleSync = 0;
            }
        }

        public TimeSpan AverageQueueWaitTime
        {
            get
            {
                if (this.measurements == null || this.measurements.Count <= 0)
                    return TimeSpan.Zero;
                lock (this.measurementsLock)
                {
                    while (this.measurements.Count > 1000)
                        this.measurements.RemoveAt(0);
                    this.measurements.RemoveAll((Predicate<PushServiceBase.WaitTimeMeasurement>)(m => m.Timestamp < DateTime.UtcNow.AddSeconds(-30.0)));
                    if (this.measurements.Count < 20)
                        return TimeSpan.Zero;
                    IEnumerable<long> source = this.measurements.Select<PushServiceBase.WaitTimeMeasurement, long>((Func<PushServiceBase.WaitTimeMeasurement, long>)(m => m.Milliseconds));
                    try
                    {
                        return TimeSpan.FromMilliseconds(source.Average());
                    }
                    catch
                    {
                        return TimeSpan.Zero;
                    }
                }
            }
        }

        public TimeSpan AverageSendTime
        {
            get
            {
                if (this.sendTimeMeasurements == null || this.sendTimeMeasurements.Count <= 0)
                    return TimeSpan.Zero;
                lock (this.sendTimeMeasurementsLock)
                {
                    while (this.sendTimeMeasurements.Count > 1000)
                        this.sendTimeMeasurements.RemoveAt(0);
                    this.sendTimeMeasurements.RemoveAll((Predicate<PushServiceBase.WaitTimeMeasurement>)(m => m.Timestamp < DateTime.UtcNow.AddSeconds(-30.0)));
                    IEnumerable<long> source = this.sendTimeMeasurements.Select<PushServiceBase.WaitTimeMeasurement, long>((Func<PushServiceBase.WaitTimeMeasurement, long>)(s => s.Milliseconds));
                    try
                    {
                        return TimeSpan.FromMilliseconds(source.Average());
                    }
                    catch
                    {
                        return TimeSpan.Zero;
                    }
                }
            }
        }

        public long QueueLength
        {
            get
            {
                lock (this.queuedNotificationsLock)
                    return (long)this.queuedNotifications.Count;
            }
        }

        public long ChannelCount
        {
            get
            {
                lock (this.channelsLock)
                    return (long)this.channels.Count;
            }
        }

        private void ScaleChannels(ChannelScaleAction action, int count = 1)
        {
            for (int index = 0; index < count && !this.cancelTokenSource.IsCancellationRequested; ++index)
            {
                int num = 0;
                bool? nullable = new bool?();
                IPushChannel pushChannel = (IPushChannel)null;
                lock (this.channelsLock)
                {
                    switch (action)
                    {
                        case ChannelScaleAction.Create:
                            pushChannel = this.PushChannelFactory.CreateChannel(this.ChannelSettings);
                            PushServiceBase.ChannelWorker channelWorker = new PushServiceBase.ChannelWorker(pushChannel, new Action<IPushChannel, CancellationTokenSource>(this.DoChannelWork));
                            channelWorker.WorkerTask.ContinueWith((Action<Task>)(t => Log.Error("Channel Worker Failed Task: " + t.Exception.ToString())), TaskContinuationOptions.OnlyOnFaulted);
                            this.channels.Add(channelWorker);
                            num = this.channels.Count;
                            nullable = new bool?(false);
                            break;
                        case ChannelScaleAction.Destroy:
                            if (this.channels.Count > 0)
                            {
                                PushServiceBase.ChannelWorker channel = this.channels[0];
                                this.channels.RemoveAt(0);
                                channel.Dispose();
                                num = this.channels.Count;
                                nullable = new bool?(true);
                                break;
                            }
                            break;
                    }
                }
                if (nullable.HasValue && !nullable.Value)
                {
                    ChannelCreatedDelegate onChannelCreated = this.OnChannelCreated;
                    if (onChannelCreated != null)
                        onChannelCreated((object)this, pushChannel);
                }
                else if (nullable.HasValue && nullable.Value)
                {
                    ChannelDestroyedDelegate channelDestroyed = this.OnChannelDestroyed;
                    if (channelDestroyed != null)
                        channelDestroyed((object)this);
                }
            }
        }

        private void DoChannelWork(IPushChannel channel, CancellationTokenSource cancelTokenSource)
        {
            string str = Guid.NewGuid().ToString();
            long num = 0;
            while (!cancelTokenSource.IsCancellationRequested)
            {
                INotification notification = this.queuedNotifications.Dequeue();
                if (notification == null)
                {
                    Thread.Sleep(100);
                }
                else
                {
                    ManualResetEvent waitForNotification = (ManualResetEvent)null;
                    if (this.BlockOnMessageResult)
                        waitForNotification = new ManualResetEvent(false);
                    double totalMilliseconds = (DateTime.UtcNow - notification.EnqueuedTimestamp).TotalMilliseconds;
                    lock (this.measurementsLock)
                        this.measurements.Add(new PushServiceBase.WaitTimeMeasurement((long)totalMilliseconds));
                    DateTime sendStart = DateTime.UtcNow;
                    ++num;
                    Interlocked.Increment(ref this.totalSendCount);
                    if (num % 1000L == 0L)
                        Log.Debug("{0}> Send Count: {1} ({2})", (object)str, (object)num, (object)Interlocked.Read(ref this.totalSendCount));
                    channel.SendNotification(notification, (SendNotificationCallbackDelegate)((sender, result) =>
                    {
                        Interlocked.Decrement(ref this.trackedNotificationCount);
                        TimeSpan timeSpan = DateTime.UtcNow - sendStart;
                        lock (this.sendTimeMeasurementsLock)
                            this.sendTimeMeasurements.Add(new PushServiceBase.WaitTimeMeasurement((long)timeSpan.TotalMilliseconds));
                        waitForNotification?.Set();
                        if (result.ShouldRequeue)
                        {
                            NotificationRequeueEventArgs e = new NotificationRequeueEventArgs(result.Notification, result.Error);
                            NotificationRequeueDelegate notificationRequeue = this.OnNotificationRequeue;
                            if (notificationRequeue != null)
                                notificationRequeue((object)this, e);
                            if (e.Cancel)
                                return;
                            this.QueueNotification(result.Notification, result.CountsAsRequeue, true, true);
                        }
                        else if (!result.IsSuccess)
                        {
                            if (result.IsSubscriptionExpired)
                            {
                                if (!string.IsNullOrEmpty(result.NewSubscriptionId))
                                {
                                    DeviceSubscriptionChangedDelegate subscriptionChanged = this.OnDeviceSubscriptionChanged;
                                    if (subscriptionChanged == null)
                                        return;
                                    subscriptionChanged((object)this, result.OldSubscriptionId, result.NewSubscriptionId, result.Notification);
                                }
                                else
                                {
                                    DeviceSubscriptionExpiredDelegate subscriptionExpired = this.OnDeviceSubscriptionExpired;
                                    if (subscriptionExpired == null)
                                        return;
                                    subscriptionExpired((object)this, result.OldSubscriptionId, result.SubscriptionExpiryUtc, result.Notification);
                                }
                            }
                            else
                            {
                                NotificationFailedDelegate notificationFailed = this.OnNotificationFailed;
                                if (notificationFailed == null)
                                    return;
                                notificationFailed((object)this, result.Notification, result.Error);
                            }
                        }
                        else
                        {
                            NotificationSentDelegate notificationSent = this.OnNotificationSent;
                            if (notificationSent == null)
                                return;
                            notificationSent((object)this, result.Notification);
                        }
                    }));
                    if (waitForNotification != null && !waitForNotification.WaitOne(this.ServiceSettings.NotificationSendTimeout))
                    {
                        Interlocked.Decrement(ref this.trackedNotificationCount);
                        Log.Info("Notification send timeout");
                        NotificationFailedDelegate notificationFailed = this.OnNotificationFailed;
                        if (notificationFailed != null)
                            notificationFailed((object)this, notification, (Exception)new TimeoutException("Notification send timed out"));
                    }
                    if (waitForNotification != null)
                    {
                        waitForNotification.Close();
                        waitForNotification = (ManualResetEvent)null;
                    }
                }
            }
            channel.Dispose();
        }

        internal class WaitTimeMeasurement
        {
            public WaitTimeMeasurement(long milliseconds)
            {
                this.Timestamp = DateTime.UtcNow;
                this.Milliseconds = milliseconds;
            }

            public DateTime Timestamp { get; set; }

            public long Milliseconds { get; set; }
        }

        internal class ChannelWorker : IDisposable
        {
            public ChannelWorker(IPushChannel channel, Action<IPushChannel, CancellationTokenSource> worker)
            {
                PushServiceBase.ChannelWorker channelWorker = this;
                this.Id = Guid.NewGuid().ToString();
                this.CancelTokenSource = new CancellationTokenSource();
                this.Channel = channel;
                this.WorkerTask = Task.Factory.StartNew((Action)(() => worker(channel, channelWorker.CancelTokenSource)), TaskCreationOptions.LongRunning);
            }

            public void Dispose()
            {
                this.CancelTokenSource.Cancel();
            }

            public string Id { get; private set; }

            public Task WorkerTask { get; private set; }

            public IPushChannel Channel { get; set; }

            public CancellationTokenSource CancelTokenSource { get; set; }
        }
    }
}