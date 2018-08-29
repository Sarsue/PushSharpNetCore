using System;
namespace PushSharp.Core
{
    public abstract class Notification : INotification
    {
        public object Tag { get; set; }

        public DateTime EnqueuedTimestamp { get; set; }

        public int QueuedCount { get; set; }

        public virtual bool IsValidDeviceRegistrationId()
        {
            return true;
        }
    }
}