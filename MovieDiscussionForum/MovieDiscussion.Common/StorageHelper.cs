using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading.Tasks;

namespace MovieDiscussion.Common
{
    public static class StorageHelper
    {
        private static readonly CloudStorageAccount storageAccount;

        static StorageHelper()
        {
            string connectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "StorageConnectionString is missing in ServiceConfiguration (.cscfg).");
            }

            storageAccount = CloudStorageAccount.Parse(connectionString);
        }

        // Queue
        public static async Task<CloudQueue> GetQueueReferenceAsync(string queueName)
        {
            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
            return queue;
        }

        // Table
        public static async Task<CloudTable> GetTableReferenceAsync(string tableName)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync();
            return table;
        }

        // Blob
        public static async Task<CloudBlobContainer> GetBlobContainerReferenceAsync(string containerName)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }
    }
}

