using System;

namespace CWServiceBus {
    public interface IServiceLocator : IDisposable {
        void BuildUp(object target);
        T Get<T>();
        object Get(Type type);
        void Inject<T>(T instance);
        void Inject(Type type, object instance);
        IServiceLocator GetChildServiceLocator();
    }
}
