using System.Web.Mvc;
using System.Web.Routing;

namespace MovieDiscussionService
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // 🟢 Nova ruta za health-monitoring endpoint
            routes.MapRoute(
                name: "HealthMonitoring",
                url: "health-monitoring",
                defaults: new { controller = "HealthMonitoring", action = "Index" }
            );

            // 🟢 Default ruta ostaje kao što je bila
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Account", action = "Login", id = UrlParameter.Optional }
            );
        }
    }
}
