using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using SendGrid;

namespace NotificationService
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private CloudQueue _queue;
        private CloudTable _logTable;
        private SendGridClient _sendGridClient;

        public override void Run()
        {
            Trace.TraceInformation("NotificationService is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Use TLS 1.2 for Service Bus connections
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.


            try
            {
                string storageConn = RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString");
                var storageAccount = CloudStorageAccount.Parse(storageConn);

                string queueName = RoleEnvironment.GetConfigurationSettingValue("NotificationQueueName");
                var queueClient = storageAccount.CreateCloudQueueClient();
                _queue = queueClient.GetQueueReference(queueName);
                _queue.CreateIfNotExists();

                string tableName = RoleEnvironment.GetConfigurationSettingValue("NotificationLogTableName");
                var tableClient = storageAccount.CreateCloudTableClient();
                _logTable = tableClient.GetTableReference(tableName);
                _logTable.CreateIfNotExists();

                string apiKey = RoleEnvironment.GetConfigurationSettingValue("SendGridApiKey");
                _sendGridClient = new SendGridClient(apiKey);

                Trace.TraceInformation("NotificationService initialized successfully.");
            }
            catch (Exception e)
            {
                Trace.TraceError("Error during OnStart initialization: " + e.Message);
                throw; // ako pukne, Azure ce pokusati restart
            }


            bool result = base.OnStart();

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("NotificationService is stopping");

            try
            {
                this.cancellationTokenSource.Cancel();
                this.runCompleteEvent.WaitOne();

                _queue = null;
                _logTable = null;
                _sendGridClient = null;

                Trace.TraceInformation("NotificationService resources released.");
            }
            catch (Exception e)
            {
                Trace.TraceError("Error while stopping NotificationService: " + e.Message);
            }
            finally
            {
                base.OnStop();

                Trace.TraceInformation("NotificationService has stopped");
            }
            
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            
            while (!cancellationToken.IsCancellationRequested)
            {

                try
                {
                    var msg = await _queue.GetMessageAsync();

                    if(msg != null)
                    {
                        Trace.TraceInformation("Message received: " + msg.AsString);

                        // TODO:
                        // 1. Preuzeti detalje komentara iz baze
                        // 2. Naci pretplacene korisnike
                        // 3. Poslati mejlove
                        // 4. Logovati u tabelu

                        // Ako je sve proslo OK, obrisi poruku iz queue-a
                        await _queue.DeleteMessageAsync(msg);

                    }
                    else
                    {
                        // Ako nema poruka, malo odspavaj
                        await Task.Delay(3000, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError("Error in RunSync: " + e.Message);
                    await Task.Delay(5000, cancellationToken);
                }


                
            }
        }
    }
}
