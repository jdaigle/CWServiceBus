using System;
using System.Collections.Generic;

namespace CWServiceBus {
    public interface IServiceLocator : IDisposable {
        void BuildUp(object target);
        T Get<T>();
        IEnumerable<T> GetAll<T>();
        object Get(Type type);
        IEnumerable<object> GetAll(Type type);
        void Inject<T>(T instance);
        void Inject(Type type, object instance);
        IServiceLocator GetChildServiceLocator();
    }
}
