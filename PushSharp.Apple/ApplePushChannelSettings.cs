using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using PushSharp.Core;

namespace PushSharp.Apple
{
    public class ApplePushChannelSettings : IPushChannelSettings
    {
        private const string APNS_SANDBOX_HOST = "gateway.sandbox.push.apple.com";
        private const string APNS_PRODUCTION_HOST = "gateway.push.apple.com";
        private const string APNS_SANDBOX_FEEDBACK_HOST = "feedback.sandbox.push.apple.com";
        private const string APNS_PRODUCTION_FEEDBACK_HOST = "feedback.push.apple.com";
        private const int APNS_SANDBOX_PORT = 2195;
        private const int APNS_PRODUCTION_PORT = 2195;
        private const int APNS_SANDBOX_FEEDBACK_PORT = 2196;
        private const int APNS_PRODUCTION_FEEDBACK_PORT = 2196;

        public ApplePushChannelSettings(bool production, string certificateFile, string certificateFilePwd, bool disableCertificateCheck = false)
          : this(production, File.ReadAllBytes(certificateFile), certificateFilePwd, disableCertificateCheck)
        {
        }

        public ApplePushChannelSettings(string certificateFile, string certificateFilePwd, bool disableCertificateCheck = false)
          : this(File.ReadAllBytes(certificateFile), certificateFilePwd, disableCertificateCheck)
        {
        }

        public ApplePushChannelSettings(bool production, byte[] certificateData, string certificateFilePwd, bool disableCertificateCheck = false)
          : this(production, new X509Certificate2(certificateData, certificateFilePwd, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet), disableCertificateCheck)
        {
        }

        public ApplePushChannelSettings(byte[] certificateData, string certificateFilePwd, bool disableCertificateCheck = false)
          : this(new X509Certificate2(certificateData, certificateFilePwd, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet), disableCertificateCheck)
        {
        }

        public ApplePushChannelSettings(X509Certificate2 certificate, bool disableCertificateCheck = false)
        {
            this.Initialize(this.DetectProduction(certificate), certificate, disableCertificateCheck);
        }

        public ApplePushChannelSettings(bool production, X509Certificate2 certificate, bool disableCertificateCheck = false)
        {
            this.Initialize(production, certificate, disableCertificateCheck);
        }

        private void Initialize(bool production, X509Certificate2 certificate, bool disableCertificateCheck)
        {
            this.Host = !production ? "gateway.sandbox.push.apple.com" : "gateway.push.apple.com";
            this.FeedbackHost = !production ? "feedback.sandbox.push.apple.com" : "feedback.push.apple.com";
            this.Port = !production ? 2195 : 2195;
            this.FeedbackPort = !production ? 2196 : 2196;
            this.Certificate = certificate;
            this.MillisecondsToWaitBeforeMessageDeclaredSuccess = 3000;
            this.ConnectionTimeout = 10000;
            this.MaxConnectionAttempts = 3;
            this.FeedbackIntervalMinutes = 10;
            this.FeedbackTimeIsUTC = false;
            this.AdditionalCertificates = new List<X509Certificate2>();
            this.AddLocalAndMachineCertificateStores = false;
            if (!disableCertificateCheck)
                this.CheckProductionCertificateMatching(production);
            this.ValidateServerCertificate = false;
        }

        public bool DetectProduction(X509Certificate2 certificate)
        {
            bool flag = false;
            if (certificate != null && certificate.SubjectName.Name.Contains("Apple Production IOS Push Services"))
                flag = true;
            return flag;
        }

        private void CheckProductionCertificateMatching(bool production)
        {
            if (this.Certificate == null)
                throw new ArgumentNullException("You must provide a Certificate to connect to APNS with!");
            string name1 = this.Certificate.IssuerName.Name;
            string name2 = this.Certificate.SubjectName.Name;
            if (!name1.Contains("Apple"))
                throw new ArgumentException("Your Certificate does not appear to be issued by Apple!  Please check to ensure you have the correct certificate!");
            if (production && !name2.Contains("Apple Production IOS Push Services"))
                throw new ArgumentException("You have selected the Production server, yet your Certificate does not appear to be the Production certificate!  Please check to ensure you have the correct certificate!");
            if (!production && !name2.Contains("Apple Development IOS Push Services") && !name2.Contains("Pass Type ID"))
                throw new ArgumentException("You have selected the Development/Sandbox (Not production) server, yet your Certificate does not appear to be the Development/Sandbox certificate!  Please check to ensure you have the correct certificate!");
        }

        public void OverrideServer(string host, int port)
        {
            this.Host = host;
            this.Port = port;
        }

        public void OverrideFeedbackServer(string host, int port)
        {
            this.FeedbackHost = host;
            this.FeedbackPort = port;
        }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public string FeedbackHost { get; private set; }

        public int FeedbackPort { get; private set; }

        public X509Certificate2 Certificate { get; private set; }

        public List<X509Certificate2> AdditionalCertificates { get; private set; }

        public bool AddLocalAndMachineCertificateStores { get; set; }

        public bool SkipSsl { get; set; }

        public int MillisecondsToWaitBeforeMessageDeclaredSuccess { get; set; }

        public int FeedbackIntervalMinutes { get; set; }

        public bool FeedbackTimeIsUTC { get; set; }

        public bool ValidateServerCertificate { get; set; }

        public int ConnectionTimeout { get; set; }

        public int MaxConnectionAttempts { get; set; }
    }
}
