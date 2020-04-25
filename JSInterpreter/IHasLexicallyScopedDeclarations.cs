using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public interface IHasLexicallyScopedDeclarations
    {
        IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations();
    }
}
