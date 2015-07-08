using System;
using System.Collections.Generic;

namespace CWServiceBus
{
    public interface IServiceLocator : IDisposable
    {
        void BuildUp(object target);
        IEnumerable<T> GetAll<T>();
        object Get(Type type);
        void RegisterComponent<T>(T instance);
        IServiceLocator GetChildServiceLocator();
    }
}
