using System;
using CWServiceBus;

namespace CWServiceBus
{
    public class NoOPUnitOfWorkManager : IManagesUnitOfWork
    {
        public void Begin(IServiceLocator childServiceLocator, IMessageContext messageContext)
        {
            // NOP
        }

        public void End(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception exception)
        {
            // NOP
        }
    }
}