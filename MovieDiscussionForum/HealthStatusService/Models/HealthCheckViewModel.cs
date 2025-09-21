using System;

namespace HealthStatusService.Models
{
    public class HealthCheckViewModel
    {
        public string ServiceName { get; set; }
        public DateTime CheckTime { get; set; }
        public string Status { get; set; } // "OK" ili "NOT_OK"
    }
}