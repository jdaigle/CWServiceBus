using System;
using CWServiceBus.Dispatch;
using CWServiceBus;

namespace MyServer
{
    public class MyOwnUnitOfWork : IManagesUnitOfWork
    {
        public void Begin(IServiceLocator childServiceLocator, IMessageContext messageContext)
        {
            LogMessage("Begin");
        }

        public void End(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception ex)
        {
            if (ex == null)
                LogMessage("Commit");
            else
                LogMessage("Rollback, reason: " + ex);
        }

        void LogMessage(string message)
        {
            Console.WriteLine(string.Format("UoW({0}) - {1}",GetHashCode(), message));
        }
    }
}