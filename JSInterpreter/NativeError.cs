using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class NativeError : FunctionObject
    {
        public NativeError() { }
        public NativeError(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                DefinePropertyOrThrow("message", new PropertyDescriptor(new StringValue(message), true, false, true));
            }
        }
    }
}
