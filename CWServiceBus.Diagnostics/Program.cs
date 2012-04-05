using System;
using NDesk.Options;
using System.Diagnostics;

namespace CWServiceBus.Diagnostics
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var showHelp = false;
            var install = false;
            var uninstall = false;
            var test = false;
            var p = new OptionSet();
            p.Add("h|help|?", "Displays This", v => showHelp = true);
            p.Add("i|install", "Installs the Perf Counters", v => install = true);
            p.Add("u|uninstall", "Installs the Perf Counters", v => uninstall = true);
            p.Add("t|test", "Tests That The Perf Counters Are Installed", v => test = true);
            p.Parse(args);

            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (install)
            {
                PerformanceCounters.InstallCounters();
                return;
            }

            if (uninstall)
            {
                PerformanceCounters.UninstallCounters();
                return;
            }

            if (test)
            {
                Debug.Assert(PerformanceCounterCategory.Exists(PerformanceCounters.CategoryName), "Performance Counter Category Is NOT Installed");
                Debug.Assert(PerformanceCounterCategory.CounterExists(PerformanceCounters.TotalMessagesReceived, PerformanceCounters.CategoryName), "Performance Counter Is NOT Installed");
                return;
            }
        }
    }
}
