using System;
namespace PushSharp.Core
{
    public delegate void DeviceSubscriptionChangedDelegate(object sender, string oldSubscriptionId, string newSubscriptionId, INotification notification);
    public delegate void DeviceSubscriptionExpiredDelegate(object sender, string expiredSubscriptionId, DateTime expirationDateUtc, INotification notification);
    public class DeviceSubscriptonExpiredException : Exception
    {
        public DeviceSubscriptonExpiredException()
          : base("Device Subscription has Expired")
        {
        }
    }
}
