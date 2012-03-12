using System;
using System.Collections.Generic;

namespace CWServiceBus {
    public interface IServiceLocator : IDisposable {
        void BuildUp(object target);
        T Get<T>();
        IEnumerable<T> GetAll<T>();
        object Get(Type type);
        IEnumerable<object> GetAll(Type type);
        void RegisterComponent<T>(T instance);
        void RegisterComponent(Type type, object instance);
        IServiceLocator GetChildServiceLocator();
    }
}
