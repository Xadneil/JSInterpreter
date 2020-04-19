using JSInterpreter.AST;
using JSInterpreter.Lexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.Parser
{
    /*
    enum Associativity
    {
        Left,
        Right
    };

    class Parser
    {
        private static readonly Dictionary<TokenType, int> operatorPrecedence = new Dictionary<TokenType, int>();

        private ParserState ParserState;
        private ParserState? SavedState;

        public Parser(Lexer.Lexer lexer)
        {
            ParserState = new ParserState(lexer);
            if (!operatorPrecedence.Any())
            {
                operatorPrecedence[TokenType.Period] = 20;
                operatorPrecedence[TokenType.BracketOpen] = 20;
                operatorPrecedence[TokenType.ParenOpen] = 20;
                operatorPrecedence[TokenType.QuestionMarkPeriod] = 20;

                operatorPrecedence[TokenType.New] = 19;

                operatorPrecedence[TokenType.PlusPlus] = 18;
                operatorPrecedence[TokenType.MinusMinus] = 18;

                operatorPrecedence[TokenType.ExclamationMark] = 17;
                operatorPrecedence[TokenType.Tilde] = 17;
                operatorPrecedence[TokenType.Typeof] = 17;
                operatorPrecedence[TokenType.Void] = 17;
                operatorPrecedence[TokenType.Delete] = 17;
                operatorPrecedence[TokenType.Await] = 17;

                operatorPrecedence[TokenType.DoubleAsterisk] = 16;

                operatorPrecedence[TokenType.Asterisk] = 15;
                operatorPrecedence[TokenType.Slash] = 15;
                operatorPrecedence[TokenType.Percent] = 15;

                operatorPrecedence[TokenType.Plus] = 14;
                operatorPrecedence[TokenType.Minus] = 14;

                operatorPrecedence[TokenType.ShiftLeft] = 13;
                operatorPrecedence[TokenType.ShiftRight] = 13;
                operatorPrecedence[TokenType.UnsignedShiftRight] = 13;

                operatorPrecedence[TokenType.LessThan] = 12;
                operatorPrecedence[TokenType.LessThanEquals] = 12;
                operatorPrecedence[TokenType.GreaterThan] = 12;
                operatorPrecedence[TokenType.GreaterThanEquals] = 12;
                operatorPrecedence[TokenType.In] = 12;
                operatorPrecedence[TokenType.Instanceof] = 12;

                operatorPrecedence[TokenType.EqualsEquals] = 11;
                operatorPrecedence[TokenType.ExclamationMarkEquals] = 11;
                operatorPrecedence[TokenType.EqualsEqualsEquals] = 11;
                operatorPrecedence[TokenType.ExclamationMarkEqualsEquals] = 11;

                operatorPrecedence[TokenType.Ampersand] = 10;

                operatorPrecedence[TokenType.Caret] = 9;

                operatorPrecedence[TokenType.Pipe] = 8;

                operatorPrecedence[TokenType.DoubleQuestionMark] = 7;

                operatorPrecedence[TokenType.DoubleAmpersand] = 6;

                operatorPrecedence[TokenType.DoublePipe] = 5;

                operatorPrecedence[TokenType.QuestionMark] = 4;

                operatorPrecedence[TokenType.Equals] = 3;
                operatorPrecedence[TokenType.PlusEquals] = 3;
                operatorPrecedence[TokenType.MinusEquals] = 3;
                operatorPrecedence[TokenType.AsteriskAsteriskEquals] = 3;
                operatorPrecedence[TokenType.AsteriskEquals] = 3;
                operatorPrecedence[TokenType.SlashEquals] = 3;
                operatorPrecedence[TokenType.PercentEquals] = 3;
                operatorPrecedence[TokenType.ShiftLeftEquals] = 3;
                operatorPrecedence[TokenType.ShiftRightEquals] = 3;
                operatorPrecedence[TokenType.UnsignedShiftRightEquals] = 3;
                operatorPrecedence[TokenType.PipeEquals] = 3;

                operatorPrecedence[TokenType.Yield] = 2;

                operatorPrecedence[TokenType.Comma] = 1;
            }
        }

        public bool HasErrors() => ParserState.HasErrors || ParserState.Lexer.HasErrors();

        public int OperatorPrecedence(TokenType type)
        {
            if (!operatorPrecedence.TryGetValue(type, out int precedence))
            {
                throw new InvalidOperationException($"Internal Error: No precedence for operator {type}");
            }
            return precedence;
        }

        public Associativity OperatorAssociativity(TokenType type)
        {
            switch (type)
            {
                case TokenType.Period:
                case TokenType.BracketOpen:
                case TokenType.ParenOpen:
                case TokenType.QuestionMarkPeriod:
                case TokenType.Asterisk:
                case TokenType.Slash:
                case TokenType.Percent:
                case TokenType.Plus:
                case TokenType.Minus:
                case TokenType.ShiftLeft:
                case TokenType.ShiftRight:
                case TokenType.UnsignedShiftRight:
                case TokenType.LessThan:
                case TokenType.LessThanEquals:
                case TokenType.GreaterThan:
                case TokenType.GreaterThanEquals:
                case TokenType.In:
                case TokenType.Instanceof:
                case TokenType.EqualsEquals:
                case TokenType.ExclamationMarkEquals:
                case TokenType.EqualsEqualsEquals:
                case TokenType.ExclamationMarkEqualsEquals:
                case TokenType.Typeof:
                case TokenType.Void:
                case TokenType.Ampersand:
                case TokenType.Caret:
                case TokenType.Pipe:
                case TokenType.DoubleQuestionMark:
                case TokenType.DoubleAmpersand:
                case TokenType.DoublePipe:
                case TokenType.Comma:
                    return Associativity.Left;
                default:
                    return Associativity.Right;
            }
        }

        public Script ParseScript()
        {
            var statements = new List<IStatementListItem>();
            while (!Done())
            {
                if (Match(TokenType.Semicolon))
                    Consume();
                else if (MatchStatement())
                    statements.Add(ParseStatement());
                else
                {
                    Expected("statement");
                    Consume();
                }
            }

            return new Script(new StatementList(statements));
        }

        private IStatementListItem ParseStatement()
        {
            switch (ParserState.CurentToken.Type)
            {
                case TokenType.Function:
                    return ParseFunctionDeclaration();
                case TokenType.CurlyOpen:
                    return ParseBlock();
                case TokenType.Return:
                    return ParseReturnStatement();
                case TokenType.Var:
                    return ParseVarDeclaration();
                case TokenType.Let:
                case TokenType.Const:
                    return ParseLexicalDeclaration();
                case TokenType.For:
                    return ParseForStatement();
                case TokenType.If:
                    return ParseIfStatement();
                case TokenType.Throw:
                    return ParseThrowStatement();
                case TokenType.Try:
                    return ParseTryStatement();
                case TokenType.Break:
                    return ParseBreakStatement();
                case TokenType.Continue:
                    return ParseContinueStatement();
                case TokenType.Switch:
                    return ParseSwitchStatement();
                case TokenType.Do:
                    return ParseDoWhileStatement();
                default:
                    if (MatchExpression())
                    {
                        IExpression expr = ParseExpression(minPrecedence: 0);
                        ConsumeOrInsertSemicolon();
                        return new ExpressionStatement(expr);
                    }
                    ParserState.HasErrors = true;
                    Expected("statement (missing switch case)");
                    Consume();
                    return new EmptyStatement();
            }
        }

        private IExpression ParsePrimaryExpression()
        {
            if (MatchUnaryPrefixedExpression())
                return ParseUnaryPrefixedExpression();
            switch (ParserState.CurentToken.Type)
            {
                case TokenType.ParenOpen:
                    {
                        Consume(TokenType.ParenOpen);
                        if (Match(TokenType.ParenClose) || Match(TokenType.Identifier))
                        {
                            var arrow_function_result = try_parse_arrow_function_expression(true);
                            if (!arrow_function_result.is_null())
                            {
                                return arrow_function_result.release_nonnull();
                            }
                        }
                        var expression = ParseExpression(0);
                        Consume(TokenType.ParenClose);
                        return expression;
                    }
                case TokenType.This:
                    Consume();
                    return new ThisExpression();
                case TokenType.Identifier:
                    {
                        var arrow_function_result = try_parse_arrow_function_expression(false);
                        if (!arrow_function_result.is_null())
                        {
                            return arrow_function_result.release_nonnull();
                        }
                        return new IdentifierReference(new Identifier(Consume().Value));
                    }
                case TokenType.NumericLiteral:
                    return new Literal(Consume().DoubleValue());
                case TokenType.BoolLiteral:
                    return new Literal(Consume().BoolValue());
                case TokenType.StringLiteral:
                    return new Literal(Consume().StringValue());
                case TokenType.NullLiteral:
                    Consume();
                    return Literal.NullLiteral;
                case TokenType.CurlyOpen:
                    return ParseObjectExpression();
                case TokenType.Function:
                    return ParseFunctionExpression();
                case TokenType.BracketOpen:
                    return ParseArrayExpression();
                case TokenType.New:
                    return ParseNewExpression();
                default:
                    ParserState.HasErrors = true;
                    Expected("primary expression (missing switch case)");
                    Consume();
                    //TODO error node
                    return Literal.NullLiteral;
            }
        }

        private IExpression ParseUnaryPrefixedExpression()
        {
            var precedence = OperatorPrecedence(ParserState.CurentToken.Type);
            var associativity = OperatorAssociativity(ParserState.CurentToken.Type);
            switch (ParserState.CurentToken.Type)
            {
                case TokenType.PlusPlus:
                    Consume();
                    return new PrefixUpdateExpression(ParseExpression(precedence, associativity), UpdateOperation.Increment);
                case TokenType.MinusMinus:
                    Consume();
                    return new PrefixUpdateExpression(ParseExpression(precedence, associativity), UpdateOperation.Decrement);
                case TokenType.ExclamationMark:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.BooleanNot, ParseExpression(precedence, associativity));
                case TokenType.Tilde:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.BitwiseNot, ParseExpression(precedence, associativity));
                case TokenType.Plus:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.Plus, ParseExpression(precedence, associativity));
                case TokenType.Minus:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.Negate, ParseExpression(precedence, associativity));
                case TokenType.Typeof:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.Typeof, ParseExpression(precedence, associativity));
                case TokenType.Void:
                    Consume();
                    return new OperatorUnaryExpression(UnaryOperator.Void, ParseExpression(precedence, associativity));
                default:
                    ParserState.HasErrors = true;
                    Expected("primary expression (missing switch case)");
                    Consume();
                    //TODO error node
                    return Literal.NullLiteral;
            }
        }

        private IExpression ParseObjectExpression()
        {
            List<IPropertyDefinition> properties = new List<IPropertyDefinition>();
            Consume(TokenType.CurlyOpen);

            while (!Done() && !Match(TokenType.CurlyClose))
            {
                string property_name;
                if (Match(TokenType.Identifier))
                {
                    property_name = Consume(TokenType.Identifier).Value;
                }
                else if (Match(TokenType.StringLiteral))
                {
                    property_name = Consume(TokenType.StringLiteral).StringValue();
                }
                else if (Match(TokenType.NumericLiteral))
                {
                    property_name = Consume(TokenType.NumericLiteral).Value;
                }
                else
                {
                    ParserState.HasErrors = true;
                    var current_token = ParserState.CurentToken;
                    Console.WriteLine($"Syntax Error: Unexpected token {current_token.Type} as member in object initialization. Expected a numeric literal, string literal or identifier (line: {current_token.LineNumber}, column: {current_token.LineColumn}))");
                    Consume();
                    continue;
                }

                if (Match(TokenType.Colon))
                {
                    Consume(TokenType.Colon);
                    properties.Add(new PropertyDefinition(property_name, ParseExpression(0)));
                }
                else
                {
                    properties.Add(new IdentifierReference(new Identifier(property_name)));
                }

                if (!Match(TokenType.Comma))
                    break;

                Consume(TokenType.Comma);
            }

            Consume(TokenType.CurlyClose);
            return new ObjectLiteral(properties);
        }

        private IExpression ParseArrayExpression()
        {
            Consume(TokenType.BracketOpen);

            var elements = new List<IArrayLiteralItem>();
            while (MatchExpression() || Match(TokenType.Comma))
            {
                IArrayLiteralItem expression;
                if (MatchExpression())
                {
                    expression = ParseExpression(0);
                    if (Match(TokenType.Comma))
                        Consume();
                }
                else
                {
                    int i = 0;
                    while (Match(TokenType.Comma))
                    {
                        i++;
                        Consume();
                    }
                    expression = new Elision(i);
                }
                elements.Add(expression);
            }

            Consume(TokenType.BracketClose);
            return new ArrayLiteral(elements);
        }

        private IExpression ParseExpression(int minPrecedence, Associativity associativity = Associativity.Left)
        {
            IExpression expression = ParsePrimaryExpression();
            while (MatchSecondaryExpression())
            {
                int newPrecedence = OperatorPrecedence(ParserState.CurentToken.Type);
                if (newPrecedence < minPrecedence)
                    break;
                if (newPrecedence == minPrecedence && associativity == Associativity.Left)
                    break;

                Associativity newAssociativity = OperatorAssociativity(ParserState.CurentToken.Type);
                expression = ParseSecondaryExpression(expression, newPrecedence, newAssociativity);
            }
            return expression;
        }

        private IExpression ParseSecondaryExpression(ILeftHandSideExpression lhs, int min_precedence, Associativity associativity)
        {
            switch (ParserState.CurentToken.Type)
            {
                case TokenType.Plus:
                    Consume();
                    return new AdditiveExpression(lhs, AdditiveOperator.Add, ParseExpression(min_precedence, associativity));
                case TokenType.PlusEquals:
                    Consume();
                    return new OperatorAssignmentExpression(lhs, AssignmentOperator.Plus, ParseExpression(min_precedence, associativity));
                case TokenType.Minus:
                    Consume();
                    return new AdditiveExpression(lhs, AdditiveOperator.Subtract, ParseExpression(min_precedence, associativity));
                case TokenType.MinusEquals:
                    Consume();
                    return new OperatorAssignmentExpression(lhs, AssignmentOperator.Minus, ParseExpression(min_precedence, associativity));
                case TokenType.Asterisk:
                    Consume();
                    return new MultiplicativeExpression(lhs, MultiplicativeOperator.Multiply, ParseExpression(min_precedence, associativity));
                case TokenType.AsteriskEquals:
                    Consume();
                    return new OperatorAssignmentExpression(lhs, AssignmentOperator.Multiply, ParseExpression(min_precedence, associativity));
                case TokenType.Slash:
                    Consume();
                    return new MultiplicativeExpression(lhs, MultiplicativeOperator.Divide, ParseExpression(min_precedence, associativity));
                case TokenType.SlashEquals:
                    Consume();
                    return new OperatorAssignmentExpression(lhs, AssignmentOperator.Divide, ParseExpression(min_precedence, associativity));
                case TokenType.Percent:
                    Consume();
                    return new MultiplicativeExpression(lhs, MultiplicativeOperator.Modulus, ParseExpression(min_precedence, associativity));
                case TokenType.DoubleAsterisk:
                    Consume();
                    return new ExponentiationExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.GreaterThan:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.GreaterThan, ParseExpression(min_precedence, associativity));
                case TokenType.GreaterThanEquals:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.GreaterThanOrEqual, ParseExpression(min_precedence, associativity));
                case TokenType.LessThan:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.LessThan, ParseExpression(min_precedence, associativity));
                case TokenType.LessThanEquals:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.LessThanOrEqual, ParseExpression(min_precedence, associativity));
                case TokenType.EqualsEqualsEquals:
                    Consume();
                    return new EqualityExpression(lhs, EqualityOperator.StrictEquals, ParseExpression(min_precedence, associativity));
                case TokenType.ExclamationMarkEqualsEquals:
                    Consume();
                    return new EqualityExpression(lhs, EqualityOperator.StrictNotEquals, ParseExpression(min_precedence, associativity));
                case TokenType.EqualsEquals:
                    Consume();
                    return new EqualityExpression(lhs, EqualityOperator.Equals, ParseExpression(min_precedence, associativity));
                case TokenType.ExclamationMarkEquals:
                    Consume();
                    return new EqualityExpression(lhs, EqualityOperator.NotEquals, ParseExpression(min_precedence, associativity));
                case TokenType.Instanceof:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.Instanceof, ParseExpression(min_precedence, associativity));
                case TokenType.In:
                    Consume();
                    return new RelationalExpression(lhs, RelationalOperator.In, ParseExpression(min_precedence, associativity));
                case TokenType.Ampersand:
                    Consume();
                    return new BitwiseAndExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.Pipe:
                    Consume();
                    return new BitwiseOrExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.Caret:
                    Consume();
                    return new BitwiseXorExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.ParenOpen:
                    return ParseCallExpression(lhs);
                case TokenType.Equals:
                    Consume();
                    return new AssignmentExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.Period:
                    Consume();
                    return new DotMemberExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.BracketOpen:
                    {
                        Consume(TokenType.BracketOpen);
                        var expression = new IndexMemberExpression(lhs, ParseExpression(0));
                        Consume(TokenType.BracketClose);
                        return expression;
                    }
                case TokenType.PlusPlus:
                    Consume();
                    return new PostfixUpdateExpression(lhs, UpdateOperation.Increment);
                case TokenType.MinusMinus:
                    Consume();
                    return new PostfixUpdateExpression(lhs, UpdateOperation.Decrement);
                case TokenType.DoubleAmpersand:
                    Consume();
                    return new LogicalAndExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.DoublePipe:
                    Consume();
                    return new LogicalOrExpression(lhs, ParseExpression(min_precedence, associativity));
                case TokenType.QuestionMark:
                    return ParseConditionalExpression(lhs);
                default:
                    ParserState.HasErrors = true;
                    Expected("secondary expression (missing switch case)");
                    Consume();
                    //TODO error node
                    return Literal.NullLiteral;
            }
        }

        public ICallExpression ParseCallExpression(ICallExpression lhs)
        {
            Consume(TokenType.ParenOpen);
            var arguments = new List<IArgumentItem>();

            while (MatchExpression())
            {
                arguments.Add(ParseExpression(0));
                if (!Match(TokenType.Comma))
                    break;
                Consume();
            }

            Consume(TokenType.ParenClose);

            return new RecursiveCallExpression(lhs, new Arguments(arguments));
        }

        private bool MatchStatement()
        {
            var type = ParserState.CurentToken.Type;
            return MatchExpression()
                || type == TokenType.Function
                || type == TokenType.Return
                || type == TokenType.Let
                || type == TokenType.Class
                || type == TokenType.Delete
                || type == TokenType.Do
                || type == TokenType.If
                || type == TokenType.Throw
                || type == TokenType.Try
                || type == TokenType.While
                || type == TokenType.For
                || type == TokenType.Const
                || type == TokenType.CurlyOpen
                || type == TokenType.Switch
                || type == TokenType.Break
                || type == TokenType.Continue
                || type == TokenType.Var;
        }

        private bool MatchExpression()
        {
            var type = ParserState.CurentToken.Type;
            return type == TokenType.BoolLiteral
                || type == TokenType.NumericLiteral
                || type == TokenType.StringLiteral
                || type == TokenType.NullLiteral
                || type == TokenType.Identifier
                || type == TokenType.New
                || type == TokenType.CurlyOpen
                || type == TokenType.BracketOpen
                || type == TokenType.ParenOpen
                || type == TokenType.Function
                || type == TokenType.This
                || MatchUnaryPrefixedExpression();
        }

        private bool MatchUnaryPrefixedExpression()
        {
            var type = ParserState.CurentToken.Type;
            return type == TokenType.PlusPlus
                || type == TokenType.MinusMinus
                || type == TokenType.ExclamationMark
                || type == TokenType.Tilde
                || type == TokenType.Plus
                || type == TokenType.Minus
                || type == TokenType.Typeof
                || type == TokenType.Void;
        }

        private bool MatchSecondaryExpression()
        {
            var type = ParserState.CurentToken.Type;
            return type == TokenType.Plus
                || type == TokenType.PlusEquals
                || type == TokenType.Minus
                || type == TokenType.MinusEquals
                || type == TokenType.Asterisk
                || type == TokenType.AsteriskEquals
                || type == TokenType.Slash
                || type == TokenType.SlashEquals
                || type == TokenType.Percent
                || type == TokenType.DoubleAsterisk
                || type == TokenType.Equals
                || type == TokenType.EqualsEqualsEquals
                || type == TokenType.ExclamationMarkEqualsEquals
                || type == TokenType.EqualsEquals
                || type == TokenType.ExclamationMarkEquals
                || type == TokenType.GreaterThan
                || type == TokenType.GreaterThanEquals
                || type == TokenType.LessThan
                || type == TokenType.LessThanEquals
                || type == TokenType.ParenOpen
                || type == TokenType.Period
                || type == TokenType.BracketOpen
                || type == TokenType.PlusPlus
                || type == TokenType.MinusMinus
                || type == TokenType.Instanceof
                || type == TokenType.QuestionMark
                || type == TokenType.Ampersand
                || type == TokenType.Pipe
                || type == TokenType.Caret
                || type == TokenType.DoubleAmpersand
                || type == TokenType.DoublePipe
                || type == TokenType.DoubleQuestionMark;
        }

        private Token Consume()
        {
            var oldToken = ParserState.CurentToken;
            ParserState.CurentToken = ParserState.Lexer.Next();
            return oldToken;
        }

        private void ConsumeOrInsertSemicolon()
        {
            // Semicolon was found and will be Consumed
            if (Match(TokenType.Semicolon))
            {
                Consume();
                return;
            }
            // Insert semicolon if...
            // ...token is preceeded by one or more newlines
            if (ParserState.CurentToken.Trivia.Contains('\n'))
                return;
            // ...token is a closing curly brace
            if (Match(TokenType.CurlyClose))
                return;
            // ...token is eof
            if (Match(TokenType.Eof))
                return;

            // No rule for semicolon insertion applies -> syntax error
            Expected("Semicolon");
        }

        private Token Consume(TokenType expectedType)
        {
            if (ParserState.CurentToken.Type != expectedType)
            {
                Expected(expectedType.ToString());
            }
            return Consume();
        }

        private bool Match(TokenType type) => ParserState.CurentToken.Type == type;

        private bool Done() => Match(TokenType.Eof);

        private void Expected(string what)
        {
            ParserState.HasErrors = true;
            var currentToken = ParserState.CurentToken;
            Console.WriteLine($"Syntax Error: Unexpected token {currentToken}. Expected {what} (line: {currentToken.LineNumber}, column: {currentToken.LineColumn})");
        }
    }
    */
}
