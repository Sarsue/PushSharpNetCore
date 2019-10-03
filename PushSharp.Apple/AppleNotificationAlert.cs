using System.Collections.Generic;

namespace PushSharp.Apple
{
    public class AppleNotificationAlert
    {
        public AppleNotificationAlert()
        {
            this.Body = (string)null;
            this.ActionLocalizedKey = (string)null;
            this.LocalizedKey = (string)null;
            this.LocalizedArgs = new List<object>();
            this.LaunchImage = (string)null;
        }

        public string Body { get; set; }

        public string ActionLocalizedKey { get; set; }

        public string LocalizedKey { get; set; }

        public List<object> LocalizedArgs { get; set; }

        public void AddLocalizedArgs(params object[] values)
        {
            this.LocalizedArgs.AddRange((IEnumerable<object>)values);
        }

        public string LaunchImage { get; set; }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(this.Body) && string.IsNullOrEmpty(this.ActionLocalizedKey) && string.IsNullOrEmpty(this.LocalizedKey) && ((this.LocalizedArgs == null || this.LocalizedArgs.Count <= 0) && string.IsNullOrEmpty(this.LaunchImage));
            }
        }
    }
}
