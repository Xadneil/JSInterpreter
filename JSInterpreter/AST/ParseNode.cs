using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class ParseNode
    {
        public bool IsStrictMode { get; private set; }

        protected ParseNode(bool isStrictMode)
        {
            IsStrictMode = isStrictMode;
        }
    }
}
