using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using HealthStatusService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace HealthStatusService.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var model = new List<HealthCheckViewModel>();

            // Konektovanje na Azure Storage
            var storageAccount = CloudStorageAccount.Parse(
                "UseDevelopmentStorage=true"); // ili connection string iz Web.config

            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference("HealthCheck");

            // Uzimamo podatke iz poslednja 2h
            var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
            var query = new TableQuery<DynamicTableEntity>()
                .Where(TableQuery.GenerateFilterConditionForDate("CheckTime", QueryComparisons.GreaterThanOrEqual, twoHoursAgo));

            var results = table.ExecuteQuery(query);

            foreach (var entity in results)
            {
                model.Add(new HealthCheckViewModel
                {
                    ServiceName = entity.PartitionKey,
                    CheckTime = entity.Properties["CheckTime"].DateTime ?? DateTime.MinValue,
                    Status = entity.Properties["Status"].StringValue
                });
            }

            // Sortiranje po vremenu
            model = model.OrderByDescending(x => x.CheckTime).ToList();

            // Grupisanje po servisu i računanje statistike
            var stats = model
                .GroupBy(x => x.ServiceName)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        int total = g.Count();
                        int up = g.Count(x => x.Status == "Healthy");
                        int down = total - up;
                        double uptimePercent = total > 0 ? (up * 100.0 / total) : 0;
                        return new { Up = up, Down = down, UptimePercent = uptimePercent };
                    }
                );

            // Slanje u ViewBag za graf/percentaže
            ViewBag.Stats = stats;

            return View(model);
        }
    }
}
