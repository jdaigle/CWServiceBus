using System.Collections.Generic;
using System.Diagnostics;
using log4net;

namespace CWServiceBus.Diagnostics
{
    public class PerformanceSampler
    {
        private static ILog logger = LogManager.GetLogger("CWServiceBus.Diagnostics");

        static PerformanceSampler()
        {
            if (!PerformanceCounterCategory.Exists(PerformanceCounters.CategoryName))
            {
                logger.WarnFormat("The Performance Counters For Category {0} are not installed. Performance sampling will be disabled.", PerformanceCounters.CategoryName);
                _disablePerformanceSampling = true;
            }
            else
            {
                category = new PerformanceCounterCategory(PerformanceCounters.CategoryName);
            }
        }

        public static IEnumerable<string> ListInstances()
        {
            if (_disablePerformanceSampling)
            {
                return new string[0];
            }
            return category.GetInstanceNames();
        }

        private static Dictionary<string, PerformanceSampler> samplers = new Dictionary<string, PerformanceSampler>();
        
        public static PerformanceSampler ForInstance(string instanceName)
        {
            if (samplers.ContainsKey(instanceName))
            {
                return samplers[instanceName];
            }
            lock (samplers)
            {
                if (samplers.ContainsKey(instanceName))
                {
                    return samplers[instanceName];
                }
                samplers[instanceName] = new PerformanceSampler(instanceName);
                return samplers[instanceName];
            }
        }

        private PerformanceSampler(string instanceName)
        {
            if (_disablePerformanceSampling)
            {
                return;
            }

            if (PerformanceCounterCategory.CounterExists(PerformanceCounters.TotalMessagesReceived, PerformanceCounters.CategoryName) &&
                    PerformanceCounterCategory.CounterExists(PerformanceCounters.MessagesReceivedRate, PerformanceCounters.CategoryName))
            {
                this.totalMessagesReceived = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.TotalMessagesReceived, instanceName, true);
                this.messagesReceivedRate = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.MessagesReceivedRate, instanceName, true);
            }
            else
            {
                logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This sampler will be disabled.", PerformanceCounters.TotalMessagesReceived, PerformanceCounters.MessagesReceivedRate);
                _disableMessagesReceived = true;
            }

            if (PerformanceCounterCategory.CounterExists(PerformanceCounters.TotalMessagesSent, PerformanceCounters.CategoryName) &&
                PerformanceCounterCategory.CounterExists(PerformanceCounters.MessagesSentRate, PerformanceCounters.CategoryName))
            {
                this.totalMessagesSent = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.TotalMessagesSent, instanceName, true);
                this.messagesSentRate = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.MessagesSentRate, instanceName, true);
            }
            else
            {
                logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This sampler will be disabled.", PerformanceCounters.TotalMessagesSent, PerformanceCounters.MessagesSentRate);
                _disableMessagesSent = true;
            }

            if (PerformanceCounterCategory.CounterExists(PerformanceCounters.TotalMessageFailures, PerformanceCounters.CategoryName) &&
                PerformanceCounterCategory.CounterExists(PerformanceCounters.MessageFailureRate, PerformanceCounters.CategoryName))
            {
                this.totalMessageFailures = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.TotalMessageFailures, instanceName, true);
                this.messageFailureRate = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.MessageFailureRate, instanceName, true);
            }
            else
            {
                logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This sampler will be disabled.", PerformanceCounters.TotalMessageFailures, PerformanceCounters.MessageFailureRate);
                _disableMessageFailure = true;
            }

            if (PerformanceCounterCategory.CounterExists(PerformanceCounters.AvgMessageHandlingDuration, PerformanceCounters.CategoryName) &&
                PerformanceCounterCategory.CounterExists(PerformanceCounters.AvgMessageHandlingDurationBase, PerformanceCounters.CategoryName))
            {
                this.avgMessageHandlingDuration = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.AvgMessageHandlingDuration, instanceName, true);
                this.avgMessageHandlingDurationBase = new PerformanceCounter(PerformanceCounters.CategoryName, PerformanceCounters.AvgMessageHandlingDurationBase, instanceName, true);
            }
            else
            {
                logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This sampler will be disabled.", PerformanceCounters.AvgMessageHandlingDuration, PerformanceCounters.AvgMessageHandlingDurationBase);
                _disableAvgMessageHandling = true;
            }
        }

        private readonly PerformanceCounter totalMessagesReceived;
        private readonly PerformanceCounter messagesReceivedRate;
        private readonly PerformanceCounter totalMessagesSent;
        private readonly PerformanceCounter messagesSentRate;
        private readonly PerformanceCounter totalMessageFailures;
        private readonly PerformanceCounter messageFailureRate;
        private readonly PerformanceCounter avgMessageHandlingDuration;
        private readonly PerformanceCounter avgMessageHandlingDurationBase;

        private static PerformanceCounterCategory category;
        private static bool _disablePerformanceSampling = false;
        private static bool _disableMessagesReceived = false;
        private static bool _disableMessagesSent = false;
        private static bool _disableMessageFailure = false;
        private static bool _disableAvgMessageHandling = false;

        public float SampleTotalMessagesReceived()
        {
            if (_disablePerformanceSampling || _disableMessagesReceived)
            {
                return 0;
            }
            return totalMessagesReceived.NextValue();
        }

        public float SampleMessagesReceivedRate()
        {
            if (_disablePerformanceSampling || _disableMessagesReceived)
            {
                return 0;
            }
            return messagesReceivedRate.NextValue();
        }

        public float SampleTotalMessagesSent()
        {
            if (_disablePerformanceSampling || _disableMessagesSent)
            {
                return 0;
            }
            return totalMessagesSent.NextValue();
        }

        public float SampleMessagesSentRate()
        {
            if (_disablePerformanceSampling || _disableMessagesSent)
            {
                return 0;
            }
            return messagesSentRate.NextValue();
        }

        public float SampleTotalMessageFailures()
        {
            if (_disablePerformanceSampling || _disableMessageFailure)
            {
                return 0;
            }
            return totalMessageFailures.NextValue();
        }

        public float SampleMessageFailureRate()
        {
            if (_disablePerformanceSampling || _disableMessageFailure)
            {
                return 0;
            }
            return messagesReceivedRate.NextValue();
        }

        public float SampleAverageMessageHandlingDuration()
        {
            if (_disablePerformanceSampling || _disableAvgMessageHandling)
            {
                return 0;
            }
            return avgMessageHandlingDuration.NextValue();
        }
    }
}
