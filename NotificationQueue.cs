using System.Collections.Generic;
namespace PushSharp.Core
{
    public class NotificationQueue
    {
        private object lockObj;
        private List<INotification> notifications;

        public NotificationQueue()
        {
            this.notifications = new List<INotification>();
            this.lockObj = new object();
        }

        public void Enqueue(INotification notification)
        {
            lock (this.lockObj)
                this.notifications.Add(notification);
        }

        public void EnqueueAtStart(INotification notification)
        {
            lock (this.lockObj)
                this.notifications.Insert(0, notification);
        }

        public void EnqueueAt(INotification notification, int index = 0)
        {
            lock (this.lockObj)
                this.notifications.Insert(index, notification);
        }

        public INotification Dequeue()
        {
            INotification notification = (INotification)null;
            lock (this.lockObj)
            {
                if (this.notifications.Count > 0)
                {
                    notification = this.notifications[0];
                    this.notifications.RemoveAt(0);
                }
            }
            return notification;
        }

        public int Count
        {
            get
            {
                lock (this.lockObj)
                    return this.notifications.Count;
            }
        }
    }
}