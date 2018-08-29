using System;
namespace PushSharp.Core
{
    public delegate void ChannelCreatedDelegate(object sender, IPushChannel pushChannel);
    public delegate void ChannelDestroyedDelegate(object sender);
    public delegate void ChannelExceptionDelegate(object sender, IPushChannel pushChannel, Exception error);
    public delegate void NotificationFailedDelegate(object sender, INotification notification, Exception error);
    public delegate void NotificationRequeueDelegate(object sender, NotificationRequeueEventArgs e);
    public delegate void NotificationSentDelegate(object sender, INotification notification);
    public delegate void PushChannelExceptionDelegate(object sender, Exception ex);
    public delegate void SendNotificationCallbackDelegate(object sender, SendNotificationResult result);
    public delegate void ServiceExceptionDelegate(object sender, Exception error);

}
