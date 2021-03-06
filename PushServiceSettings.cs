﻿using System;
namespace PushSharp.Core
{
    public class PushServiceSettings : IPushServiceSettings
    {
        public PushServiceSettings()
        {
            this.AutoScaleChannels = true;
            this.MaxAutoScaleChannels = 20;
            this.MinAvgTimeToScaleChannels = 100L;
            this.Channels = 1;
            this.MaxNotificationRequeues = 5;
            this.NotificationSendTimeout = 15000;
            this.IdleTimeout = TimeSpan.FromMinutes(5.0);
        }

        public bool AutoScaleChannels { get; set; }

        public int MaxAutoScaleChannels { get; set; }

        public long MinAvgTimeToScaleChannels { get; set; }

        public int Channels { get; set; }

        public int MaxNotificationRequeues { get; set; }

        public int NotificationSendTimeout { get; set; }

        public TimeSpan IdleTimeout { get; set; }
    }
}