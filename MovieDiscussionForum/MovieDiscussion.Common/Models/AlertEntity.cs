using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieDiscussion.Common.Models
{
    public class AlertEntity : TableEntity
    {
        public AlertEntity() { }

        public AlertEntity(string email)
        {
            PartitionKey = "Alert";   // svi idu u istu particiju
            RowKey = email;           // ključ = email adresa
            CreatedAt = DateTime.UtcNow;
        }

        public DateTime CreatedAt { get; set; }
    }
}
