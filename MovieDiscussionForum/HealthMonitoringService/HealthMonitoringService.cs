using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.WindowsAzure.Storage.Table;

namespace HealthMonitoringService
{
    public class HealthCheckEntity : TableEntity
    {
        public HealthCheckEntity(string serviceName, string rowKey)
        {
            this.PartitionKey = serviceName;   // npr. "MovieDiscussionService"
            this.RowKey = rowKey;             // obično Guid ili timestamp
        }

        public HealthCheckEntity() { }

        public string Status { get; set; }    // "OK" ili "NOT_OK"
        public DateTime CheckTime { get; set; }
    }
}