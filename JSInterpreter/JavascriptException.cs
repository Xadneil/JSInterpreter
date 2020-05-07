using System;
using System.Runtime.Serialization;

namespace JSInterpreter
{
    [Serializable]
    public class JavascriptException : Exception
    {
        private Completion completion;

        public JavascriptException()
        {
        }

        public JavascriptException(Completion completion) : base(Description(completion))
        {
            this.completion = completion;
        }

        private static string Description(Completion completion)
        {
            if (completion.value != null && completion.value is Object e && e.HasOwnProperty("message").Other)
                return (e.Get("message").value.ToJsString().value as StringValue).@string;
            return completion.value?.GetType().ToString();
        }

        public JavascriptException(string message) : base(message)
        {
        }

        public JavascriptException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JavascriptException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}