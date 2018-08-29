using System;
namespace PushSharp.Core
{
    public interface IPushService : IDisposable
    {
        event ChannelCreatedDelegate OnChannelCreated;

        event ChannelDestroyedDelegate OnChannelDestroyed;

        event NotificationSentDelegate OnNotificationSent;

        event NotificationFailedDelegate OnNotificationFailed;

        event NotificationRequeueDelegate OnNotificationRequeue;

        event ChannelExceptionDelegate OnChannelException;

        event ServiceExceptionDelegate OnServiceException;

        event DeviceSubscriptionExpiredDelegate OnDeviceSubscriptionExpired;

        event DeviceSubscriptionChangedDelegate OnDeviceSubscriptionChanged;

        IPushChannelFactory PushChannelFactory { get; }

        IPushServiceSettings ServiceSettings { get; }

        IPushChannelSettings ChannelSettings { get; }

        bool IsStopping { get; }

        void QueueNotification(INotification notification);

        void Stop(bool waitForQueueToFinish = true);
    }
}