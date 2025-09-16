using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace MovieDiscussion.Common.Models
{
    public class FollowEntity : TableEntity
    {
        public FollowEntity() { }
        public FollowEntity(string discussionId, string userId)
        {
            PartitionKey = discussionId; // veza sa diskusijom
            RowKey = userId;             // jedan follow po korisniku
        }

        public bool IsFollowing { get; set; } = true; // uvek true kad je zapraćeno
    }
}
