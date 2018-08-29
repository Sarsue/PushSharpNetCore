using System;
namespace PushSharp.Core
{
    public interface ILogger
    {
        void Debug(string format, params object[] objs);

        void Info(string format, params object[] objs);

        void Warning(string format, params object[] objs);

        void Error(string format, params object[] objs);
    }
    public interface INotification
    {
        object Tag { get; set; }

        int QueuedCount { get; set; }

        bool IsValidDeviceRegistrationId();

        DateTime EnqueuedTimestamp { get; set; }
    }
    public interface IPushChannel : IDisposable
    {
        void SendNotification(INotification notification, SendNotificationCallbackDelegate callback);
    }
    public interface IPushChannelFactory
    {
        IPushChannel CreateChannel(IPushChannelSettings channelSettings);
    }
    public interface IPushChannelSettings
    {
    }
}