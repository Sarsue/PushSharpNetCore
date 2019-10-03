using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
namespace PushSharp.Apple
{
    public class FeedbackService
    {
        public event FeedbackService.FeedbackReceivedDelegate OnFeedbackReceived;

        public event FeedbackService.FeedbackExceptionDelegate OnFeedbackException;

        public void RaiseFeedbackReceived(string deviceToken, DateTime timestamp)
        {
            FeedbackService.FeedbackReceivedDelegate feedbackReceived = this.OnFeedbackReceived;
            if (feedbackReceived == null)
                return;
            feedbackReceived(deviceToken, timestamp);
        }

        public void RaiseFeedbackException(Exception ex)
        {
            FeedbackService.FeedbackExceptionDelegate feedbackException = this.OnFeedbackException;
            if (feedbackException == null)
                return;
            feedbackException(ex);
        }

        public void Run(ApplePushChannelSettings settings)
        {
            try
            {
                this.Run(settings, new CancellationTokenSource().Token);
            }
            catch (Exception ex)
            {
                this.RaiseFeedbackException(ex);
            }
        }

        public void Run(ApplePushChannelSettings settings, CancellationToken cancelToken)
        {
            Encoding ascii = Encoding.ASCII;
            X509Certificate2 certificate = settings.Certificate;
            X509CertificateCollection clientCertificates = new X509CertificateCollection();
            clientCertificates.Add((X509Certificate)certificate);
            TcpClient tcpClient = new TcpClient(settings.FeedbackHost, settings.FeedbackPort);
            SslStream sslStream = new SslStream((Stream)tcpClient.GetStream(), true, (RemoteCertificateValidationCallback)((sender, cert, chain, sslErrs) => true), (LocalCertificateSelectionCallback)((sender, targetHost, localCerts, remoteCert, acceptableIssuers) => (X509Certificate)certificate));
            sslStream.AuthenticateAsClient(settings.FeedbackHost, clientCertificates, SslProtocols.Tls, false);
            byte[] buffer = new byte[38];
            DateTime dateTime = DateTime.Now.AddYears(-1);
            for (int index = sslStream.Read(buffer, 0, buffer.Length); index > 0; index = sslStream.Read(buffer, 0, buffer.Length))
            {
                if (!cancelToken.IsCancellationRequested)
                {
                    try
                    {
                        byte[] numArray1 = new byte[4];
                        byte[] numArray2 = new byte[32];
                        Array.Copy((Array)buffer, 0, (Array)numArray1, 0, 4);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse((Array)numArray1);
                        DateTime timestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)BitConverter.ToInt32(numArray1, 0));
                        if (!settings.FeedbackTimeIsUTC)
                            timestamp = timestamp.ToLocalTime();
                        Array.Copy((Array)buffer, 6, (Array)numArray2, 0, 32);
                        string deviceToken = BitConverter.ToString(numArray2).Replace("-", string.Empty).ToLower().Trim();
                        if (deviceToken.Length == 64)
                        {
                            if (timestamp > dateTime)
                                this.RaiseFeedbackReceived(deviceToken, timestamp);
                        }
                    }
                    catch
                    {
                    }
                    Array.Clear((Array)buffer, 0, buffer.Length);
                }
                else
                    break;
            }
            try
            {
                sslStream.Close();
                sslStream.Dispose();
            }
            catch
            {
            }
            try
            {
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Client.Dispose();
            }
            catch
            {
            }
            try
            {
                tcpClient.Close();
            }
            catch
            {
            }
        }

        public delegate void FeedbackReceivedDelegate(string deviceToken, DateTime timestamp);

        public delegate void FeedbackExceptionDelegate(Exception ex);
    }
}
