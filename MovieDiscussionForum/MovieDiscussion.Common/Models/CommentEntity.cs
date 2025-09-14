using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class CommentEntity : TableEntity
    {
        public CommentEntity() { }

        public CommentEntity(string discussionId, string commentId)
        {
            PartitionKey = discussionId; // group comments by discussion
            RowKey = commentId;          // Guid.NewGuid().ToString()
        }

        public string UserId { get; set; }
        public string Text { get; set; }
        public DateTime PostedAt { get; set; }
    }
}
