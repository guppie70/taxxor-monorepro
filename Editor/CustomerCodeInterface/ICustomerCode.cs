using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CustomerCodeInterface
{
    public interface ICustomerCode
    {
        public string ClientName { get; }

        // default implementation
        public string DoSomething(string parameter);

        public string DoSomethingOptional(string parameter)
        {
            return "Default implementation in case this method is not implemented in a custom code DLL";
        }

        public string LoremIpsum()
        {
            return "Value from interface code";
        }
    }
}
