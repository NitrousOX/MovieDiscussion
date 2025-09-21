using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class HealthMonitoringController : Controller
    {
        // GET: /health-monitoring
        [HttpGet]
        [Route("health-monitoring")] // obavezno dodaj, da WorkerRole može pingovati
        public ActionResult Index()
        {
            // Vraća "OK" ili detaljan status servisa
            return Content("OK", "text/plain");
        }
    }
}
