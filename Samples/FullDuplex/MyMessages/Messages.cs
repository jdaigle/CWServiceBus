using System;

namespace MyMessages
{
    public class RequestDataMessage
    {
        public Guid DataId { get; set; }
        public string String { get; set; }
    }

    public class DataResponseMessage
    {
        public Guid DataId { get; set; }
        public string String { get; set; }
    }
}
