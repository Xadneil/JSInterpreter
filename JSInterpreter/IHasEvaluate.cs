using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    interface IHasEvaluate
    {
        Completion Evaluate(Interpreter interpreter);
    }
}
