using JSInterpreter.Lexer;

namespace JSInterpreter.Parser
{
    struct ParserState
    {
        public Lexer.Lexer Lexer { get; private set; }
        public Token CurentToken { get; set; }
        public bool PassedNewLine => Lexer.PassedNewLine;
        public bool HasErrors { get; set; }

        public ParserState(Lexer.Lexer lexer)
        {
            Lexer = lexer;
            CurentToken = lexer.Next();
            HasErrors = false;
        }
    }
}
