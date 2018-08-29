
using System;
namespace PushSharp.Core
{
    public class NotificationRequeueEventArgs : EventArgs
    {
        public NotificationRequeueEventArgs(INotification notification, Exception cause)
        {
            this.Cancel = false;
            this.Notification = notification;
            this.RequeueCause = cause;
        }

        public bool Cancel { get; set; }

        public INotification Notification { get; private set; }

        public Exception RequeueCause { get; private set; }
    }
}