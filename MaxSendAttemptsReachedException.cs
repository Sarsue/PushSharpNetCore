﻿using System;
namespace PushSharp.Core
{
    public class MaxSendAttemptsReachedException : Exception
    {
        public MaxSendAttemptsReachedException()
          : base("The maximum number of Send attempts to send the notification was reached!")
        {
        }
    }
}