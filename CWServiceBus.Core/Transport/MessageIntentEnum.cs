namespace CWServiceBus.Transport
{
    ///<summary>
    /// Enumeration defining different kinds of message intent like Send and Publish.
    ///</summary>
    /// <remarks>
    /// To remain compatible with NServiceBus, this enum has the same values as the NServiceBus
    /// implementation
    /// </remarks>
    public enum MessageIntentEnum
    {
        Send,
        Publish,
        Subscribe,
        Unsubscribe,
        FaultNotification
    }
}
