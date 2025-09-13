using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class CommentEntity : TableEntity
    {
        public CommentEntity() { }

        public CommentEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey; 
            RowKey = rowKey;
        }

        public string UserId { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Likes { get; set; }
        public int Dislikes { get; set; }
    }
}
