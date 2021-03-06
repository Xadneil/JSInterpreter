﻿/*
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
    public class Lexer
    {
        private static readonly Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>();
        private static readonly Dictionary<string, TokenType> three_char_tokens = new Dictionary<string, TokenType>();
        private static readonly Dictionary<string, TokenType> two_char_tokens = new Dictionary<string, TokenType>();
        private static readonly Dictionary<char, TokenType> single_char_tokens = new Dictionary<char, TokenType>();

        public Token CurrentToken { get => currentState.CurrentToken; private set => currentState.CurrentToken = value; }

        private readonly string source;
        private readonly Stack<LexerState> stateStack;

        private LexerState currentState;

        private int position { get => currentState.position; set => currentState.position = value; }
        private char currentChar { get => currentState.currentChar; set => currentState.currentChar = value; }
        private int lineNumber { get => currentState.lineNumber; set => currentState.lineNumber = value; }
        private int lineColumn { get => currentState.lineColumn; set => currentState.lineColumn = value; }
        private bool passedNewLine { get => currentState.passedNewLine; set => currentState.passedNewLine = value; }


        public Lexer(string source)
        {
            this.source = source;
            stateStack = new Stack<LexerState>();
            currentState = new LexerState(0, default, 1, 1, false, new Token(TokenType.Eof, "", "", 0, 0, 0, 0, false));

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
                keywords["enum"] = TokenType.Enum;
                keywords["false"] = TokenType.BoolLiteral;
                keywords["export"] = TokenType.Export;
                keywords["extends"] = TokenType.Extends;
                keywords["finally"] = TokenType.Finally;
                keywords["for"] = TokenType.For;
                keywords["function"] = TokenType.Function;
                keywords["if"] = TokenType.If;
                keywords["import"] = TokenType.Import;
                keywords["in"] = TokenType.In;
                keywords["instanceof"] = TokenType.Instanceof;
                //keywords["interface"] = TokenType.Interface; // prefer the identifier version, so it doesn't cause errors in non-strict code
                keywords["let"] = TokenType.Let;
                keywords["new"] = TokenType.New;
                keywords["null"] = TokenType.NullLiteral;
                keywords["of"] = TokenType.Of;
                keywords["return"] = TokenType.Return;
                keywords["super"] = TokenType.Super;
                keywords["switch"] = TokenType.Switch;
                keywords["this"] = TokenType.This;
                keywords["throw"] = TokenType.Throw;
                keywords["true"] = TokenType.BoolLiteral;
                keywords["try"] = TokenType.Try;
                keywords["typeof"] = TokenType.Typeof;
                keywords["var"] = TokenType.Var;
                keywords["void"] = TokenType.Void;
                keywords["while"] = TokenType.While;
                keywords["with"] = TokenType.With;
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

        private void Consume()
        {
            if (position >= source.Length)
            {
                position = source.Length + 1;
                //currentChar = '\0';
                return;
            }

            if (currentChar == '\n')
            {
                lineNumber++;
                lineColumn = 1;
                passedNewLine = true;
            }
            else
            {
                lineColumn++;
            }

            currentChar = source[position++];
        }

        private void ConsumeExponent()
        {
            Consume();
            if (!IsEOF() && (currentChar == '-' || currentChar == '+'))
                Consume();
            while (!IsEOF() && char.IsDigit(currentChar))
            {
                Consume();
            }
        }

        private bool Match(char a, char b)
        {
            if (position >= source.Length)
                return false;

            return currentChar == a
                && source[position] == b;
        }

        private bool Match(char a, char b, char c, char d)
        {
            if (position + 2 >= source.Length)
                return false;

            return currentChar == a
                && source[position] == b
                && source[position + 1] == c
                && source[position + 2] == d;
        }

        private bool IsEOF()
        {
            return position == source.Length + 1;
        }

        private bool IsIdentifierStart()
        {
            return !IsEOF() && (char.IsLetter(currentChar) || currentChar == '_' || currentChar == '$' || currentChar == '\\');
        }

        private bool IsIdentifierMiddle()
        {
            return !IsEOF() && (IsIdentifierStart() || char.IsDigit(currentChar));
        }

        private bool IsLineCommentStart()
        {
            return Match('/', '/');
        }

        private bool IsBlockCommentStart()
        {
            return Match('/', '*');
        }

        private bool IsBlockCommentEnd()
        {
            return Match('*', '/');
        }

        private bool IsNumericLiteralStart()
        {
            return !IsEOF() && (char.IsDigit(currentChar) || (currentChar == '.' && position < source.Length && char.IsDigit(source[position])));
        }

        private void SyntaxError(string msg)
        {
            throw new Exception($"Syntax Error: {msg} (line: {lineNumber}, column: {lineColumn})");
        }

        public Token Next()
        {
            passedNewLine = false;
            int trivia_start = position;

            ConsumeWhitespaceAndComments();

            int value_start = position;
            TokenType token_type;
            string? overrideValue = null;

            if (IsEOF())
            {
                token_type = TokenType.Eof;
            }
            else if (IsIdentifierStart())
            {
                // identifier or keyword
                StringBuilder sb = new StringBuilder();
                do
                {
                    if (currentChar == '\\' && position + 4 < source.Length)
                    {
                        Consume();
                        if (currentChar != 'u')
                        {
                            SyntaxError("Invalid identifier staring with \\");
                        }
                        else
                        {
                            Consume();
                            bool hasBraces = currentChar == '{';
                            if (hasBraces)
                                Consume();
                            int numberStart = position - 1;
                            if (!currentChar.IsHexDigit())
                            {
                                SyntaxError("Invalid identifier staring with \\");
                            }
                            else
                                Consume();
                            if (!currentChar.IsHexDigit())
                            {
                                SyntaxError("Invalid identifier staring with \\");
                            }
                            else
                                Consume();
                            if (!currentChar.IsHexDigit())
                            {
                                SyntaxError("Invalid identifier staring with \\");
                            }
                            else
                                Consume();
                            if (!currentChar.IsHexDigit())
                            {
                                SyntaxError("Invalid identifier staring with \\");
                            }
                            else
                                Consume();
                            if (hasBraces)
                            {
                                if (currentChar == '}')
                                    Consume();
                                else
                                {
                                    SyntaxError("unicode escape without closing }");
                                }
                            }
                            var numbers = source.Substring(numberStart, 4);
                            if (!int.TryParse(numbers, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int charValue))
                            {
                                SyntaxError($"Invalid unicode sequence {numbers}");
                            }
                            sb.Append((char)charValue);
                        }
                    }
                    else
                    {
                        sb.Append(currentChar);
                        Consume();
                    }
                } while (IsIdentifierMiddle());

                overrideValue = sb.ToString();

                if (!keywords.TryGetValue(overrideValue, out token_type))
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
                        while (!IsEOF() && char.IsDigit(currentChar))
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
                        while (!IsEOF() && currentChar >= '0' && currentChar <= '7')
                        {
                            Consume();
                        }
                    }
                    else if (currentChar == 'b' || currentChar == 'B')
                    {
                        // binary
                        Consume();
                        while (!IsEOF() && (currentChar == '0' || currentChar == '1'))
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
                    else if (!IsEOF() && char.IsDigit(currentChar))
                    {
                        // octal without 'O' prefix. Forbidden in 'strict mode'
                        // FIXME: We need to make sure this produces a syntax error when in strict mode
                        do
                        {
                            Consume();
                        } while (!IsEOF() && char.IsDigit(currentChar));
                    }
                }
                else
                {
                    // 1...9 or period
                    while (!IsEOF() && char.IsDigit(currentChar))
                    {
                        Consume();
                    }
                    if (currentChar == '.')
                    {
                        Consume();
                        while (!IsEOF() && char.IsDigit(currentChar))
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
            else
            {
                // There is only one four-char operator: >>>=
                if (Match('>', '>', '>', '='))
                {
                    Consume();
                    Consume();
                    Consume();
                    Consume();
                    token_type = TokenType.UnsignedShiftRightEquals;
                }
                else if (position + 1 < source.Length && three_char_tokens.TryGetValue(new string(new[] { currentChar, source[position], source[position + 1] }), out TokenType tokenType))
                {
                    Consume();
                    Consume();
                    Consume();
                    token_type = tokenType;
                }
                else if (position < source.Length && two_char_tokens.TryGetValue(new string(new[] { currentChar, source[position] }), out TokenType tokenType2))
                {
                    Consume();
                    Consume();
                    token_type = tokenType2;
                }
                else if (single_char_tokens.TryGetValue(currentChar, out TokenType tokenType3))
                {
                    Consume();
                    token_type = tokenType3;
                }
                else
                {
                    Consume();
                    token_type = TokenType.Invalid;
                }
            }

            CurrentToken = new Token(
                token_type,
                source.Substring(trivia_start - 1, value_start - trivia_start),
                overrideValue ?? source.Substring(value_start - 1, position - value_start),
                trivia_start - 1,
                value_start - 1,
                lineNumber,
                lineColumn - position + value_start,
                passedNewLine);

            return CurrentToken;
        }

        public Token NextRegex(Token regexStart)
        {
            if (IsLineTerminator() || (regexStart.Type == TokenType.Slash && (currentChar == '*' || currentChar == '/')))
                throw new InvalidOperationException("invalid regex");

            int value_start = position;

            while (currentChar != '/' && !IsEOF())
            {
                if (IsLineTerminator())
                    throw new InvalidOperationException("unterminated regular expression literal");
                if (currentChar == '\\')
                {
                    Consume();
                }
                Consume();
            }
            if (currentChar != '/')
                throw new InvalidOperationException("unterminated regular expression literal");
            // trailing /
            Consume();
            // flags
            while (IsIdentifierMiddle())
            {
                Consume();
            }

            return CurrentToken = new Token(
                TokenType.RegexLiteral,
                regexStart.Trivia,
                regexStart.Value + source.Substring(value_start - 1, position - value_start),
                regexStart.TriviaStart,
                regexStart.ValueStart,
                regexStart.LineNumber,
                regexStart.LineColumn,
                regexStart.PassedNewLine);
        }

        private bool IsLineTerminator()
        {
            return currentChar == '\n' || currentChar == '\r' || currentChar == '\u2028' || currentChar == '\u2029';
        }

        private void ConsumeWhitespaceAndComments()
        {
            while (!IsEOF())
            {
                if (char.IsWhiteSpace(currentChar))
                {
                    do
                    {
                        Consume();
                    } while (!IsEOF() && char.IsWhiteSpace(currentChar));
                }
                else if (IsLineCommentStart())
                {
                    Consume();
                    do
                    {
                        Consume();
                    } while (!IsEOF() && currentChar != '\n' && currentChar != '\r' && currentChar != '\u2028' && currentChar != '\u2029');
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
        }

        private bool IsHexDigit(char c)
        {
            return !IsEOF() && ((c >= '0' && c <= '9') ||
               (c >= 'a' && c <= 'f') ||
               (c >= 'A' && c <= 'F'));
        }

        public void SaveState()
        {
            stateStack.Push(currentState);
        }

        public void RestoreState()
        {
            currentState = stateStack.Pop();
        }

        public void DeleteState()
        {
            stateStack.Pop();
        }
    }
}
