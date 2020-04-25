using System;
using System.Runtime.Serialization;

namespace JSInterpreter.Parser
{
    [Serializable]
    public class ParseFailureException : Exception
    {
        public ParseFailureException()
        {
        }

        public ParseFailureException(string message) : base(message)
        {
        }

        public ParseFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ParseFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}