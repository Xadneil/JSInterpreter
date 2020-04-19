using System;
using System.Runtime.Serialization;

namespace JSInterpreter.Parser
{
    [Serializable]
    internal class ParseFailureExeption : Exception
    {
        public ParseFailureExeption()
        {
        }

        public ParseFailureExeption(string message) : base(message)
        {
        }

        public ParseFailureExeption(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ParseFailureExeption(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}