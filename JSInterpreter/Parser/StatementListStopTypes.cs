using JSInterpreter.Lexer;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.Parser
{
    static class StatementListStopTypes
    {
        public static HashSet<TokenType> CurlyClose = new HashSet<TokenType>() { TokenType.CurlyClose };

        public static HashSet<TokenType> CaseDefaultCurlyClose = new HashSet<TokenType>() { TokenType.CurlyClose, TokenType.Case, TokenType.Default };
    }
}
