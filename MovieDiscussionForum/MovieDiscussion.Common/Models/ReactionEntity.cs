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
        public ReactionEntity(string discussionId, string userId)
        {
            PartitionKey = discussionId; // reactions tied to discussion
            RowKey = userId;             // one reaction per user
        }

        public bool IsPositive { get; set; }
    }
}
