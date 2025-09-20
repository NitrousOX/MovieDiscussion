using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieDiscussion.Common.Models
{
    public class NotificationLogEntity : TableEntity
    {
        public NotificationLogEntity() { }

        public NotificationLogEntity(string discussionId, string logId)
        {
            this.PartitionKey = discussionId;
            this.RowKey = logId;
        }

        public string CommentId { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
        public DateTime SentAt { get; set; }
        public string Status { get; set; }

        public string ErrorMessage { get; set; }
    }
}
