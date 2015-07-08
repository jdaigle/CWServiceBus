using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CWServiceBus.Diagnostics
{
    internal class Metrics
    {
        static Metrics()
        {
            MetricsCollectionEnabled = false;
            bool.TryParse(ConfigurationManager.AppSettings["metrics_enabled"], out MetricsCollectionEnabled);
            var host = ConfigurationManager.AppSettings["metrics_host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "127.0.0.1";
            }
            var port = 8125;
            int.TryParse(ConfigurationManager.AppSettings["metrics_port"], out port);
            Default = new Metrics(host, port);
            if (MetricsCollectionEnabled)
            {
                AsyncMetricAppender.Start();
            }
        }

        public static readonly Metrics Default;
        public static readonly bool MetricsCollectionEnabled ;

        private Metrics(string host, int port)
        {
            udpClient = new UdpClient(host, port);
        }

        private readonly UdpClient udpClient;

        public void AddTiming(string key, long time, double sampleRate = 1)
        {
            if (!MetricsCollectionEnabled) { return; }
            var packet = key + ":" + time.ToString("F0") + "|ms";
            if (sampleRate != 1)
            {
                packet += "@" + sampleRate.ToString("F4");
            }
            Append(packet);
        }

        public void SetGauge(string key, long value)
        {
            if (!MetricsCollectionEnabled) { return; }
            var packet = key + ":" + value.ToString("F0") + "|g";
            Append(packet);
        }

        public void Increment(string key, long value = 1)
        {
            if (!MetricsCollectionEnabled) { return; }
            var packet = key + ":" + value.ToString("F0") + "|c";
            Append(packet);
        }

        private void Append(string packet)
        {
            if (!MetricsCollectionEnabled) { return; }
            AsyncMetricAppender.Append(packet);
        }

        /// <summary>
        /// Sends the packet, immediately
        /// </summary>
        public void SendPacket(string packet)
        {
            if (!MetricsCollectionEnabled) { return; }
            var buffer = Encoding.ASCII.GetBytes(packet);
            var bytesSent = udpClient.Send(buffer, buffer.Length);
        }

        public Batch SendBatch()
        {
            return new Batch(this);
        }

        public class Batch
        {
            public Batch(Metrics metrics)
            {
                this.metrics = metrics;
            }

            StringBuilder packetBuilder = new StringBuilder();
            Metrics metrics;

            public Batch AddTiming(string key, long time, double sampleRate = 1)
            {
                var packet = key + ":" + time.ToString("F0") + "|ms";
                if (sampleRate != 1)
                {
                    packet += "@" + sampleRate.ToString("F4");
                }
                packetBuilder.Append(packet).Append("\n");
                return this;
            }

            public Batch SetGauge(string key, long value)
            {
                var packet = key + ":" + value.ToString("F0") + "|g";
                packetBuilder.Append(packet).Append("\n");
                return this;
            }

            public Batch Increment(string key, long value = 1)
            {
                var packet = key + ":" + value.ToString("F0") + "|c";
                packetBuilder.Append(packet).Append("\n");
                return this;
            }

            public void Send()
            {
                metrics.Append(packetBuilder.ToString());
            }
        }

        public static class AsyncMetricAppender
        {
            // TODO: this might not be the most performant compared to a ring buffer. But it's simple.
            private static readonly ConcurrentQueue<string> pendingPackets = new ConcurrentQueue<string>();
            private static readonly AutoResetEvent queueNotifier = new AutoResetEvent(false);

            public static void Start()
            {
                Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        try
                        {
                            string packet = null;
                            while (!pendingPackets.TryDequeue(out packet))
                            {
                                queueNotifier.WaitOne();
                            }
                            if (packet != null)
                            {
                                Metrics.Default.SendPacket(packet);
                            }
                        }
                        catch (Exception)
                        {
                            // what to do with the exception?
                        }
                    }
                }, TaskCreationOptions.LongRunning);
            }

            public static void Append(string packet)
            {
                pendingPackets.Enqueue(packet);
                queueNotifier.Set();
            }
        }
    }
}
