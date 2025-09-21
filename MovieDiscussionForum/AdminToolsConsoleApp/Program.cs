using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AdminToolsConsoleApp
{
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
