using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.Lexer
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types")]
    public struct LexerState
    {
        public int position;
        public char currentChar;
        public int lineNumber;
        public int lineColumn;
        public bool passedNewLine;
        public Token CurrentToken;

        public LexerState(int position, char currentChar, int lineNumber, int lineColumn, bool passedNewLine, Token currentToken)
        {
            this.position = position;
            this.currentChar = currentChar;
            this.lineNumber = lineNumber;
            this.lineColumn = lineColumn;
            this.passedNewLine = passedNewLine;
            CurrentToken = currentToken;
        }
    }
}
