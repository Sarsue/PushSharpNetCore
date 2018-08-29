using System;
using System.Collections.Generic;
using System.Linq;
namespace PushSharp.Core
{
    public class PushBroker : IPushBroker, IDisposable
    {
        private readonly object serviceRegistrationsLock = new object();
        private List<ServiceRegistration> serviceRegistrations;

        public PushBroker()
        {
            this.serviceRegistrations = new List<ServiceRegistration>();
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

        public void RegisterService<TPushNotification>(IPushService pushService, string applicationId, bool raiseErrorOnDuplicateRegistrations = true) where TPushNotification : Notification
        {
            if (raiseErrorOnDuplicateRegistrations && this.GetRegistrations<TPushNotification>(applicationId).Any<IPushService>())
                throw new InvalidOperationException("There's already a service registered to handle " + typeof(TPushNotification).Name + " notification types for the Application Id: " + (applicationId ?? "[ANY].  If you want to register the service anyway, pass in the raiseErrorOnDuplicateRegistrations=true parameter to this method."));
            ServiceRegistration serviceRegistration = ServiceRegistration.Create<TPushNotification>(pushService, applicationId);
            lock (this.serviceRegistrationsLock)
                this.serviceRegistrations.Add(serviceRegistration);
            pushService.OnChannelCreated += this.OnChannelCreated;
            pushService.OnChannelDestroyed += this.OnChannelDestroyed;
            pushService.OnChannelException += this.OnChannelException;
            pushService.OnDeviceSubscriptionExpired += this.OnDeviceSubscriptionExpired;
            pushService.OnNotificationFailed += this.OnNotificationFailed;
            pushService.OnNotificationSent += this.OnNotificationSent;
            pushService.OnNotificationRequeue += this.OnNotificationRequeue;
            pushService.OnServiceException += this.OnServiceException;
            pushService.OnDeviceSubscriptionChanged += this.OnDeviceSubscriptionChanged;
        }

        public void RegisterService<TPushNotification>(IPushService pushService, bool raiseErrorOnDuplicateRegistrations = true) where TPushNotification : Notification
        {
            this.RegisterService<TPushNotification>(pushService, (string)null, raiseErrorOnDuplicateRegistrations);
        }

        public void QueueNotification<TPushNotification>(TPushNotification notification) where TPushNotification : Notification
        {
            foreach (IPushService registration in this.GetRegistrations<TPushNotification>())
                registration.QueueNotification((INotification)notification);
        }

        public void QueueNotification<TPushNotification>(TPushNotification notification, string applicationId) where TPushNotification : Notification
        {
            IEnumerable<IPushService> registrations = this.GetRegistrations<TPushNotification>(applicationId);
            if (registrations == null || !registrations.Any<IPushService>())
                throw new IndexOutOfRangeException("There are no Registered Services that handle this type of Notification");
            foreach (IPushService pushService in registrations)
                pushService.QueueNotification((INotification)notification);
        }

        public IEnumerable<IPushService> GetAllRegistrations()
        {
            lock (this.serviceRegistrationsLock)
                return this.serviceRegistrations.Select<ServiceRegistration, IPushService>((Func<ServiceRegistration, IPushService>)(s => s.Service));
        }

        public IEnumerable<IPushService> GetRegistrations<TNotification>()
        {
            return this.GetRegistrations<TNotification>((string)null);
        }

        public IEnumerable<IPushService> GetRegistrations(string applicationId)
        {
            lock (this.serviceRegistrationsLock)
                return this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(s => s.ApplicationId.Equals(applicationId))).Select<ServiceRegistration, IPushService>((Func<ServiceRegistration, IPushService>)(s => s.Service));
        }

        public IEnumerable<IPushService> GetRegistrations<TNotification>(string applicationId)
        {
            Type type = typeof(TNotification);
            if (string.IsNullOrEmpty(applicationId))
            {
                lock (this.serviceRegistrationsLock)
                    return this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(sr => sr.NotificationType == type)).Select<ServiceRegistration, IPushService>((Func<ServiceRegistration, IPushService>)(sr => sr.Service));
            }
            else
            {
                lock (this.serviceRegistrationsLock)
                    return this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(sr =>
                    {
                        if (!string.IsNullOrEmpty(sr.ApplicationId) && sr.ApplicationId.Equals(applicationId))
                            return sr.NotificationType == type;
                        return false;
                    })).Select<ServiceRegistration, IPushService>((Func<ServiceRegistration, IPushService>)(sr => sr.Service));
            }
        }

        public void StopAllServices(bool waitForQueuesToFinish = true)
        {
            List<ServiceRegistration> source = new List<ServiceRegistration>();
            lock (this.serviceRegistrationsLock)
            {
                source.AddRange((IEnumerable<ServiceRegistration>)this.serviceRegistrations);
                this.serviceRegistrations.Clear();
            }
            source.AsParallel<ServiceRegistration>().ForAll<ServiceRegistration>((Action<ServiceRegistration>)(sr => this.StopService(sr, waitForQueuesToFinish)));
        }

        public void StopAllServices<TNotification>(bool waitForQueuesToFinish = true)
        {
            this.StopAllServices<TNotification>((string)null, waitForQueuesToFinish);
        }

        public void StopAllServices<TNotification>(string applicationId, bool waitForQueuesToFinish = true)
        {
            Type type = typeof(TNotification);
            List<ServiceRegistration> source = new List<ServiceRegistration>();
            lock (this.serviceRegistrationsLock)
            {
                if (string.IsNullOrEmpty(applicationId))
                {
                    IEnumerable<ServiceRegistration> serviceRegistrations = this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(s => s.NotificationType == type));
                    if (serviceRegistrations != null && serviceRegistrations.Any<ServiceRegistration>())
                        source.AddRange(serviceRegistrations);
                    this.serviceRegistrations.RemoveAll((Predicate<ServiceRegistration>)(s => s.NotificationType == type));
                }
                else
                {
                    IEnumerable<ServiceRegistration> serviceRegistrations = this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(s =>
                    {
                        if (s.NotificationType == type)
                            return s.ApplicationId.Equals(applicationId);
                        return false;
                    }));
                    if (serviceRegistrations != null && serviceRegistrations.Any<ServiceRegistration>())
                        source.AddRange(serviceRegistrations);
                    this.serviceRegistrations.RemoveAll((Predicate<ServiceRegistration>)(s =>
                    {
                        if (s.NotificationType == type)
                            return s.ApplicationId.Equals(applicationId);
                        return false;
                    }));
                }
            }
            if (source == null || !source.Any<ServiceRegistration>())
                return;
            source.AsParallel<ServiceRegistration>().ForAll<ServiceRegistration>((Action<ServiceRegistration>)(sr => this.StopService(sr, waitForQueuesToFinish)));
        }

        public void StopAllServices(string applicationId, bool waitForQueuesToFinish = true)
        {
            List<ServiceRegistration> source = new List<ServiceRegistration>();
            lock (this.serviceRegistrationsLock)
            {
                IEnumerable<ServiceRegistration> serviceRegistrations = this.serviceRegistrations.Where<ServiceRegistration>((Func<ServiceRegistration, bool>)(s => s.ApplicationId.Equals(applicationId)));
                if (serviceRegistrations != null && serviceRegistrations.Any<ServiceRegistration>())
                    source.AddRange(serviceRegistrations);
                this.serviceRegistrations.RemoveAll((Predicate<ServiceRegistration>)(s => s.ApplicationId.Equals(applicationId)));
            }
            if (source == null || !source.Any<ServiceRegistration>())
                return;
            source.AsParallel<ServiceRegistration>().ForAll<ServiceRegistration>((Action<ServiceRegistration>)(sr => this.StopService(sr, waitForQueuesToFinish)));
        }

        private void StopService(ServiceRegistration sr, bool waitForQueuesToFinish)
        {
            sr.Service.Stop(waitForQueuesToFinish);
            sr.Service.OnChannelCreated -= this.OnChannelCreated;
            sr.Service.OnChannelDestroyed -= this.OnChannelDestroyed;
            sr.Service.OnChannelException -= this.OnChannelException;
            sr.Service.OnDeviceSubscriptionExpired -= this.OnDeviceSubscriptionExpired;
            sr.Service.OnNotificationFailed -= this.OnNotificationFailed;
            sr.Service.OnNotificationSent -= this.OnNotificationSent;
            sr.Service.OnServiceException -= this.OnServiceException;
            sr.Service.OnDeviceSubscriptionChanged -= this.OnDeviceSubscriptionChanged;
        }

        void IDisposable.Dispose()
        {
            this.StopAllServices(false);
        }
    }
}