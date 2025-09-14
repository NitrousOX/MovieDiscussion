using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class DiscussionController : Controller
    {
        // GET: Discussion
        public ActionResult Index()
        {
            return View();
        }
    }
}