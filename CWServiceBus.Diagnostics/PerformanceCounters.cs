using System;
using System.Diagnostics;
using log4net;

namespace CWServiceBus.Diagnostics
{
    public class PerformanceCounters : IDisposable
    {
        public static ILog logger = LogManager.GetLogger("CWServiceBus.Diagnostics");

        public const string CategoryName = "CWServiceBus";
        public const string TotalMessagesReceived = "Total Messages Received";
        public const string MessagesReceivedRate = "Messages Received / Sec";
        public const string TotalMessagesSent = "Total Messages Sent";
        public const string MessagesSentRate = "Messages Sent / Sec";
        public const string TotalMessageFailures = "Total Message Failures";
        public const string MessageFailureRate = "Message Failures / Sec";
        public const string AvgMessageHandlingDuration = "Average Message Handling Duration";
        public const string AvgMessageHandlingDurationBase = "Average Message Handling Duration Base";

        public static void InstallCounters()
        {
            UninstallCounters();
            if (PerformanceCounterCategory.Exists(CategoryName))
            {
                logger.Error("Logger Still Exists After Attempting to Uninstall for Installation.");
                return;
            }

            var counters = new CounterCreationDataCollection
			{
				new CounterCreationData(TotalMessagesReceived, "Total number of messages received", PerformanceCounterType.NumberOfItems32),
				new CounterCreationData(MessagesReceivedRate, "Rate of message recevied per second", PerformanceCounterType.RateOfCountsPerSecond32),
                new CounterCreationData(TotalMessagesSent, "Total number of messages sent", PerformanceCounterType.NumberOfItems32),
				new CounterCreationData(MessagesSentRate, "Rate of messages sent per second", PerformanceCounterType.RateOfCountsPerSecond32),
                new CounterCreationData(TotalMessageFailures, "Total number of message failures", PerformanceCounterType.NumberOfItems32),
				new CounterCreationData(MessageFailureRate, "Rate of message failures sent per second", PerformanceCounterType.RateOfCountsPerSecond32),
				new CounterCreationData(AvgMessageHandlingDuration, "Average duration for each message handled", PerformanceCounterType.AverageTimer32),
				new CounterCreationData(AvgMessageHandlingDurationBase, "Average duration base for each message handled", PerformanceCounterType.AverageBase),
			};

            PerformanceCounterCategory.Create(CategoryName, "CWServiceBus Messaging Bus", PerformanceCounterCategoryType.MultiInstance, counters);
        }

        public static void UninstallCounters()
        {
            if (PerformanceCounterCategory.Exists(CategoryName))
            {
                PerformanceCounterCategory.Delete(CategoryName);
            }
        }

        public PerformanceCounters(string instanceName)
        {
            instanceName = instanceName.Replace('(', '[')
                                       .Replace(')', ']')
                                       .Replace('#', '_')
                                       .Replace('\\', '_')
                                       .Replace('/', '_');

            if (!PerformanceCounterCategory.Exists(CategoryName))
            {
                logger.WarnFormat("The Performance Counters For Category {0} are not installed. Performance counting will be disabled.", CategoryName);
                _disablePerformanceCounting = true;
            }
            else
            {
                if (PerformanceCounterCategory.CounterExists(TotalMessagesReceived, CategoryName) &&
                    PerformanceCounterCategory.CounterExists(MessagesReceivedRate, CategoryName))
                {
                    this.totalMessagesReceived = new PerformanceCounter(CategoryName, TotalMessagesReceived, instanceName, false);
                    this.messagesReceivedRate = new PerformanceCounter(CategoryName, MessagesReceivedRate, instanceName, false);
                }
                else
                {
                    logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This counter will be disabled.", TotalMessagesReceived, MessagesReceivedRate);
                    _disableMessagesReceived = true;
                }

                if (PerformanceCounterCategory.CounterExists(TotalMessagesSent, CategoryName) &&
                    PerformanceCounterCategory.CounterExists(MessagesSentRate, CategoryName))
                {
                    this.totalMessagesSent = new PerformanceCounter(CategoryName, TotalMessagesSent, instanceName, false);
                    this.messagesSentRate = new PerformanceCounter(CategoryName, MessagesSentRate, instanceName, false);
                }
                else
                {
                    logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This counter will be disabled.", TotalMessagesSent, MessagesSentRate);
                    _disableMessagesSent = true;
                }

                if (PerformanceCounterCategory.CounterExists(TotalMessageFailures, CategoryName) &&
                    PerformanceCounterCategory.CounterExists(MessageFailureRate, CategoryName))
                {
                    this.totalMessageFailures = new PerformanceCounter(CategoryName, TotalMessageFailures, instanceName, false);
                    this.messageFailureRate = new PerformanceCounter(CategoryName, MessageFailureRate, instanceName, false);
                }
                else
                {
                    logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This counter will be disabled.", TotalMessageFailures, MessageFailureRate);
                    _disableMessageFailure = true;
                }

                if (PerformanceCounterCategory.CounterExists(AvgMessageHandlingDuration, CategoryName) &&
                    PerformanceCounterCategory.CounterExists(AvgMessageHandlingDurationBase, CategoryName))
                {
                    this.avgMessageHandlingDuration = new PerformanceCounter(CategoryName, AvgMessageHandlingDuration, instanceName, false);
                    this.avgMessageHandlingDurationBase = new PerformanceCounter(CategoryName, AvgMessageHandlingDurationBase, instanceName, false);
                }
                else
                {
                    logger.WarnFormat("The Performance Counters {0} and {1} are not installed. This counter will be disabled.", AvgMessageHandlingDuration, AvgMessageHandlingDurationBase);
                    _disableAvgMessageHandling = true;
                }
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

        private static bool _disablePerformanceCounting = false;
        private static bool _disableMessagesReceived = false;
        private static bool _disableMessagesSent = false;
        private static bool _disableMessageFailure = false;
        private static bool _disableAvgMessageHandling = false;

        public void OnMessageReceived()
        {
            if (_disablePerformanceCounting || _disableMessagesReceived)
            {
                return;
            }

            this.totalMessagesReceived.Increment();
            this.messagesReceivedRate.Increment();
        }

        public void OnMessageSent()
        {
            if (_disablePerformanceCounting || _disableMessagesSent)
            {
                return;
            }

            this.totalMessagesSent.Increment();
            this.messagesSentRate.Increment();
        }

        public void OnMessageFailure()
        {
            if (_disablePerformanceCounting || _disableMessageFailure)
            {
                return;
            }

            this.totalMessageFailures.Increment();
            this.messageFailureRate.Increment();
        }

        public void OnMessageHandled(long elapsedMilliseconds, long elapsedTicks)
        {
            if (_disablePerformanceCounting || _disableAvgMessageHandling)
            {
                return;
            }
            if (logger.IsDebugEnabled)
            {
                logger.DebugFormat("Logging Elapsed Ticks {0} ticks", elapsedTicks);
            }
            this.avgMessageHandlingDuration.IncrementBy(elapsedTicks);
            this.avgMessageHandlingDurationBase.Increment();
        }

        ~PerformanceCounters()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                try
                {
                    this.totalMessagesReceived.Dispose();
                    this.messagesReceivedRate.Dispose();
                    this.totalMessagesSent.Dispose();
                    this.messagesSentRate.Dispose();
                    this.totalMessageFailures.Dispose();
                    this.messageFailureRate.Dispose();
                    this.avgMessageHandlingDuration.Dispose();
                    this.avgMessageHandlingDurationBase.Dispose();
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public string elapsedticks { get; set; }
    }
}
