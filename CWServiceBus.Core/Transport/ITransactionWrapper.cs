using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CWServiceBus.Transport {
    public interface ITransactionWrapper {
        void RunInTransaction(Action<ITransactionToken> callback);
    }
}
