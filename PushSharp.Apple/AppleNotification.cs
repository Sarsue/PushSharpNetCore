using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PushSharp.Core;

namespace PushSharp.Apple
{
    public class AppleNotification : Notification
    {
        private static readonly object nextIdentifierLock = new object();
        private static int nextIdentifier = 0;
        public static readonly DateTime DoNotStore = DateTime.MinValue;
        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const byte MAX_UTF8_SYMBOL_SIZE = 4;
        public const int DEVICE_TOKEN_BINARY_SIZE = 32;
        public const int DEVICE_TOKEN_STRING_SIZE = 64;
        public const int MAX_PAYLOAD_SIZE = 2048;

        public AppleNotification()
        {
            this.DeviceToken = string.Empty;
            this.Payload = new AppleNotificationPayload();
            this.Identifier = AppleNotification.GetNextIdentifier();
        }

        public AppleNotification(string deviceToken)
        {
            if (!string.IsNullOrEmpty(deviceToken) && deviceToken.Length != 64)
                throw new NotificationFailureException(5, this);
            this.DeviceToken = deviceToken;
            this.Payload = new AppleNotificationPayload();
            this.Identifier = AppleNotification.GetNextIdentifier();
        }

        public AppleNotification(string deviceToken, AppleNotificationPayload payload)
        {
            if (!string.IsNullOrEmpty(deviceToken) && deviceToken.Length != 64)
                throw new NotificationFailureException(5, this);
            this.DeviceToken = deviceToken;
            this.Payload = payload;
            this.Identifier = AppleNotification.GetNextIdentifier();
        }

        private static int GetNextIdentifier()
        {
            lock (AppleNotification.nextIdentifierLock)
            {
                if (AppleNotification.nextIdentifier >= 2147483637)
                    AppleNotification.nextIdentifier = 1;
                return AppleNotification.nextIdentifier++;
            }
        }

        public static void ResetIdentifier()
        {
            lock (AppleNotification.nextIdentifierLock)
                AppleNotification.nextIdentifier = 0;
        }

        public int Identifier { get; private set; }

        public string DeviceToken { get; set; }

        public AppleNotificationPayload Payload { get; set; }

        public DateTime? Expiration { get; set; }

        public override bool IsValidDeviceRegistrationId()
        {
            return new Regex("^[0-9A-F]+$", RegexOptions.IgnoreCase).Match(this.DeviceToken).Success;
        }

        public override string ToString()
        {
            try
            {
                if (this.Payload != null)
                    return this.Payload.ToJson();
            }
            catch
            {
            }
            return "{}";
        }

        public byte[] ToBytes()
        {
            byte[] bytes1 = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.Identifier));
            int host = -1;
            DateTime? expiration1 = this.Expiration;
            if ((!expiration1.HasValue ? 1 : (expiration1.GetValueOrDefault() != AppleNotification.DoNotStore ? 1 : 0)) != 0)
            {
                DateTime? expiration2 = this.Expiration;
                host = (int)((!expiration2.HasValue ? DateTime.UtcNow.AddMonths(1) : expiration2.Value).ToUniversalTime() - AppleNotification.UNIX_EPOCH).TotalSeconds;
            }
            byte[] bytes2 = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(host));
            if (string.IsNullOrEmpty(this.DeviceToken))
                throw new NotificationFailureException(2, this);
            if (!this.IsValidDeviceRegistrationId())
                throw new NotificationFailureException(8, this);
            byte[] numArray = new byte[this.DeviceToken.Length / 2];
            for (int index = 0; index < numArray.Length; ++index)
            {
                try
                {
                    numArray[index] = byte.Parse(this.DeviceToken.Substring(index * 2, 2), NumberStyles.HexNumber);
                }
                catch (Exception)
                {
                    throw new NotificationFailureException(8, this);
                }
            }
            if (numArray.Length != 32)
                throw new NotificationFailureException(5, this);
            byte[] bytes3 = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(numArray.Length)));
            byte[] bytes4 = Encoding.UTF8.GetBytes(this.Payload.ToJson());
            if (bytes4.Length > 2048)
            {
                do
                {
                    int num = bytes4.Length - 2048;
                    this.Payload.Alert.Body = this.Payload.Alert.Body.Substring(0, this.Payload.Alert.Body.Length - (num / 4 + (num % 4 != 0 ? 1 : 0)));
                    bytes4 = Encoding.UTF8.GetBytes(this.Payload.ToJson());
                }
                while (bytes4.Length > 2048 && !string.IsNullOrEmpty(this.Payload.Alert.Body));
                if (bytes4.Length > 2048)
                    throw new NotificationFailureException(7, this);
            }
            byte[] bytes5 = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(bytes4.Length)));
            return this.BuildBufferFrom((IList<byte[]>)new List<byte[]>()
      {
        new byte[1]{ (byte) 1 },
        bytes1,
        bytes2,
        bytes3,
        numArray,
        bytes5,
        bytes4
      });
        }

        private byte[] BuildBufferFrom(IList<byte[]> bufferParts)
        {
            int length = 0;
            for (int index = 0; index < bufferParts.Count; ++index)
                length += bufferParts[index].Length;
            byte[] numArray = new byte[length];
            int dstOffset = 0;
            for (int index = 0; index < bufferParts.Count; ++index)
            {
                byte[] bufferPart = bufferParts[index];
                Buffer.BlockCopy((Array)bufferParts[index], 0, (Array)numArray, dstOffset, bufferPart.Length);
                dstOffset += bufferPart.Length;
            }
            return numArray;
        }
    }
}
