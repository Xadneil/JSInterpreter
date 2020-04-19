/*
 * Copyright (c) 2020, Stephan Unverwerth <s.unverwerth@gmx.de>; Daniel Flaws <dflaws1@gmail.com>
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this
 *    list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.Lexer
{
    class Lexer
    {
        private static readonly Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>();
        private static readonly Dictionary<string, TokenType> three_char_tokens = new Dictionary<string, TokenType>();
        private static readonly Dictionary<string, TokenType> two_char_tokens = new Dictionary<string, TokenType>();
        private static readonly Dictionary<char, TokenType> single_char_tokens = new Dictionary<char, TokenType>();

        private readonly string source;
        private int position;
        Token currentToken;
        char currentChar;
        bool hasErrors;
        int lineNumber = 1;
        int lineColumn = 1;
        bool logErrors = true;

        public bool PassedNewLine { get; private set; }

        public Lexer(string source)
        {
            this.source = source;
            currentToken = new Token(TokenType.Eof, null, null, 0, 0);

            if (!keywords.Any())
            {
                keywords["await"] = TokenType.Await;
                keywords["break"] = TokenType.Break;
                keywords["case"] = TokenType.Case;
                keywords["catch"] = TokenType.Catch;
                keywords["class"] = TokenType.Class;
                keywords["const"] = TokenType.Const;
                keywords["continue"] = TokenType.Continue;
                keywords["debugger"] = TokenType.Debugger;
                keywords["default"] = TokenType.Default;
                keywords["delete"] = TokenType.Delete;
                keywords["do"] = TokenType.Do;
                keywords["else"] = TokenType.Else;
                keywords["false"] = TokenType.BoolLiteral;
                keywords["finally"] = TokenType.Finally;
                keywords["for"] = TokenType.For;
                keywords["function"] = TokenType.Function;
                keywords["if"] = TokenType.If;
                keywords["in"] = TokenType.In;
                keywords["instanceof"] = TokenType.Instanceof;
                keywords["interface"] = TokenType.Interface;
                keywords["let"] = TokenType.Let;
                keywords["new"] = TokenType.New;
                keywords["null"] = TokenType.NullLiteral;
                keywords["of"] = TokenType.Of;
                keywords["return"] = TokenType.Return;
                keywords["switch"] = TokenType.Switch;
                keywords["this"] = TokenType.This;
                keywords["throw"] = TokenType.Throw;
                keywords["true"] = TokenType.BoolLiteral;
                keywords["try"] = TokenType.Try;
                keywords["typeof"] = TokenType.Typeof;
                keywords["var"] = TokenType.Var;
                keywords["void"] = TokenType.Void;
                keywords["while"] = TokenType.While;
                keywords["yield"] = TokenType.Yield;
            }

            if (!three_char_tokens.Any())
            {
                three_char_tokens["..."] = TokenType.Ellipsis;
                three_char_tokens["==="] = TokenType.EqualsEqualsEquals;
                three_char_tokens["!=="] = TokenType.ExclamationMarkEqualsEquals;
                three_char_tokens["**="] = TokenType.AsteriskAsteriskEquals;
                three_char_tokens["<<="] = TokenType.ShiftLeftEquals;
                three_char_tokens[">>="] = TokenType.ShiftRightEquals;
                three_char_tokens[">>>"] = TokenType.UnsignedShiftRight;
            }

            if (!two_char_tokens.Any())
            {
                two_char_tokens["=>"] = TokenType.Arrow;
                two_char_tokens["+="] = TokenType.PlusEquals;
                two_char_tokens["-="] = TokenType.MinusEquals;
                two_char_tokens["*="] = TokenType.AsteriskEquals;
                two_char_tokens["/="] = TokenType.SlashEquals;
                two_char_tokens["%="] = TokenType.PercentEquals;
                two_char_tokens["&="] = TokenType.AmpersandEquals;
                two_char_tokens["|="] = TokenType.PipeEquals;
                two_char_tokens["^="] = TokenType.CaretEquals;
                two_char_tokens["&&"] = TokenType.DoubleAmpersand;
                two_char_tokens["||"] = TokenType.DoublePipe;
                two_char_tokens["??"] = TokenType.DoubleQuestionMark;
                two_char_tokens["**"] = TokenType.DoubleAsterisk;
                two_char_tokens["=="] = TokenType.EqualsEquals;
                two_char_tokens["<="] = TokenType.LessThanEquals;
                two_char_tokens[">="] = TokenType.GreaterThanEquals;
                two_char_tokens["!="] = TokenType.ExclamationMarkEquals;
                two_char_tokens["--"] = TokenType.MinusMinus;
                two_char_tokens["++"] = TokenType.PlusPlus;
                two_char_tokens["<<"] = TokenType.ShiftLeft;
                two_char_tokens[">>"] = TokenType.ShiftRight;
                two_char_tokens["?."] = TokenType.QuestionMarkPeriod;
            }

            if (!single_char_tokens.Any())
            {
                single_char_tokens['&'] = TokenType.Ampersand;
                single_char_tokens['*'] = TokenType.Asterisk;
                single_char_tokens['['] = TokenType.BracketOpen;
                single_char_tokens[']'] = TokenType.BracketClose;
                single_char_tokens['^'] = TokenType.Caret;
                single_char_tokens[':'] = TokenType.Colon;
                single_char_tokens[','] = TokenType.Comma;
                single_char_tokens['{'] = TokenType.CurlyOpen;
                single_char_tokens['}'] = TokenType.CurlyClose;
                single_char_tokens['='] = TokenType.Equals;
                single_char_tokens['!'] = TokenType.ExclamationMark;
                single_char_tokens['-'] = TokenType.Minus;
                single_char_tokens['('] = TokenType.ParenOpen;
                single_char_tokens[')'] = TokenType.ParenClose;
                single_char_tokens['%'] = TokenType.Percent;
                single_char_tokens['.'] = TokenType.Period;
                single_char_tokens['|'] = TokenType.Pipe;
                single_char_tokens['+'] = TokenType.Plus;
                single_char_tokens['?'] = TokenType.QuestionMark;
                single_char_tokens[';'] = TokenType.Semicolon;
                single_char_tokens['/'] = TokenType.Slash;
                single_char_tokens['~'] = TokenType.Tilde;
                single_char_tokens['<'] = TokenType.LessThan;
                single_char_tokens['>'] = TokenType.GreaterThan;
            }
            Consume();
        }

        public void Consume()
        {
            if (position >= source.Length)
            {
                position = source.Length + 1;
                currentChar = '\0';
                return;
            }

            if (currentChar == '\n')
            {
                lineNumber++;
                lineColumn = 1;
                PassedNewLine = true;
            }
            else
            {
                lineColumn++;
            }

            currentChar = source[position++];
        }

        public void ConsumeExponent()
        {
            Consume();
            if (currentChar == '-' || currentChar == '+')
                Consume();
            while (char.IsDigit(currentChar))
            {
                Consume();
            }
        }

        public bool Match(char a, char b)
        {
            if (position >= source.Length)
                return false;

            return currentChar == a
                && source[position] == b;
        }

        public bool Match(char a, char b, char c)
        {
            if (position + 1 >= source.Length)
                return false;

            return currentChar == a
                && source[position] == b
                && source[position + 1] == c;
        }

        public bool Match(char a, char b, char c, char d)
        {
            if (position + 2 >= source.Length)
                return false;

            return currentChar == a
                && source[position] == b
                && source[position + 1] == c
                && source[position + 2] == d;
        }

        public bool IsEOF()
        {
            return currentChar == '\0';
        }

        public bool IsIdentifierStart()
        {
            return char.IsLetter(currentChar) || currentChar == '_' || currentChar == '$';
        }

        public bool IsIdentifierMiddle()
        {
            return IsIdentifierStart() || char.IsDigit(currentChar);
        }

        public bool IsLineCommentStart()
        {
            return Match('/', '/');
        }

        public bool IsBlockCommentStart()
        {
            return Match('/', '*');
        }

        public bool IsBlockCommentEnd()
        {
            return Match('*', '/');
        }

        public bool IsNumericLiteralStart()
        {
            return char.IsDigit(currentChar) || (currentChar == '.' && position < source.Length && char.IsDigit(source[position]));
        }

        public void SyntaxError(string msg)
        {
            hasErrors = true;
            if (logErrors)
                Console.WriteLine($"Syntax Error: {msg} (line: {lineNumber}, column: {lineColumn})");
        }

        public Token Next()
        {
            PassedNewLine = false;
            int trivia_start = position;

            // Consume whitespace and comments
            while (true)
            {
                if (char.IsWhiteSpace(currentChar))
                {
                    do
                    {
                        Consume();
                    } while (char.IsWhiteSpace(currentChar));
                }
                else if (IsLineCommentStart())
                {
                    Consume();
                    do
                    {
                        Consume();
                    } while (!IsEOF() && currentChar != '\n');
                }
                else if (IsBlockCommentStart())
                {
                    Consume();
                    do
                    {
                        Consume();
                    } while (!IsEOF() && !IsBlockCommentEnd());
                    Consume(); // Consume *
                    Consume(); // Consume /
                }
                else
                {
                    break;
                }
            }

            int value_start = position;
            var token_type = TokenType.Invalid;

            if (IsIdentifierStart())
            {
                // identifier or keyword
                do
                {
                    Consume();
                } while (IsIdentifierMiddle());

                string value = source.Substring(value_start - 1, position - value_start);

                if (!keywords.TryGetValue(value, out token_type))
                {
                    token_type = TokenType.Identifier;
                }
            }
            else if (IsNumericLiteralStart())
            {
                if (currentChar == '0')
                {
                    Consume();
                    if (currentChar == '.')
                    {
                        // decimal
                        Consume();
                        while (char.IsDigit(currentChar))
                        {
                            Consume();
                        }
                        if (currentChar == 'e' || currentChar == 'E')
                        {
                            ConsumeExponent();
                        }
                    }
                    else if (currentChar == 'e' || currentChar == 'E')
                    {
                        ConsumeExponent();
                    }
                    else if (currentChar == 'o' || currentChar == 'O')
                    {
                        // octal
                        Consume();
                        while (currentChar >= '0' && currentChar <= '7')
                        {
                            Consume();
                        }
                    }
                    else if (currentChar == 'b' || currentChar == 'B')
                    {
                        // binary
                        Consume();
                        while (currentChar == '0' || currentChar == '1')
                        {
                            Consume();
                        }
                    }
                    else if (currentChar == 'x' || currentChar == 'X')
                    {
                        // hexadecimal
                        Consume();
                        while (IsHexDigit(currentChar))
                        {
                            Consume();
                        }
                    }
                    else if (char.IsDigit(currentChar))
                    {
                        // octal without 'O' prefix. Forbidden in 'strict mode'
                        // FIXME: We need to make sure this produces a syntax error when in strict mode
                        do
                        {
                            Consume();
                        } while (char.IsDigit(currentChar));
                    }
                }
                else
                {
                    // 1...9 or period
                    while (char.IsDigit(currentChar))
                    {
                        Consume();
                    }
                    if (currentChar == '.')
                    {
                        Consume();
                        while (char.IsDigit(currentChar))
                        {
                            Consume();
                        }
                    }
                    if (currentChar == 'e' || currentChar == 'E')
                    {
                        ConsumeExponent();
                    }
                }
                token_type = TokenType.NumericLiteral;
            }
            else if (currentChar == '"' || currentChar == '\'')
            {
                char stop_char = currentChar;
                Consume();
                while (currentChar != stop_char && currentChar != '\n' && !IsEOF())
                {
                    if (currentChar == '\\')
                    {
                        Consume();
                    }
                    Consume();
                }
                if (currentChar != stop_char)
                {
                    SyntaxError("unterminated string literal");
                    token_type = TokenType.UnterminatedStringLiteral;
                }
                else
                {
                    Consume();
                    token_type = TokenType.StringLiteral;
                }
            }
            else if (currentChar == '\0')
            {
                token_type = TokenType.Eof;
            }
            else
            {
                // There is only one four-char operator: >>>=
                bool found_four_char_token = false;
                if (Match('>', '>', '>', '='))
                {
                    found_four_char_token = true;
                    Consume();
                    Consume();
                    Consume();
                    Consume();
                    token_type = TokenType.UnsignedShiftRightEquals;
                }

                bool found_three_char_token = false;
                if (!found_four_char_token && position + 1 < source.Length)
                {
                    char second_char = source[position];
                    char third_char = source[position + 1];
                    string three_chars = new string(new[] { currentChar, second_char, third_char });
                    if (three_char_tokens.TryGetValue(three_chars, out TokenType tokenType))
                    {
                        found_three_char_token = true;
                        Consume();
                        Consume();
                        Consume();
                        token_type = tokenType;
                    }
                }

                bool found_two_char_token = false;
                if (!found_four_char_token && !found_three_char_token && position < source.Length)
                {
                    char second_char = source[position];
                    string two_chars = new string(new[] { currentChar, second_char });
                    if (two_char_tokens.TryGetValue(two_chars, out TokenType tokenType))
                    {
                        found_two_char_token = true;
                        Consume();
                        Consume();
                        token_type = tokenType;
                    }
                }

                bool found_one_char_token = false;
                if (!found_four_char_token && !found_three_char_token && !found_two_char_token)
                {
                    if (single_char_tokens.TryGetValue(currentChar, out TokenType tokenType))
                    {
                        found_one_char_token = true;
                        Consume();
                        token_type = tokenType;
                    }
                }

                if (!found_four_char_token && !found_three_char_token && !found_two_char_token && !found_one_char_token)
                {
                    Consume();
                    token_type = TokenType.Invalid;
                }
            }

            currentToken = new Token(
                token_type,
                source.Substring(trivia_start - 1, value_start - trivia_start),
                source.Substring(value_start - 1, position - value_start),
                lineNumber,
                lineColumn - position + value_start);

            return currentToken;
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
               (c >= 'a' && c <= 'f') ||
               (c >= 'A' && c <= 'F');
        }

        public bool HasErrors()
        {
            return hasErrors;
        }
    }
}
