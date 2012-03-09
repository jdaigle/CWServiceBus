using System;
using System.Reflection;

namespace CWServiceBus.Dispatch
{
    public class DispatchInfo
    {
        public DispatchInfo() { }
        public DispatchInfo(Type messageType, Type instanceType, MethodInfo methodInfo)
        {
            MessageType = messageType;
            InstanceType = instanceType;
            MethodInfo = methodInfo;
        }

        public Type MessageType { get; set; }
        public Type InstanceType { get; set; }
        public MethodInfo MethodInfo { get; set; }
    }
}
