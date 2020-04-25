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
                        var literal = number.ToLowerInvariant();
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
                                value += modulo * (c - 'a' + 10);
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

            return double.Parse(Value);
        }

        public string StringValue()
        {
            if (Type != TokenType.StringLiteral)
            {
                throw new InvalidOperationException("StringValue can only be used on a string literal.");
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 1; i < Value.Length - 1; ++i)
            {
                if (Value[i] == '\\' && i + 1 < Value.Length - 1)
                {
                    i++;
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
                            builder.Append((char)0);
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
                        default:
                            // FIXME: Also parse octal, hex and unicode sequences
                            // should anything else generate a syntax error?
                            builder.Append(Value[i]);
                            break;
                    }
                }
                else
                {
                    builder.Append(Value[i]);
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
