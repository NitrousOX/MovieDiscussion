using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Table;
using MovieDiscussion.Common;
using MovieDiscussion.Common.Models;
using System.Collections.Generic;
using System.Linq;

namespace MovieDiscussionService.Controllers
{
    public class HomeController : Controller
    {
        public async Task<ActionResult> Index()
        {
            CloudTable discussionsTable = await StorageHelper.GetTableReferenceAsync("Discussions");

            TableQuery<DiscussionEntity> query = new TableQuery<DiscussionEntity>();
            var segment = await discussionsTable.ExecuteQuerySegmentedAsync(query, null);

            var discussions = segment.Results.OrderByDescending(d => d.Timestamp).ToList();
            return View(discussions);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Movie Discussion Forum - Discuss movies, share opinions, and comment.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Contact support at support@moviediscussion.com";
            return View();
        }
    }
}
