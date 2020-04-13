using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    interface ILeftHandSideExpression : IUpdateExpression, IForInOfInitializer
    {
    }

    interface INewExpression : ILeftHandSideExpression
    {
    }
}
