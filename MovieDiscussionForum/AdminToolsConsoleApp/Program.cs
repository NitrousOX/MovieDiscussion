using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using MovieDiscussion.Common.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

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
            if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
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

                case "remove-alert":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: remove-alert <email>");
                        return;
                    }
                    await RemoveAlertAsync(args[1]);
                    break;

                case "update-alert":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: update-alert <oldEmail> <newEmail>");
                        return;
                    }
                    await UpdateAlertAsync(args[1], args[2]);
                    break;

                case "list-alerts":
                    await ListAlertsAsync();
                    break;

                case "list-notifications":
                    {
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
            Console.WriteLine("  verify-user <email>              Marks a user as Author (IsAuthor = true) in Users table");
            Console.WriteLine("  add-alert <email>                Adds a new alert email");
            Console.WriteLine("  remove-alert <email>             Removes an alert email");
            Console.WriteLine("  update-alert <oldEmail> <newEmail> Updates an existing alert email");
            Console.WriteLine("  list-alerts                      Lists all alert emails");
            Console.WriteLine("  list-notifications [--failed]    Lists recent notification logs (use --failed to filter)");
        }

        static CloudTable GetTable(CloudTableClient client, string tableName)
        {
            var table = client.GetTableReference(tableName);
            table.CreateIfNotExists();
            return table;
        }

        static async Task VerifyUserAsync(string email)
        {
            var conn = ConfigurationManager.ConnectionStrings["StorageConnectionString"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
            {
                Console.WriteLine("StorageConnectionString not found in App.config.");
                return;
            }

            var account = CloudStorageAccount.Parse(conn);
            var tableClient = account.CreateCloudTableClient();
            var usersTable = GetTable(tableClient, UsersTableName);

            var retrieve = TableOperation.Retrieve<UserEntity>(email, email);
            var retrieveResult = await usersTable.ExecuteAsync(retrieve);
            var user = retrieveResult.Result as UserEntity;

            if (user == null)
            {
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

            user.IsAuthor = true;
            var upsert = TableOperation.InsertOrMerge(user);
            await usersTable.ExecuteAsync(upsert);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"User marked as Author: {email}");
            Console.ResetColor();
        }

        // === ALERT MANAGEMENT ===
        static async Task<CloudTable> GetAlertsTableAsync()
        {
            var conn = ConfigurationManager.ConnectionStrings["StorageConnectionString"]?.ConnectionString;
            var account = CloudStorageAccount.Parse(conn);
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(AlertsTableName);
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

        static async Task UpdateAlertAsync(string oldEmail, string newEmail)
        {
            var table = await GetAlertsTableAsync();
            var retrieve = TableOperation.Retrieve<AlertEntity>("Alert", oldEmail);
            var result = await table.ExecuteAsync(retrieve);
            var oldEntity = result.Result as AlertEntity;

            if (oldEntity == null)
            {
                Console.WriteLine("Not found: " + oldEmail);
                return;
            }

            var newEntity = new AlertEntity(newEmail)
            {
                CreatedAt = oldEntity.CreatedAt
            };

            await table.ExecuteAsync(TableOperation.InsertOrReplace(newEntity));
            await table.ExecuteAsync(TableOperation.Delete(oldEntity));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Alert email updated: {oldEmail} -> {newEmail}");
            Console.ResetColor();
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
            foreach (var alert in alerts.OrderBy(a => a.CreatedAt))
            {
                Console.WriteLine($"- {alert.RowKey} (added {alert.CreatedAt})");
            }
        }

        // === NOTIFICATION LOGS ===
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

            query = query.Take(100);

            var token = default(TableContinuationToken);
            var all = new List<NotificationLogEntity>();

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

            foreach (var log in all.OrderByDescending(x => x.SentAt))
            {
                var time = log.SentAt.ToString("u");
                var line = $"{time} | {log.Status,-7} | {log.Email,-30} | disc={log.PartitionKey} | comm={log.CommentId}";
                if (!string.IsNullOrWhiteSpace(log.ErrorMessage))
                    line += $" | error={log.ErrorMessage}";
                Console.WriteLine(line);
            }
        }
    }
}
