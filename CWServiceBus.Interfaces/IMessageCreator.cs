using System;

namespace CWServiceBus
{
    /// <summary>
    /// The abstraction for creating interface-based messages.
    /// </summary>
    public interface IMessageCreator
    {
        T CreateInstance<T>();
        T CreateInstance<T>(Action<T> action);
        object CreateInstance(Type messageType);
    }
}
