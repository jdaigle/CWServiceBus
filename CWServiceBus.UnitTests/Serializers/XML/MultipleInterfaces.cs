using System;

namespace CWServiceBus.Serializers.XML.Test
{
    public interface IFirst : IMessage
    {
        String FirstName { get; set; }
    }
    
    public interface ISecond : IFirst
    {
    }
    
    public interface IThird : ISecond
    {
    }
}
