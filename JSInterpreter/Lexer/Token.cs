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

using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.Lexer
{
    public class Token
    {
        public TokenType Type { get; private set; }
        public string Trivia { get; private set; }
        public string Value { get; private set; }
        public int LineNumber { get; private set; }
        public int LineColumn { get; private set; }
        public bool PassedNewLine { get; private set; }

        public int TriviaStart { get; private set; }
        public int ValueStart { get; private set; }

        public Token(TokenType type, string trivia, string value, int triviaStart, int valueStart, int lineNumber, int lineColumn, bool passedNewLine)
        {
            Type = type;
            Trivia = trivia;
            Value = value;
            TriviaStart = triviaStart;
            ValueStart = valueStart;
            LineNumber = lineNumber;
            LineColumn = lineColumn;
            PassedNewLine = passedNewLine;
        }

        public double DoubleValue()
        {
            if (Type != TokenType.NumericLiteral)
                throw new InvalidOperationException("DoubleValue can only be used on a numeric literal.");
            if (Value[0] == '0' && Value.Length >= 2)
            {
                if (Value[1] == 'x' || Value[1] == 'X')
                {
                    // hexadecimal
                    string number = Value.Substring(2);
                    if (number.Length < 16)
                        return Convert.ToInt64(number, 16);
                    else if (number.Length > 255)
                    {
                        return double.PositiveInfinity;
                    }
                    else
                    {
                        double value = 0;

                        double modulo = 1;
                        var literal = number.ToUpperInvariant();
                        var length = literal.Length - 1;
                        for (var i = length; i >= 0; i--)
                        {
                            var c = literal[i];

                            if (c <= '9')
                            {
                                value += modulo * (c - '0');
                            }
                            else
                            {
                                value += modulo * (c - 'A' + 10);
                            }

                            modulo *= 16;
                        }
                        return value;
                    }
                }
                else if (Value[1] == 'o' || Value[1] == 'O')
                {
                    // octal
                    return Convert.ToInt32(Value.Substring(2), 8);
                }
                else if (Value[1] == 'b' || Value[1] == 'B')
                {
                    // binary
                    return Convert.ToInt32(Value.Substring(2), 2);
                }
                else if (char.IsDigit(Value[1]))
                {
                    // also octal, but syntax error in strict mode
                    return Convert.ToInt32(Value.Substring(1), 8);
                }
            }

            return double.Parse(Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private enum StringLexState
        {
            Normal,
            EscapeCharacter,
            HexEscape,
            UnicodeEscape,
            UnicodeBrace
        }

        public string StringValue()
        {
            if (Type != TokenType.StringLiteral)
            {
                throw new InvalidOperationException("StringValue can only be used on a string literal.");
            }
            StringBuilder builder = new StringBuilder();
            var state = StringLexState.Normal;
            int hexCount = 0, unicodeCount = 0;
            var hexChars = new char[2];
            var unicodeChars = new char[4];
            for (int i = 1; i < Value.Length - 1; ++i)
            {
                switch (state)
                {
                    case StringLexState.Normal:
                        if (Value[i] == '\\')
                        {
                            state = StringLexState.EscapeCharacter;
                            break;
                        }
                        builder.Append(Value[i]);
                        break;
                    case StringLexState.EscapeCharacter:
                        switch (Value[i])
                        {
                            case 'b':
                                builder.Append('\b');
                                break;
                            case 'f':
                                builder.Append('\f');
                                break;
                            case 'n':
                                builder.Append('\n');
                                break;
                            case 'r':
                                builder.Append('\r');
                                break;
                            case 't':
                                builder.Append('\t');
                                break;
                            case 'v':
                                builder.Append('\v');
                                break;
                            case '0':
                                if (i + 1 < Value.Length && !char.IsDigit(Value[i + 1]))
                                    builder.Append((char)0);
                                else
                                    throw new NotImplementedException("\\0 may not appear before more digits");
                                break;
                            case '\'':
                                builder.Append('\'');
                                break;
                            case '"':
                                builder.Append('"');
                                break;
                            case '\\':
                                builder.Append('\\');
                                break;
                            case 'x':
                                state = StringLexState.HexEscape;
                                hexCount = 0;
                                break;
                            case 'u':
                                state = StringLexState.UnicodeEscape;
                                unicodeCount = 0;
                                break;
                            default:
                                // not a needed escape sequence
                                builder.Append(Value[i]);
                                break;
                        }
                        if (state == StringLexState.EscapeCharacter)
                            state = StringLexState.Normal;
                        break;
                    case StringLexState.HexEscape:
                        if (!Value[i].IsHexDigit())
                            throw new InvalidOperationException("Only hex characters are allowed in \\x escape sequences");
                        hexChars[hexCount] = Value[i];
                        hexCount++;
                        if (hexCount == 2)
                        {
                            builder.Append((char)(hexChars[0].ToHexValue() << 4) + hexChars[1].ToHexValue());
                            state = StringLexState.Normal;
                        }
                        break;
                    case StringLexState.UnicodeEscape:
                        if (unicodeCount == 0 && Value[i] == '{')
                        {
                            state = StringLexState.UnicodeBrace;
                            break;
                        }
                        if (!Value[i].IsHexDigit())
                            throw new InvalidOperationException("Only hex characters are allowed in \\u escape sequences");
                        unicodeChars[unicodeCount] = Value[i];
                        unicodeCount++;
                        if (unicodeCount == 4)
                        {
                            builder.Append((char)(
                                (unicodeChars[0].ToHexValue() << 12) +
                                (unicodeChars[1].ToHexValue() << 8) +
                                (unicodeChars[2].ToHexValue() << 4) +
                                (unicodeChars[3].ToHexValue())
                                ));
                            state = StringLexState.Normal;
                        }
                        break;
                    case StringLexState.UnicodeBrace:
                        builder.Append(Value[i]);
                        if (i + 1 < Value.Length || Value[i + 1] != '}')
                            throw new InvalidOperationException("Escape sequence starting with \\u{ must end with }");
                        // consume the }
                        i++;
                        state = StringLexState.Normal;
                        break;
                    default:
                        throw new InvalidOperationException("Invalid StringLexState");
                }
            }
            return builder.ToString();
        }

        public bool BoolValue()
        {
            if (Type != TokenType.BoolLiteral)
            {
                throw new InvalidOperationException("BoolValue can only be used on a boolean literal.");
            }
            return Value == "true";
        }

        // /abc/g
        // 012345
        // length = 6
        public RegularExpressionLiteral RegexValue()
        {
            var lastSlash = Value.LastIndexOf('/');
            if (lastSlash == Value.Length - 1)
                return new RegularExpressionLiteral(Value[1..^1], "");
            return new RegularExpressionLiteral(Value[1..lastSlash], Value.Substring(lastSlash + 1));
        }
    }
}
