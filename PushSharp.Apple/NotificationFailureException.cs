using System;
namespace PushSharp.Apple
{
    public class NotificationFailureException : Exception
    {
        public NotificationFailureException(int errorStatusCode, AppleNotification notification)
        {
            this.ErrorStatusCode = errorStatusCode;
            this.Notification = notification;
        }

        public AppleNotification Notification { get; set; }

        public int ErrorStatusCode { get; set; }

        public string ErrorStatusDescription
        {
            get
            {
                string empty = string.Empty;
                switch (this.ErrorStatusCode)
                {
                    case 0:
                        return "No errors encountered";
                    case 1:
                        return "Processing error";
                    case 2:
                        return "Missing device token";
                    case 3:
                        return "Missing topic";
                    case 4:
                        return "Missing payload";
                    case 5:
                        return "Invalid token size";
                    case 6:
                        return "Invaid topic size";
                    case 7:
                        return "Invalid payload size";
                    case 8:
                        return "Invalid token";
                    case (int)byte.MaxValue:
                        return "None (unknown)";
                    default:
                        return "Undocumented error status code";
                }
            }
        }

        public override string ToString()
        {
            string empty = string.Empty;
            if (this.Notification != null)
                empty = this.Notification.ToString();
            return string.Format("APNS NotificationFailureException -> {0} : {1} -> {2}", (object)this.ErrorStatusCode, (object)this.ErrorStatusDescription, (object)empty);
        }
    }
}
