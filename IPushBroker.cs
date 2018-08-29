using System;
using System.Collections.Generic;
namespace PushSharp.Core
{
    public interface IPushBroker : IDisposable
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

        void RegisterService<TPushNotification>(IPushService pushService, string applicationId, bool raiseErrorOnDuplicateRegistrations = true) where TPushNotification : Notification;

        void RegisterService<TPushNotification>(IPushService pushService, bool raiseErrorOnDuplicateRegistrations = true) where TPushNotification : Notification;

        void QueueNotification<TPushNotification>(TPushNotification notification) where TPushNotification : Notification;

        void QueueNotification<TPushNotification>(TPushNotification notification, string applicationId) where TPushNotification : Notification;

        IEnumerable<IPushService> GetAllRegistrations();

        IEnumerable<IPushService> GetRegistrations<TNotification>();

        IEnumerable<IPushService> GetRegistrations(string applicationId);

        IEnumerable<IPushService> GetRegistrations<TNotification>(string applicationId);

        void StopAllServices(bool waitForQueuesToFinish = true);

        void StopAllServices<TNotification>(bool waitForQueuesToFinish = true);

        void StopAllServices<TNotification>(string applicationId, bool waitForQueuesToFinish = true);

        void StopAllServices(string applicationId, bool waitForQueuesToFinish = true);
    }
}