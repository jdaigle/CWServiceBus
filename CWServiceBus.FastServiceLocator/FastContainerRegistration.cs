using System;
using log4net;

namespace CWServiceBus.FastServiceLocator
{
    public class FastContainerRegistration : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FastContainerRegistration));
        private readonly Func<FastContainer, object> resolve;
        private object instance;
        private bool instancePerCall;

        private FastContainerRegistration(FastContainerRegistration other)
        {
            this.resolve = other.resolve;
            this.instancePerCall = other.instancePerCall;
            this.instance = other.instance;
        }

        public FastContainerRegistration(Func<FastContainer, object> resolve)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Adding Wireup Callback");
            }
            this.resolve = resolve;
        }

        public FastContainerRegistration(object instance)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Adding Wireup Instance for {0}", instance.GetType());
            }
            this.instance = instance;
        }

        public virtual FastContainerRegistration InstancePerCall()
        {
            AssertNotDisposed();
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Configuring InstancePerCall");
            }
            this.instancePerCall = true;
            return this;
        }

        public virtual object Resolve(FastContainer container)
        {
            AssertNotDisposed();
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Resolving Instance");
            }
            if (this.instancePerCall)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Building New Instance");
                }
                return this.resolve(container);
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Attempting To Resolve Instance");
            }

            if (this.instance != null)
                return this.instance;

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Building And Storing New Instance");
            }
            return this.instance = this.resolve(container);
        }

        public virtual FastContainerRegistration Clone()
        {
            AssertNotDisposed();
            return new FastContainerRegistration(this);
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("TinyContainerRegistration");
            }
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
                    if (instance != null && instance is IDisposable)
                    {
                        ((IDisposable)instance).Dispose();
                    }
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
