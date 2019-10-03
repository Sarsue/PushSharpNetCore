using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
namespace PushSharp.Apple
{
    public class AppleNotificationPayload
    {
        public AppleNotificationPayload()
        {
            this.HideActionButton = false;
            this.Alert = new AppleNotificationAlert();
            this.CustomItems = new Dictionary<string, object[]>();
        }

        public AppleNotificationPayload(string alert)
        {
            this.HideActionButton = false;
            this.Alert = new AppleNotificationAlert()
            {
                Body = alert
            };
            this.CustomItems = new Dictionary<string, object[]>();
        }

        public AppleNotificationPayload(string alert, int badge)
        {
            this.HideActionButton = false;
            this.Alert = new AppleNotificationAlert()
            {
                Body = alert
            };
            this.Badge = new int?(badge);
            this.CustomItems = new Dictionary<string, object[]>();
        }

        public AppleNotificationPayload(string alert, int badge, string sound)
        {
            this.HideActionButton = false;
            this.Alert = new AppleNotificationAlert()
            {
                Body = alert
            };
            this.Badge = new int?(badge);
            this.Sound = sound;
            this.CustomItems = new Dictionary<string, object[]>();
        }

        public AppleNotificationPayload(string alert, int badge, string sound, string category)
          : this(alert, badge, sound)
        {
            this.Category = category;
        }

        public AppleNotificationAlert Alert { get; set; }

        public int? ContentAvailable { get; set; }

        public int? Badge { get; set; }

        public string Sound { get; set; }

        public bool HideActionButton { get; set; }

        public string Category { get; set; }

        public Dictionary<string, object[]> CustomItems { get; private set; }

        public void AddCustom(string key, params object[] values)
        {
            if (values == null)
                return;
            this.CustomItems.Add(key, values);
        }

        public string ToJson()
        {
            JObject jobject1 = new JObject();
            JObject jobject2 = new JObject();
            if (!this.Alert.IsEmpty)
            {
                if (!string.IsNullOrEmpty(this.Alert.Body) && string.IsNullOrEmpty(this.Alert.LocalizedKey) && string.IsNullOrEmpty(this.Alert.ActionLocalizedKey) && ((this.Alert.LocalizedArgs == null || this.Alert.LocalizedArgs.Count <= 0) && (string.IsNullOrEmpty(this.Alert.LaunchImage) && !this.HideActionButton)))
                {
                    jobject2.Add("alert", (JToken)new JValue(this.Alert.Body));
                }
                else
                {
                    JObject jobject3 = new JObject();
                    if (!string.IsNullOrEmpty(this.Alert.LocalizedKey))
                        jobject3.Add("loc-key", (JToken)new JValue(this.Alert.LocalizedKey));
                    if (this.Alert.LocalizedArgs != null && this.Alert.LocalizedArgs.Count > 0)
                        jobject3.Add("loc-args", (JToken)new JArray(this.Alert.LocalizedArgs.ToArray()));
                    if (!string.IsNullOrEmpty(this.Alert.Body))
                        jobject3.Add("body", (JToken)new JValue(this.Alert.Body));
                    if (this.HideActionButton)
                        jobject3.Add("action-loc-key", (JToken)new JValue((string)null));
                    else if (!string.IsNullOrEmpty(this.Alert.ActionLocalizedKey))
                        jobject3.Add("action-loc-key", (JToken)new JValue(this.Alert.ActionLocalizedKey));
                    if (!string.IsNullOrEmpty(this.Alert.LaunchImage))
                        jobject3.Add("launch-image", (JToken)new JValue(this.Alert.LaunchImage));
                    jobject2.Add("alert", (JToken)jobject3);
                }
            }
            if (this.Badge.HasValue)
                jobject2.Add("badge", (JToken)new JValue((long)this.Badge.Value));
            if (!string.IsNullOrEmpty(this.Sound))
                jobject2.Add("sound", (JToken)new JValue(this.Sound));
            if (this.ContentAvailable.HasValue)
            {
                jobject2.Add("content-available", (JToken)new JValue((long)this.ContentAvailable.Value));
                if (string.IsNullOrEmpty(this.Sound))
                    jobject2.Add("sound", (JToken)new JValue(string.Empty));
            }
            if (!string.IsNullOrEmpty(this.Category))
                jobject2.Add("category", (JToken)new JValue(this.Category));
            if (((JContainer)jobject2).Count > 0)
                jobject1.Add("aps", (JToken)jobject2);
            foreach (string key in this.CustomItems.Keys)
            {
                if (this.CustomItems[key].Length == 1)
                {
                    object obj = this.CustomItems[key][0];
                    jobject1.Add(key, !(obj is JToken) ? (JToken)new JValue(obj) : (JToken)obj);
                }
                else if (this.CustomItems[key].Length > 1)
                    jobject1.Add(key, (JToken)new JArray(this.CustomItems[key]));
            }
            string str = ((JToken)jobject1).ToString((Formatting)0, (JsonConverter[])null);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char ch in str)
            {
                if (ch < ' ' || ch > '\x007F')
                    stringBuilder.Append("\\u" + string.Format("{0:x4}", (object)Convert.ToUInt32(ch)));
                else
                    stringBuilder.Append(ch);
            }
            return str;
        }

        public override string ToString()
        {
            return this.ToJson();
        }
    }
}
