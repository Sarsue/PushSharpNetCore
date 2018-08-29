using System;

namespace PushSharp.Core
{
    public interface IPushServiceSettings
    {
        bool AutoScaleChannels { get; set; }

        int MaxAutoScaleChannels { get; set; }

        long MinAvgTimeToScaleChannels { get; set; }

        int Channels { get; set; }

        int MaxNotificationRequeues { get; set; }

        int NotificationSendTimeout { get; set; }

        TimeSpan IdleTimeout { get; set; }
    }
}