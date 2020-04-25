using System;
using System.Collections.Generic;

namespace JSInterpreter.Lexer
{
    public class Tokens
    {
        private readonly List<Token> tokens = new List<Token>();

        public void Add(Token token)
        {
            tokens.Add(token);
        }

        public Token this[int index] => tokens[index];

        public int Count => tokens.Count;
    }
}