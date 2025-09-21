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
using SendGrid.Helpers.Mail;
using MovieDiscussion.Common.Models;

namespace NotificationService
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private CloudQueue _queue;
        private CloudTable _logTable;
        private SendGridClient _sendGridClient;
        private CloudTable _commentsTable;
        private CloudTable _followersTable;

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

                // Comments table (citamo komentare po poruci iz queue-a)
                _commentsTable = tableClient.GetTableReference("Comments");
                _commentsTable.CreateIfNotExists();

                // Pretplate korisnika na diskusije
                _followersTable = tableClient.GetTableReference("Follows");
                _followersTable.CreateIfNotExists();

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

                    if (msg != null)
                    {
                        Trace.TraceInformation("Message received: " + msg.AsString);

                        // TODO:
                        // 1. Preuzeti detalje komentara iz baze
                        await ProcessMessageAsync(msg.AsString);

                        // Ako je sve proslo OK, obrisi poruku iz queue-a
                        await _queue.DeleteMessageAsync(msg);
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

                }


            }
        }
        

        private async Task ProcessMessageAsync(string commentId)
        {
            if (string.IsNullOrWhiteSpace(commentId))
            {
                Trace.TraceWarning("ProcessMessageAsync: empty commentId");
                return;
            }

            // 1) Pronadji komentar u tabeli Comments po RowKey = commentId
            var filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, commentId);
            var querry = new TableQuery<CommentEntity>().Where(filter).Take(1);

            var segment = await _commentsTable.ExecuteQuerySegmentedAsync(querry, token: null);
            var comment = segment.Results.FirstOrDefault();

            if (comment == null)
            {
                Trace.TraceWarning($"ProcessMessageAsync: Comment not found for RowKey={commentId}");
                return;
            }


            // 2) Za sledece korake nam je bitan discussionId (PartitionKey)
            var discussionId = comment.PartitionKey;

            Trace.TraceInformation($"Loaded comment. DiscussionId={discussionId}, CommentId={commentId}, UserId={comment.UserId}, PostedAt={comment.PostedAt:u}");

            // 3) Pronadji followere iz tabele Follows
            var followers = await GetFollowersAsync(discussionId);

            if(followers.Count == 0)
            {
                Trace.TraceInformation($"No followers found for discussion {discussionId}");
                return;
            }

            Trace.TraceInformation($"Found {followers.Count} followers for discussion {discussionId}");

            // Slanje mejlova
            foreach (var f in followers)
            {
                string email = f.RowKey;    //Row key = email korisnika
                string subject = $"New comment in discussion {discussionId}";
                string body = $"User {comment.UserId} posted: {comment.Text} at {comment.PostedAt:u}";

                try
                {
                    await SendEmailAsync(email, subject, body);
                    await LogNotificationsAsync(discussionId, commentId, f.RowKey, email, "Sent");
                }
                catch (Exception e)
                {
                    await LogNotificationsAsync(discussionId, commentId, f.RowKey, email, "Failed", e.Message);
                }
                
            }
        }
                   
        private async Task<List<FollowEntity>> GetFollowersAsync(string discussionId)
        {
            var followers = new List<FollowEntity>();

            var filter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, discussionId);
            var query = new TableQuery<FollowEntity>().Where(filter);

            TableContinuationToken token = null;

            do
            {
                var segment = await _followersTable.ExecuteQuerySegmentedAsync(query, token);
                followers.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null);

            return followers.Where(f => f.IsFollowing).ToList();
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Trace.TraceWarning("SendEmailAsync: empty email, skipping.");
                return;
            }

            // Koristimo dummy posiljaoca noreply@moviediscussion.com
            var from = new EmailAddress("ssejmjanovic@gmail.com", "MovieDiscussion Notifications");
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, body, body);

            try
            {
                var response = await _sendGridClient.SendEmailAsync(msg);
                Trace.TraceInformation($"Email sent to {toEmail}, status {response.StatusCode}");
            }
            catch (Exception e)
            {
                Trace.TraceError($"SendEmailAsync error for {toEmail}: {e.Message}");
            }
        }

        private async Task LogNotificationsAsync(string discussionId, string commentId, string userId, string email, string status, string errorMessage = null)
        {
            var logId = Guid.NewGuid().ToString();
            var log = new NotificationLogEntity(discussionId, logId)
            {
                CommentId = commentId,
                UserId = userId,
                Email = email,
                SentAt = DateTime.UtcNow,
                Status = status,
                ErrorMessage = errorMessage
            };

            var insert = TableOperation.Insert(log);
            await _logTable.ExecuteAsync(insert);

            Trace.TraceInformation($"Notification logged: {email}, status={status}");


                
            
        }
    }
}
