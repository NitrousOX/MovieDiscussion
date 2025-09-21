using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HealthMonitoringService
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private CloudTable _healthTable;
        private CloudTable _alertEmailsTable;

        private string _movieDiscussionHealthUrl;
        private string _notificationHealthUrl;
        private string _sendGridApiKey;

        public override bool OnStart()
        {
            Trace.TraceInformation("HealthMonitoringService starting...");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 12;

            // Load configuration settings
            _movieDiscussionHealthUrl = RoleEnvironment.GetConfigurationSettingValue("MovieDiscussionHealthUrl");
            _notificationHealthUrl = RoleEnvironment.GetConfigurationSettingValue("NotificationHealthUrl");
            _sendGridApiKey = RoleEnvironment.GetConfigurationSettingValue("SendGridApiKey");
            string storageConn = RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString");

            // Initialize Azure Table for logging health
            var storageAccount = CloudStorageAccount.Parse(storageConn);
            var tableClient = storageAccount.CreateCloudTableClient();

            _healthTable = tableClient.GetTableReference("HealthCheck");
            _healthTable.CreateIfNotExists();

            // Initialize table for alert emails
            _alertEmailsTable = tableClient.GetTableReference("AlertEmails");
            _alertEmailsTable.CreateIfNotExists();

            Trace.TraceInformation("HealthMonitoringService started.");
            return base.OnStart();
        }

        public override void Run()
        {
            Trace.TraceInformation("HealthMonitoringService is running");
            try
            {
                RunAsync(cancellationTokenSource.Token);
            }
            finally
            {
                runCompleteEvent.Set();
            }
        }

        private void RunAsync(CancellationToken token)
        {
            Trace.TraceInformation("HealthMonitoringService loop started.");

            while (!token.IsCancellationRequested)
            {
                CheckService("MovieDiscussionService", _movieDiscussionHealthUrl);
                CheckService("NotificationService", _notificationHealthUrl);

                try
                {
                    Task.Delay(TimeSpan.FromSeconds(3), token).Wait(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void CheckService(string serviceName, string url)
        {
            bool isAlive = PingService(url);

            // Log status in HealthCheck table
            SaveHealthCheck(serviceName, isAlive);

            // If service is down, send alert emails
            if (!isAlive)
            {
                var emails = GetAlertEmails();
                if (emails.Count > 0 && !string.IsNullOrEmpty(_sendGridApiKey))
                {
                    foreach (var email in emails)
                    {
                        SendAlertEmail(serviceName, email);
                    }
                }
                else
                {
                    Trace.TraceWarning($"Service {serviceName} is DOWN, but no alert emails or SendGrid API key set.");
                }
            }
        }

        private bool PingService(string url)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadString(url);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SaveHealthCheck(string serviceName, bool status)
        {
            var entity = new HealthCheckEntity(serviceName, Guid.NewGuid().ToString())
            {
                Status = status ? "OK" : "NOT_OK",
                CheckTime = DateTime.UtcNow
            };

            var insertOp = TableOperation.Insert(entity);
            _healthTable.Execute(insertOp);

            Trace.TraceInformation($"HealthCheck logged: {serviceName} = {(status ? "OK" : "NOT_OK")}");
        }

        private List<string> GetAlertEmails()
        {
            var emails = new List<string>();

            var query = new TableQuery<AlertEmailEntity>();
            foreach (var entity in _alertEmailsTable.ExecuteQuery(query))
            {
                emails.Add(entity.Email);
            }

            return emails;
        }

        private void SendAlertEmail(string serviceName, string toEmail)
        {
            try
            {
                var client = new SendGridClient(_sendGridApiKey);
                var from = new EmailAddress("ssejmjanovic@gmail.com", "Health Monitor");
                var subject = $"⚠️ {serviceName} is DOWN";
                var to = new EmailAddress(toEmail);
                var plainTextContent = $"The service {serviceName} failed health check at {DateTime.UtcNow}.";
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, null);

                client.SendEmailAsync(msg).Wait();
                Trace.TraceInformation($"Alert email sent to {toEmail} for {serviceName}");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to send alert email to {toEmail}: {ex.Message}");
            }
        }

        public override void OnStop()
        {
            Trace.TraceInformation("HealthMonitoringService stopping...");

            cancellationTokenSource.Cancel();
            runCompleteEvent.WaitOne();

            base.OnStop();
            Trace.TraceInformation("HealthMonitoringService stopped.");
        }
    }

    // Entity class for alert emails
    public class AlertEmailEntity : TableEntity
    {
        public AlertEmailEntity() { }
        public AlertEmailEntity(string email)
        {
            this.PartitionKey = "AlertEmail";
            this.RowKey = email;
            Email = email;
        }
        public string Email { get; set; }
    }
}
