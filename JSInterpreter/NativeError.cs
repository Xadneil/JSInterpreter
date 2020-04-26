using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NativeError : FunctionObject
    {
        public NativeError() { }
        public NativeError(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                DefinePropertyOrThrow("message", new PropertyDescriptor(new StringValue(message), true, false, true));
            }
        }

        public string Message
        {
            get
            {
                var comp = Get("message");
                if (comp.IsAbrupt()) return "Error getting message";
                var stringValue = comp.value as StringValue;
                return stringValue?.@string ?? "message property is not a string";
            }
        }
    }
}
