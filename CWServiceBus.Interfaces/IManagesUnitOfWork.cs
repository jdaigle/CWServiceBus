using System;

namespace CWServiceBus {
    public interface IManagesUnitOfWork {
        void Begin(IServiceLocator childServiceLocator, IMessageContext messageContext);
        void End(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception exception);
    }
}
