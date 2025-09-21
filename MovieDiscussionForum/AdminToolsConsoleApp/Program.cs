using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MovieDiscussion.Common.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Configuration;

namespace AdminToolsConsoleApp
{
    internal class Program
    {
        private const string UsersTableName = "Users";
        private const string AlertsTableName = "Alerts";
        private const string NotificationLogsTableName = "NotificationLogs";

        static void Main(string[] args)
        {
            try
            {
                Run(args).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Fatal error: " + e.Message);
                Console.ResetColor();
            }
        }

        static async Task Run(string[] args)
        {
            if(args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                return;
            }

            var cmd = args[0].ToLowerInvariant();

            switch (cmd)
            {
                case "verify-user":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: verify-user <email>");
                        return;
                    }
                    await VerifyUserAsync(args[1]);
                    break;
                case "add-alert":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: add-alert <email>");
                        return;
                    }
                    await AddAlertAsync(args[1]);
                    break;
                case "remvoe-alert":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: remove-alert <email>");
                        return;
                    }
                    await RemoveAlertAsync(args[1]);
                    break;
                case "list-alerts":
                    await ListAlertsAsync();
                    break;
                case "list-notifications":
                    {
                        // Podržimo opcioni flag --failed
                        bool failedOnly = args.Any(a => a.Equals("--failed", StringComparison.OrdinalIgnoreCase));
                        await ListNotificationsAsync(failedOnly);
                        break;
                    }

                default:
                    Console.WriteLine("Unknown command.");
                    PrintHelp();
                    break;
            }

        }

        static void PrintHelp()
        {
            Console.WriteLine("AdminToolsConsoleApp commands:");
            Console.WriteLine("  verify-user <email>    Marks a user as Author (IsAuthor = true) in Users table");
            Console.WriteLine("  list-notifications [--failed]   Lists recent notification logs (use --failed to filter)");

        }

        static CloudTable GetTable(CloudTableClient client, string tableName)
        {
            var table = client.GetTableReference(tableName);
            table.CreateIfNotExists();
            return table;
        }

        static async Task VerifyUserAsync(string email)
        {
            // 1) Storage konekcija
            var conn = ConfigurationManager.ConnectionStrings["StorageConnectionString"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
            {
                Console.WriteLine("StorageConnectionString not found in App.config.");
                return;
            }

            var account = CloudStorageAccount.Parse(conn);
            var tableClient = account.CreateCloudTableClient();
            var usersTable = GetTable(tableClient, UsersTableName);

            // 2) Probaj vise nacina da nadjes korisnika jer ne znamo tacnu semu (robustno):
            // 2a) Pokusaj Retreive sa (PartitionKey=emai, RowKey=email)
            var retreive = TableOperation.Retrieve<UserEntity>(email, email);
            var retreiveResult = await usersTable.ExecuteAsync(retreive);
            var user = retreiveResult.Result as UserEntity;

            if(user == null)
            {
                //2b) Ako nije nadjen, probaj query po RowKey == email (radi i ako je PartitionKey nesto drugo)
                var filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, email);
                var query = new TableQuery<UserEntity>().Where(filter).Take(1);
                var segment = await usersTable.ExecuteQuerySegmentedAsync(query, token: null);
                user = segment.Results.FirstOrDefault();
            }

            if (user == null)
            {
                Console.WriteLine($"User not found: {email}");
                return;
            }

            // 3) Obelezi kao autora
            user.IsAuthor = true;

            // 4) Upis (InsertOrMerge je najbezbedniji jer zadrzava ostala polja)
            var upsert = TableOperation.InsertOrMerge(user);
            await usersTable.ExecuteAsync(upsert);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"User marked as Author: {email}");
            Console.ResetColor();
        }

        // ALERT MANAGEMENT
        static async Task<CloudTable> GetAlertsTableAsync()
        {
            var conn = ConfigurationManager.ConnectionStrings["StorageConnectionString"]?.ConnectionString;
            var account = CloudStorageAccount.Parse(conn);
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference("Alerts");
            await table.CreateIfNotExistsAsync();
            return table;
        }

        static async Task AddAlertAsync(string email)
        {
            var table = await GetAlertsTableAsync();
            var entity = new AlertEntity(email);
            var insert = TableOperation.InsertOrReplace(entity);
            await table.ExecuteAsync(insert);

            Console.WriteLine($"Alert email added: {email}");
        }

        static async Task RemoveAlertAsync(string email)
        {
            var table = await GetAlertsTableAsync();
            var retrieve = TableOperation.Retrieve<AlertEntity>("Alert", email);
            var result = await table.ExecuteAsync(retrieve);
            var entity = result.Result as AlertEntity;
            if (entity == null)
            {
                Console.WriteLine("Not found: " + email);
                return;
            }

            await table.ExecuteAsync(TableOperation.Delete(entity));
            Console.WriteLine($"Alert email removed: {email}");
        }

        static async Task ListAlertsAsync()
        {
            var table = await GetAlertsTableAsync();
            var query = new TableQuery<AlertEntity>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Alert")
            );

            TableContinuationToken token = null;
            var alerts = new List<AlertEntity>();

            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                alerts.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null);

            if (alerts.Count == 0)
            {
                Console.WriteLine("No alert emails found.");
                return;
            }

            Console.WriteLine("Alert emails:");
            foreach (var alert in alerts)
            {
                Console.WriteLine($"- {alert.RowKey} (added {alert.CreatedAt})");
            }
        }

        static async Task<CloudTable> GetNotificationLogsTableAsync()
        {
            var conn = ConfigurationManager.ConnectionStrings["StorageConnectionString"]?.ConnectionString;
            var account = CloudStorageAccount.Parse(conn);
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(NotificationLogsTableName);
            await table.CreateIfNotExistsAsync();
            return table;
        }

        static async Task ListNotificationsAsync(bool failedOnly = false)
        {
            var table = await GetNotificationLogsTableAsync();

            // Query: ako je --failed, filtriramo Status == "Failed", inače sve
            TableQuery<NotificationLogEntity> query;

            if (failedOnly)
            {
                var statusFilter = TableQuery.GenerateFilterCondition("Status", QueryComparisons.Equal, "Failed");
                query = new TableQuery<NotificationLogEntity>().Where(statusFilter);
            }
            else
            {
                query = new TableQuery<NotificationLogEntity>();
            }

            // (Opcionalno) ograniči izlaz, npr. top 100
            query = query.Take(100);

            var token = default(TableContinuationToken);
            var all = new System.Collections.Generic.List<NotificationLogEntity>();

            do
            {
                var segment = await table.ExecuteQuerySegmentedAsync(query, token);
                all.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null && all.Count < 100);

            if (all.Count == 0)
            {
                Console.WriteLine(failedOnly ? "No FAILED notifications." : "No notifications found.");
                return;
            }

            // Sortiraj po vremenu (najskorije prve)
            foreach (var log in all.OrderByDescending(x => x.SentAt))
            {
                // Format prikaza: vreme | status | email | discussion | comment | error?
                var time = log.SentAt.ToString("u"); // UTC format
                var line = $"{time} | {log.Status,-7} | {log.Email,-30} | disc={log.PartitionKey} | comm={log.CommentId}";
                if (!string.IsNullOrWhiteSpace(log.ErrorMessage))
                    line += $" | error={log.ErrorMessage}";
                Console.WriteLine(line);
            }
    public class AlertEmailEntity : TableEntity
    {
        public string Email { get; set; }

        public AlertEmailEntity() { }

        public AlertEmailEntity(string rowKey, string email)
        {
            PartitionKey = "Alerts";
            RowKey = rowKey;
            Email = email;
        }
    }

    class Program
    {
        private static CloudTable _table;

        static async Task Main(string[] args)
        {
            // Inicijalizacija Azure Table
            var storageConn = "UseDevelopmentStorage=true"; // ili pravi Azure Storage Connection String
            var storageAccount = CloudStorageAccount.Parse(storageConn);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference("AlertEmails");
            await _table.CreateIfNotExistsAsync();

            Console.WriteLine("=== Admin Tools for Alert Emails ===");

            bool running = true;
            while (running)
            {
                Console.WriteLine("\n1. List emails");
                Console.WriteLine("2. Add email");
                Console.WriteLine("3. Delete email");
                Console.WriteLine("4. Exit");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        await ListEmails();
                        break;
                    case "2":
                        await AddEmail();
                        break;
                    case "3":
                        await DeleteEmail();
                        break;
                    case "4":
                        running = false;
                        break;
                }
            }
        }

        private static async Task ListEmails()
        {
            var query = new TableQuery<AlertEmailEntity>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Alerts")
            );

            var emails = await _table.ExecuteQuerySegmentedAsync(query, null);
            Console.WriteLine("Current alert emails:");
            foreach (var e in emails.Results)
            {
                Console.WriteLine($"{e.RowKey}: {e.Email}");
            }
        }

        private static async Task AddEmail()
        {
            Console.Write("Enter new email: ");
            var email = Console.ReadLine();
            var rowKey = Guid.NewGuid().ToString();
            var entity = new AlertEmailEntity(rowKey, email);
            await _table.ExecuteAsync(TableOperation.Insert(entity));
            Console.WriteLine("Email added!");
        }

        private static async Task DeleteEmail()
        {
            Console.Write("Enter Email of address to delete: ");
            var email = Console.ReadLine();

            var query = new TableQuery<AlertEmailEntity>()
                .Where(TableQuery.GenerateFilterCondition("Email", QueryComparisons.Equal, email));

            var result = await _table.ExecuteQuerySegmentedAsync(query, null);

            if (result.Results.Count > 0)
            {
                foreach (var entity in result.Results)
                {
                    await _table.ExecuteAsync(TableOperation.Delete(entity));
                }
                Console.WriteLine("Email deleted!");
            }
            else
            {
                Console.WriteLine("Email not found.");
            }
        }
    }
}
