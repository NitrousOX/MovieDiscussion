using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NotificationService
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private CloudQueue _queue;
        private CloudTable _logTable;
        private SendGridClient _sendGridClient;
        private HttpClient _httpClient;

        public override bool OnStart()
        {
            ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 12;

            string storageConn = RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString");
            var storageAccount = CloudStorageAccount.Parse(storageConn);

            _queue = storageAccount.CreateCloudQueueClient().GetQueueReference(
                RoleEnvironment.GetConfigurationSettingValue("NotificationQueueName"));
            _queue.CreateIfNotExists();

            _logTable = storageAccount.CreateCloudTableClient().GetTableReference(
                RoleEnvironment.GetConfigurationSettingValue("NotificationLogTableName"));
            _logTable.CreateIfNotExists();

            string apiKey = RoleEnvironment.GetConfigurationSettingValue("SendGridApiKey");
            _sendGridClient = new SendGridClient(apiKey);

            _httpClient = new HttpClient();

            Trace.TraceInformation("NotificationService initialized successfully.");
            return base.OnStart();
        }

        public override void Run()
        {
            Trace.TraceInformation("NotificationService is running");

            try
            {
                RunAsync(cancellationTokenSource.Token).Wait();
            }
            catch (AggregateException aggEx)
            {
                foreach (var ex in aggEx.InnerExceptions)
                {
                    Trace.TraceError("RunAsync exception: " + ex.Message);
                }
            }
            finally
            {
                runCompleteEvent.Set();
            }
        }


        private async Task RunAsync(CancellationToken token)
        {
            string movieDiscussionUrl = RoleEnvironment.GetConfigurationSettingValue("MovieDiscussionHealthUrl");
            string healthMonitoringUrl = RoleEnvironment.GetConfigurationSettingValue("NotificationHealthUrl");

            while (!token.IsCancellationRequested)
            {
                await CheckServiceAsync("MovieDiscussionService", movieDiscussionUrl);
                await CheckServiceAsync("NotificationService", healthMonitoringUrl);

                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
        }

        private async Task CheckServiceAsync(string serviceName, string url)
        {
            bool isAlive = false;
            try
            {
                var response = await _httpClient.GetAsync(url);
                isAlive = response.IsSuccessStatusCode;
            }
            catch
            {
                isAlive = false;
            }

            await SaveHealthCheckAsync(serviceName, isAlive);

            if (!isAlive)
            {
                Trace.TraceWarning($"{serviceName} is DOWN");

                var to = new EmailAddress("strahinja600@gmail.com", "Strahinja");
                var from = new EmailAddress("alerts@moviediscussion.com", "NotificationService");
                var msg = MailHelper.CreateSingleEmail(
                    from, to, $"⚠️ {serviceName} is DOWN",
                    $"The service {serviceName} failed health check at {DateTime.UtcNow}.", null);

                try
                {
                    await _sendGridClient.SendEmailAsync(msg);
                    Trace.TraceInformation($"Alert email sent for {serviceName}");
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Failed to send alert email: {ex.Message}");
                }
            }
        }

        private async Task SaveHealthCheckAsync(string serviceName, bool status)
        {
            var entity = new DynamicTableEntity(serviceName, Guid.NewGuid().ToString());
            entity.Properties["Status"] = new EntityProperty(status ? "OK" : "NOT_OK");
            entity.Properties["CheckTime"] = new EntityProperty(DateTime.UtcNow);

            var insertOp = TableOperation.Insert(entity);
            await _logTable.ExecuteAsync(insertOp);

            Trace.TraceInformation($"HealthCheck logged: {serviceName} = {(status ? "OK" : "NOT_OK")}");
        }

        public override void OnStop()
        {
            Trace.TraceInformation("NotificationService stopping...");
            cancellationTokenSource.Cancel();
            runCompleteEvent.WaitOne();
            base.OnStop();
            Trace.TraceInformation("NotificationService stopped.");
        }
    }
}
