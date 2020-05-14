using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum LiteralType
    {
        Null,
        Boolean,
        Number,
        String
    }
    public class Literal : IPrimaryExpression
    {
        public readonly LiteralType literalType;
        public readonly double number;
        public readonly bool boolean;
        public readonly string @string;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public Literal(double number)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            literalType = LiteralType.Number;
            this.number = number;
        }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public Literal(bool boolean)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            literalType = LiteralType.Boolean;
            this.boolean = boolean;
        }

        public Literal(string @string)
        {
            literalType = LiteralType.String;
            this.@string = @string;
        }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        private Literal(LiteralType literalType)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.literalType = literalType;
        }

        public readonly static Literal NullLiteral = new Literal(LiteralType.Null);

        public Completion Evaluate(Interpreter interpreter)
        {
            return Completion.NormalCompletion(literalType switch
            {
                LiteralType.Null => NullValue.Instance,
                LiteralType.Boolean => boolean ? BooleanValue.True : BooleanValue.False,
                LiteralType.Number => new NumberValue(number),
                LiteralType.String => new StringValue(@string),
                _ => throw new InvalidOperationException($"Literal: bad literalType enum: {(int)literalType}"),
            });
        }
    }
}
