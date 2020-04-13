using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    interface IHasLexicallyScopedDeclarations
    {
        IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations();
    }
}
