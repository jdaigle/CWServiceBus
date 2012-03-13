using System;

namespace CWServiceBus.CommandBus {
    /// <summary>
    /// </summary>
    public interface ICommandBus : IMessageCreator {
        void Send(params object[] commands);
        void Send<T>(Action<T> commandConstructor);
    }
}
