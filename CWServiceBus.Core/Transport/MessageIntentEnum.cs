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
        ///<summary>
        /// Regular point-to-point send
        ///</summary>
        Send,

        ///<summary>
        /// Publish, not a regular point-to-point send
        ///</summary>
        Publish,

        /// <summary>
        /// Subscribe
        /// </summary>
        Subscribe,

        /// <summary>
        /// Unsubscribe
        /// </summary>
        Unsubscribe
    }
}
