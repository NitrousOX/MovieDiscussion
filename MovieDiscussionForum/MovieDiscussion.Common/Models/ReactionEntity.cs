using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieDiscussion.Common.Models
{
    public class ReactionEntity : TableEntity
    {
        public ReactionEntity() { }

        public ReactionEntity(string discussionId, string userId)
        {
            PartitionKey = discussionId;
            RowKey = userId;
        }

        public ReactionEntity(string discussionId, string userId, bool isPositive)
        {
            PartitionKey = discussionId;
            RowKey = userId;
            IsPositive = isPositive;
        }

        public bool IsPositive { get; set; }
    }

}
