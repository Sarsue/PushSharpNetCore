using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PushSharp.Apple
{
    public static class TcpExtensions
    {
        public static void SetSocketKeepAliveValues(this TcpClient tcpc, int KeepAliveTime, int KeepAliveInterval)
        {
            uint num = 0;
            byte[] optionInValue = new byte[Marshal.SizeOf((object)num) * 3];
            BitConverter.GetBytes(!true ? 0U : 1U).CopyTo((Array)optionInValue, 0);
            BitConverter.GetBytes((uint)KeepAliveTime).CopyTo((Array)optionInValue, Marshal.SizeOf((object)num));
            BitConverter.GetBytes((uint)KeepAliveInterval).CopyTo((Array)optionInValue, Marshal.SizeOf((object)num) * 2);
            tcpc.Client.IOControl(IOControlCode.KeepAliveValues, optionInValue, (byte[])null);
        }
    }
}
