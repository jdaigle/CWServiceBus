using System;

namespace CWServiceBus.Dispatch
{
    public interface IDispatchInspector
    {
        void OnDispatching(IServiceLocator childServiceLocator, IMessageContext messageContext);
        void OnDispatched(IServiceLocator childServiceLocator, IMessageContext messageContext, bool withError);
        void OnDispatchException(IServiceLocator childServiceLocator, IMessageContext messageContext, Exception exception);
    }
}
