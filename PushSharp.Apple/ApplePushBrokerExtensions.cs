using PushSharp.Core;

namespace PushSharp.Apple
{
    public static class ApplePushBrokerExtensions
    {
        public static void RegisterAppleService(this PushBroker broker, ApplePushChannelSettings channelSettings, IPushServiceSettings serviceSettings = null)
        {
            broker.RegisterAppleService(channelSettings, (string)null, serviceSettings);
        }

        public static void RegisterAppleService(this PushBroker broker, ApplePushChannelSettings channelSettings, string applicationId, IPushServiceSettings serviceSettings = null)
        {
            broker.RegisterService<AppleNotification>((IPushService)new ApplePushService(channelSettings, serviceSettings), applicationId, true);
        }

        public static AppleNotification AppleNotification(this PushBroker broker)
        {
            return new AppleNotification();
        }
    }
}
