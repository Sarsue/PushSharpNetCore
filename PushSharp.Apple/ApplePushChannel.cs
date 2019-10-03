using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using PushSharp.Core;

namespace  PushSharp.Apple
{
    public class ApplePushChannel : IPushChannel, IDisposable
    {
        private CancellationTokenSource cancelTokenSrc = new CancellationTokenSource();
        private List<SentNotification> sentNotifications = new List<SentNotification>();
        private readonly object sentLock = new object();
        private readonly object connectLock = new object();
        private readonly object streamWriteLock = new object();
        private int reconnectDelay = 3000;
        private float reconnectBackoffMultiplier = 1.5f;
        private byte[] readBuffer = new byte[6];
        private const string hostSandbox = "gateway.sandbox.push.apple.com";
        private const string hostProduction = "gateway.push.apple.com";
        private const int initialReconnectDelay = 3000;
        private CancellationToken cancelToken;
        private ApplePushChannelSettings appleSettings;
        private int cleanupSync;
        private Timer timerCleanup;
        private int cleanedUp;
        private int reconnects;
        private int connectionAttemptCounter;
        private volatile bool connected;
        private X509Certificate certificate;
        private X509CertificateCollection certificates;
        private TcpClient client;
        private SslStream stream;
        private Stream networkStream;
        private long trackedNotificationCount;
        private IAsyncResult readAsyncResult;
        private IAsyncResult connectAsyncResult;

        public ApplePushChannel(ApplePushChannelSettings channelSettings)
        {
            this.cancelToken = this.cancelTokenSrc.Token;
            this.appleSettings = channelSettings;
            this.certificate = (X509Certificate)this.appleSettings.Certificate;
            this.certificates = new X509CertificateCollection();
            if (this.appleSettings.AddLocalAndMachineCertificateStores)
            {
                this.certificates.AddRange((X509CertificateCollection)new X509Store(StoreLocation.LocalMachine).Certificates);
                this.certificates.AddRange((X509CertificateCollection)new X509Store(StoreLocation.CurrentUser).Certificates);
            }
            this.certificates.Add(this.certificate);
            if (this.appleSettings.AdditionalCertificates != null)
            {
                foreach (X509Certificate additionalCertificate in this.appleSettings.AdditionalCertificates)
                    this.certificates.Add(additionalCertificate);
            }
            this.timerCleanup = new Timer((TimerCallback)(state => this.Cleanup()), (object)null, TimeSpan.FromMilliseconds(1000.0), TimeSpan.FromMilliseconds(1000.0));
        }

        public event ApplePushChannel.ConnectingDelegate OnConnecting;

        public event ApplePushChannel.ConnectedDelegate OnConnected;

        public event ApplePushChannel.ConnectionFailureDelegate OnConnectionFailure;

        public event ApplePushChannel.WaitBeforeReconnectDelegate OnWaitBeforeReconnect;

        public event PushChannelExceptionDelegate OnException;

        public void SendNotification(INotification notification, SendNotificationCallbackDelegate callback)
        {
            lock (this.sentLock)
            {
                Interlocked.Increment(ref this.trackedNotificationCount);
                AppleNotification notification1 = notification as AppleNotification;
                bool flag = true;
                byte[] numArray = new byte[0];
                byte[] bytes;
                try
                {
                    bytes = notification1.ToBytes();
                }
                catch (NotificationFailureException ex)
                {
                    flag = false;
                    Interlocked.Decrement(ref this.trackedNotificationCount);
                    if (callback == null)
                        return;
                    callback((object)this, new SendNotificationResult(notification, false, (Exception)ex));
                    return;
                }
                try
                {
                    lock (this.connectLock)
                        this.Connect();
                    lock (this.streamWriteLock)
                    {
                        if (!this.client.Connected || !this.client.Client.Poll(0, SelectMode.SelectWrite) || !this.networkStream.CanWrite)
                            throw new ObjectDisposedException("Connection to APNS is not Writable");
                        this.networkStream.Write(bytes, 0, bytes.Length);
                        this.networkStream.Flush();
                        this.sentNotifications.Add(new SentNotification(notification1)
                        {
                            Callback = callback
                        });
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    this.connected = false;
                    Interlocked.Decrement(ref this.trackedNotificationCount);
                    Log.Error("Exception during APNS Send: {0} -> {1}", (object)notification1.Identifier, (object)ex);
                    bool shouldRequeue = true;
                    if (callback == null)
                        return;
                    callback((object)this, new SendNotificationResult(notification, shouldRequeue, ex));
                }
            }
        }

        public void Dispose()
        {
            if (this.cancelToken.IsCancellationRequested)
                return;
            Log.Info("ApplePushChannel->Waiting...");
            int num = 0;
            lock (this.sentLock)
                num = this.sentNotifications.Count;
            while (num > 0 || Interlocked.Read(ref this.trackedNotificationCount) > 0L)
            {
                this.Cleanup();
                Thread.Sleep(100);
                lock (this.sentLock)
                    num = this.sentNotifications.Count;
            }
            this.Cleanup();
            this.timerCleanup.Change(-1, -1);
            this.cancelTokenSrc.Cancel();
            Log.Info("ApplePushChannel: Cleaned up {0}, Reconnects: {1}", (object)this.cleanedUp, (object)this.reconnects);
            Log.Info("ApplePushChannel->DISPOSE.");
        }

        private void Reader()
        {
            try
            {
                this.readAsyncResult = this.networkStream.BeginRead(this.readBuffer, 0, 6, (AsyncCallback)(asyncResult =>
                {
                    if (this.readAsyncResult != asyncResult)
                        return;
                    lock (this.sentLock)
                    {
                        try
                        {
                            if (this.networkStream.EndRead(asyncResult) > 0)
                            {
                                this.disconnect();
                                this.HandleFailedNotification(IPAddress.NetworkToHostOrder(BitConverter.ToInt32(this.readBuffer, 2)), this.readBuffer[1]);
                                this.Reader();
                            }
                            else
                                this.connected = false;
                        }
                        catch
                        {
                            this.connected = false;
                        }
                    }
                }), (object)null);
            }
            catch
            {
                this.connected = false;
            }
        }

        private void HandleFailedNotification(int identifier, byte status)
        {
            int index = this.sentNotifications.FindIndex((Predicate<SentNotification>)(n => n.Identifier == identifier));
            if (index < 0)
                return;
            Log.Info("Failed Notification: {0}", (object)identifier);
            if (index > 0)
            {
                this.sentNotifications.GetRange(0, index).ForEach((Action<SentNotification>)(n =>
                {
                    Interlocked.Decrement(ref this.trackedNotificationCount);
                    if (n.Callback == null)
                        return;
                    n.Callback((object)this, new SendNotificationResult((INotification)n.Notification, false, (Exception)null));
                }));
                this.sentNotifications.RemoveRange(0, index);
            }
            SentNotification failedNotification = this.sentNotifications[0];
            Interlocked.Decrement(ref this.trackedNotificationCount);
            if (failedNotification.Callback != null)
                failedNotification.Callback((object)this, new SendNotificationResult((INotification)failedNotification.Notification, false, (Exception)new NotificationFailureException((int)status, failedNotification.Notification)));
            this.sentNotifications.RemoveAt(0);
            this.sentNotifications.Reverse();
            this.sentNotifications.ForEach((Action<SentNotification>)(n =>
            {
                Interlocked.Decrement(ref this.trackedNotificationCount);
                if (failedNotification.Callback == null)
                    return;
                failedNotification.Callback((object)this, new SendNotificationResult((INotification)n.Notification, true, new Exception("Sent after previously failed Notification."))
                {
                    CountsAsRequeue = false
                });
            }));
            this.sentNotifications.Clear();
        }

        private void Cleanup()
        {
            int num = -1;
            try
            {
                num = Interlocked.CompareExchange(ref this.cleanupSync, 1, 0);
                if (num != 0)
                    return;
                bool flag;
                do
                {
                    lock (this.connectLock)
                    {
                        try
                        {
                            this.Connect();
                        }
                        catch (Exception ex)
                        {
                            PushChannelExceptionDelegate onException = this.OnException;
                            if (onException != null)
                                onException((object)this, ex);
                        }
                    }
                    flag = false;
                    lock (this.sentLock)
                    {
                        if (this.sentNotifications.Count > 0)
                        {
                            if (this.connected)
                            {
                                SentNotification sentNotification = this.sentNotifications[0];
                                if (sentNotification.SentAt < DateTime.UtcNow.AddMilliseconds((double)(-1 * this.appleSettings.MillisecondsToWaitBeforeMessageDeclaredSuccess)))
                                {
                                    flag = true;
                                    Interlocked.Decrement(ref this.trackedNotificationCount);
                                    if (sentNotification.Callback != null)
                                        sentNotification.Callback((object)this, new SendNotificationResult((INotification)sentNotification.Notification, false, (Exception)null));
                                    this.sentNotifications.RemoveAt(0);
                                    Interlocked.Increment(ref this.cleanedUp);
                                }
                                else
                                    flag = false;
                            }
                            else
                            {
                                try
                                {
                                    this.sentNotifications[0].SentAt = DateTime.UtcNow;
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
                while (flag);
            }
            finally
            {
                if (num == 0)
                    this.cleanupSync = 0;
            }
        }

        private void Connect()
        {
            while (!this.connected && !this.cancelToken.IsCancellationRequested)
            {
                ++this.connectionAttemptCounter;
                try
                {
                    this.connect();
                    this.connected = true;
                    this.connectionAttemptCounter = 0;
                }
                catch (ConnectionFailureException ex)
                {
                    this.connected = false;
                    ApplePushChannel.ConnectionFailureDelegate connectionFailure = this.OnConnectionFailure;
                    if (connectionFailure != null)
                        connectionFailure(ex);
                    PushChannelExceptionDelegate onException = this.OnException;
                    if (onException != null)
                        onException((object)this, (Exception)ex);
                }
                if (!this.connected && this.connectionAttemptCounter >= this.appleSettings.MaxConnectionAttempts)
                    throw new ConnectionFailureException(string.Format("Maximum number of attempts ({0}) to connect to {1}:{2} was reached!", (object)this.appleSettings.MaxConnectionAttempts, (object)this.appleSettings.Host, (object)this.appleSettings.Port), (Exception)new TimeoutException());
                if (!this.connected)
                {
                    ApplePushChannel.WaitBeforeReconnectDelegate waitBeforeReconnect = this.OnWaitBeforeReconnect;
                    if (waitBeforeReconnect != null)
                        waitBeforeReconnect(this.reconnectDelay);
                    int num = 0;
                    while (num <= this.reconnectDelay && !this.cancelToken.IsCancellationRequested)
                    {
                        Thread.Sleep(250);
                        num += 250;
                    }
                    this.reconnectDelay = (int)((double)this.reconnectBackoffMultiplier * (double)this.reconnectDelay);
                }
                else
                {
                    this.reconnectDelay = 3000;
                    ApplePushChannel.ConnectedDelegate onConnected = this.OnConnected;
                    if (onConnected != null)
                        onConnected(this.appleSettings.Host, this.appleSettings.Port);
                }
            }
        }

        private void connect()
        {
            if (this.client != null)
                this.disconnect();
            this.client = new TcpClient();
            ApplePushChannel.ConnectingDelegate onConnecting = this.OnConnecting;
            if (onConnecting != null)
                onConnecting(this.appleSettings.Host, this.appleSettings.Port);
            try
            {
                AutoResetEvent connectDone = new AutoResetEvent(false);
                this.connectAsyncResult = this.client.BeginConnect(this.appleSettings.Host, this.appleSettings.Port, (AsyncCallback)(ar =>
                {
                    if (this.connectAsyncResult != ar)
                        return;
                    try
                    {
                        this.client.EndConnect(ar);
                        try
                        {
                            this.client.SetSocketKeepAliveValues(1200000, 30000);
                        }
                        catch
                        {
                        }
                        Interlocked.Increment(ref this.reconnects);
                        connectDone.Set();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("APNS Connect Callback Failed: " + (object)ex);
                    }
                }), (object)this.client);
                if (!connectDone.WaitOne(this.appleSettings.ConnectionTimeout))
                    throw new TimeoutException("Connection to Host Timed Out!");
            }
            catch (Exception ex)
            {
                throw new ConnectionFailureException("Connection to Host Failed", ex);
            }
            if (this.appleSettings.SkipSsl)
            {
                this.networkStream = (Stream)this.client.GetStream();
            }
            else
            {
                RemoteCertificateValidationCallback userCertificateValidationCallback;
                if (this.appleSettings != null && this.appleSettings.ValidateServerCertificate)
                {
                    userCertificateValidationCallback = new RemoteCertificateValidationCallback(ApplePushChannel.ValidateRemoteCertificate);

                }
                else
                    userCertificateValidationCallback = (RemoteCertificateValidationCallback)((sender, cert, chain, sslPolicyErrors) => true);
                this.stream = new SslStream((Stream)this.client.GetStream(), false, userCertificateValidationCallback, (LocalCertificateSelectionCallback)((sender, targetHost, localCerts, remoteCert, acceptableIssuers) => this.certificate));
                try
                {
                    this.stream.AuthenticateAsClient(this.appleSettings.Host, this.certificates, SslProtocols.Tls, false);
                }
                catch (AuthenticationException ex)
                {
                    throw new ConnectionFailureException("SSL Stream Failed to Authenticate as Client", (Exception)ex);
                }
                if (!this.stream.IsMutuallyAuthenticated)
                    throw new ConnectionFailureException("SSL Stream Failed to Authenticate", (Exception)null);
                if (!this.stream.CanWrite)
                    throw new ConnectionFailureException("SSL Stream is not Writable", (Exception)null);
                this.networkStream = (Stream)this.stream;
            }
            this.Reader();
        }

        private void disconnect()
        {
            try
            {
                this.stream.Close();
            }
            catch
            {
            }
            try
            {
                this.stream.Dispose();
            }
            catch
            {
            }
            try
            {
                this.client.Client.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            try
            {
                this.client.Client.Dispose();
            }
            catch
            {
            }
            try
            {
                this.client.Close();
            }
            catch
            {
            }
            this.client = (TcpClient)null;
            this.stream = (SslStream)null;
        }

        private static bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors policyErrors)
        {
            return policyErrors == SslPolicyErrors.None;
        }

        public delegate void ConnectingDelegate(string host, int port);

        public delegate void ConnectedDelegate(string host, int port);

        public delegate void ConnectionFailureDelegate(ConnectionFailureException exception);

        public delegate void WaitBeforeReconnectDelegate(int millisecondsToWait);
    }
}
