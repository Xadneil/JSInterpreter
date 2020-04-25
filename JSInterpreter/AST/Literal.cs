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

        public Literal(double number)
        {
            literalType = LiteralType.Number;
            this.number = number;
        }

        public Literal(bool boolean)
        {
            literalType = LiteralType.Boolean;
            this.boolean = boolean;
        }

        public Literal(string @string)
        {
            literalType = LiteralType.String;
            this.@string = @string;
        }

        private Literal(LiteralType literalType)
        {
            this.literalType = literalType;
        }

        public static Literal NullLiteral = new Literal(LiteralType.Null);

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
