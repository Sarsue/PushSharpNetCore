using System;
namespace PushSharp.Apple
{
    public class ConnectionFailureException : Exception
    {
        public ConnectionFailureException(string msg, Exception innerException)
          : base(msg, innerException)
        {
        }
    }
}
