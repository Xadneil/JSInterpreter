using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.Parser
{
    class LexerRewinder : IDisposable
    {
        private readonly Lexer.Lexer lexer;
        public bool Success;

        public LexerRewinder(Lexer.Lexer lexer)
        {
            this.lexer = lexer;
            lexer.SaveState();
        }

        public void Dispose()
        {
            if (Success)
                lexer.DeleteState();
            else
                lexer.RestoreState();
        }
    }
}
