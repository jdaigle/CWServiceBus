using System.Linq;
using System.Web.Mvc;
using CWServiceBus.Diagnostics;
using System;

namespace PerfMonWeb.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Instances = PerformanceSampler.ListInstances();
            return View();
        }

        public ActionResult Sample()
        {
            var samples = PerformanceSampler.ListInstances().Select(i =>
                new
                {
                    name = i,
                    duration = PerformanceSampler.ForInstance(i).SampleAverageMessageHandlingDuration(),
                    receivedRate = PerformanceSampler.ForInstance(i).SampleMessagesReceivedRate(),
                    sentRate = PerformanceSampler.ForInstance(i).SampleMessagesSentRate(),
                    timeStamp = DateTime.Now,
                }
            ).ToList();
            return Json(samples, JsonRequestBehavior.AllowGet);
        }
    }
}
