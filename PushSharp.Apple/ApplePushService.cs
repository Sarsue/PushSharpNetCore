using System;
using System.Threading;
using PushSharp.Core;

namespace PushSharp.Apple
{
    public class ApplePushService : PushServiceBase
    {
        private FeedbackService feedbackService;
        private CancellationTokenSource cancelTokenSource;
        private Timer timerFeedback;

        public ApplePushService(ApplePushChannelSettings channelSettings)
          : this((IPushChannelFactory)null, channelSettings, (IPushServiceSettings)null)
        {
        }

        public ApplePushService(ApplePushChannelSettings channelSettings, IPushServiceSettings serviceSettings)
          : this((IPushChannelFactory)null, channelSettings, serviceSettings)
        {
        }

        public ApplePushService(IPushChannelFactory pushChannelFactory, ApplePushChannelSettings channelSettings)
          : this(pushChannelFactory, channelSettings, (IPushServiceSettings)null)
        {
        }

        public ApplePushService(IPushChannelFactory pushChannelFactory, ApplePushChannelSettings channelSettings, IPushServiceSettings serviceSettings)
          : base(pushChannelFactory ?? (IPushChannelFactory)new ApplePushChannelFactory(), (IPushChannelSettings)channelSettings, serviceSettings)
        {
            ApplePushService applePushService = this;
            ApplePushChannelSettings pushChannelSettings = channelSettings;
            this.cancelTokenSource = new CancellationTokenSource();
            if (pushChannelSettings.FeedbackIntervalMinutes > 0)
            {
                this.feedbackService = new FeedbackService();
                this.feedbackService.OnFeedbackReceived += new FeedbackService.FeedbackReceivedDelegate(this.feedbackService_OnFeedbackReceived);
                this.feedbackService.OnFeedbackException += (FeedbackService.FeedbackExceptionDelegate)(ex => applePushService.RaiseServiceException(ex));
                if (this.timerFeedback == null)
                    this.timerFeedback = new Timer((TimerCallback)(state =>
                    {
                        try
                        {
                            applePushService.feedbackService.Run(channelSettings, applePushService.cancelTokenSource.Token);
                        }
                        catch (Exception ex)
                        {
                            applePushService.RaiseServiceException(ex);
                        }
                    }), (object)null, TimeSpan.FromSeconds(10.0), TimeSpan.FromMinutes((double)pushChannelSettings.FeedbackIntervalMinutes));
            }
            this.ServiceSettings.MaxAutoScaleChannels = 20;
        }

        private void feedbackService_OnFeedbackReceived(string deviceToken, DateTime timestamp)
        {
            this.RaiseSubscriptionExpired(deviceToken, timestamp.ToUniversalTime(), (INotification)null);
        }

        public override bool BlockOnMessageResult
        {
            get
            {
                return false;
            }
        }
    }
}
