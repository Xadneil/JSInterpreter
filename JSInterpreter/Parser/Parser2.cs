using JSInterpreter.AST;
using JSInterpreter.Lexer;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.Parser
{
    class Parser2
    {
        private ParserState ParserState;

        public Parser2(Lexer.Lexer lexer)
        {
            ParserState = new ParserState(lexer);
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
            var currentToken = ParserState.CurentToken;
            throw new ParseFailureExeption($"Syntax Error: Unexpected token {currentToken}. Expected {what} (line: {currentToken.LineNumber}, column: {currentToken.LineColumn})");
        }

        public IStatementListItem ParseStatementListItem()
        {
            if (MatchDeclaration())
                return ParseDeclaration();
            if (MatchStatement())
                return ParseStatement();
            Expected("Statement List Item");
            return null;
        }

        public bool MatchStatementListItem() => MatchDeclaration() || MatchStatement();

        public Statement ParseStatement()
        {
            switch (ParserState.CurentToken.Type)
            {
                case TokenType.BracketOpen:
                    return ParseBlock();
                case TokenType.Var:
                    return ParseVariableStatement();
                case TokenType.Semicolon:
                    Consume();
                    return new EmptyStatement();
                case TokenType.If:
                    return ParseIfStatement();
                case TokenType.For:
                case TokenType.While:
                case TokenType.Do:
                case TokenType.Switch:
                    return ParseBreakableStatement();
                case TokenType.Continue:
                    return ParseContinueStatement();
                case TokenType.Break:
                    return ParseBreakStatement();
                case TokenType.Return:
                    return ParseReturnStatement();
                case TokenType.Identifier:
                    //TODO I don't think this is right. Need a way to detect single identifier followed by a colon
                    return ParseLabelledStatement();
                case TokenType.Throw:
                    return ParseThrowStatement();
                case TokenType.Try:
                    return ParseTryStatement();
                case TokenType.Debugger:
                    Consume();
                    return new DebuggerStatement();
                default:
                    if (MatchExpression())
                    {
                        IExpression expr = ParseExpression();
                        ConsumeOrInsertSemicolon();
                        return new ExpressionStatement(expr);
                    }
                    Expected("Statement");
                    Consume();
                    return null;
            }
        }

        private bool MatchStatement()
        {
            var type = ParserState.CurentToken.Type;
            return MatchExpression()
                || type == TokenType.BracketOpen
                || type == TokenType.Var
                || type == TokenType.Semicolon
                || type == TokenType.If
                || type == TokenType.For
                || type == TokenType.While
                || type == TokenType.Do
                || type == TokenType.Switch
                || type == TokenType.Continue
                || type == TokenType.Break
                || type == TokenType.Return
                || type == TokenType.Identifier //TODO I don't think this is right. Need a way to detect single identifier followed by a colon
                || type == TokenType.Throw
                || type == TokenType.Try
                || type == TokenType.Debugger;
        }

        private Block ParseBlock()
        {
            Consume(TokenType.CurlyOpen);
            var statementList = ParseStatementList();
            Consume(TokenType.CurlyClose);

            return new Block(statementList);
        }

        private StatementList ParseStatementList()
        {
            var statementItems = new List<IStatementListItem>();
            while (MatchStatementListItem())
            {
                statementItems.Add(ParseStatementListItem());
            }

            return new StatementList(statementItems);
        }

        private VariableStatement ParseVariableStatement()
        {
            Consume(TokenType.Var);
            VariableDeclarationList variableDeclarationList = ParseVariableDeclarationList();
            if (Match(TokenType.Semicolon))
                Consume();
            return new VariableStatement(variableDeclarationList);
        }

        private VariableDeclarationList ParseVariableDeclarationList()
        {
            var list = new VariableDeclarationList();

            while (true)
            {
                var variableName = ParseBindingIdentifier();
                IAssignmentExpression initializer = null;
                if (Match(TokenType.Equals))
                    initializer = ParseInitializer();
                list.Add(new VariableDeclaration(variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return list;
        }

        private VariableDeclarationList ParseVariableDeclarationListWithInitialIdentifier(string firstIdentifier)
        {
            var list = new VariableDeclarationList();

            bool first = true;
            while (true)
            {
                string variableName;
                if (first)
                {
                    variableName = firstIdentifier;
                    first = false;
                }
                else
                    variableName = ParseBindingIdentifier();
                IAssignmentExpression initializer = null;
                if (Match(TokenType.Equals))
                    initializer = ParseInitializer();
                list.Add(new VariableDeclaration(variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return list;
        }


        private string ParseBindingIdentifier()
        {
            if (Match(TokenType.Identifier))
                return Consume().Value;
            else if (Match(TokenType.Yield))
                return "yield";
            else if (Match(TokenType.Await))
                return "await";
            Expected("Binding Identifier");
            return null;
        }

        private bool MatchBindingIdentifier() => Match(TokenType.Identifier) || Match(TokenType.Yield) || Match(TokenType.Await);

        private IAssignmentExpression ParseInitializer()
        {
            Consume(TokenType.Equals);
            return ParseAssignmentExpression();
        }

        private IfStatement ParseIfStatement()
        {
            Consume(TokenType.If);
            Consume(TokenType.ParenOpen);
            var test = ParseExpression();
            Consume(TokenType.ParenClose);
            var trueStatement = ParseStatement();
            if (Match(TokenType.Else))
            {
                Consume();
                var falseStatement = ParseStatement();
                return new IfStatement(test, trueStatement, falseStatement);
            }
            return new IfStatement(test, trueStatement);
        }

        private BreakableStatement ParseBreakableStatement()
        {
            if (MatchIterationStatement())
                return ParseIterationStatement();
            if (Match(TokenType.Switch))
                return ParseSwitchStatement();
            Expected("Iteration or Switch statement");
            return null;
        }

        private IterationStatement ParseIterationStatement()
        {
            if (Match(TokenType.Do))
            {
                Consume();
                Statement statement = ParseStatement();
                Consume(TokenType.While);
                Consume(TokenType.ParenOpen);
                IExpression test = ParseExpression();
                Consume(TokenType.ParenClose);
                if (Match(TokenType.Semicolon))
                    Consume();
                return new DoWhileIterationStatement(statement, test);
            }
            if (Match(TokenType.While))
            {
                Consume();
                Consume(TokenType.ParenOpen);
                IExpression test = ParseExpression();
                Consume(TokenType.ParenClose);
                Statement statement = ParseStatement();
                return new WhileIterationStatement(test, statement);
            }
            if (!Match(TokenType.For))
                Expected("For");
            Consume();
            //TODO await
            Consume(TokenType.ParenOpen);

            if (Match(TokenType.Semicolon) || MatchExpression())
            {
                IExpression start = null;
                if (!Match(TokenType.Semicolon))
                    start = ParseExpression();
                Consume(TokenType.Semicolon);
                IExpression test = null;
                if (!Match(TokenType.Semicolon))
                    test = ParseExpression();
                Consume(TokenType.Semicolon);
                IExpression update = null;
                if (!Match(TokenType.ParenClose))
                    update = ParseExpression();
                Consume(TokenType.ParenClose);
                var statement = ParseStatement();
                return new ForExpressionIterationStatement(start, test, update, statement);
            }
            if (Match(TokenType.Var))
            {
                Consume();

                var firstIdentifier = ParseBindingIdentifier();
                if (Match(TokenType.In))
                {
                    Consume();
                    var expression = ParseExpression();
                    Consume(TokenType.ParenClose);
                    var statement = ParseStatement();
                    return new ForInVarIterationStatement(new ForBinding(firstIdentifier), expression, statement);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    IAssignmentExpression expression = ParseAssignmentExpression();
                    Consume(TokenType.ParenClose);
                    var statement = ParseStatement();
                    return new ForOfVarIterationStatement(new ForBinding(firstIdentifier), expression, statement);
                }
                if (Match(TokenType.Equals))
                {
                    Consume();
                    var variableDeclarationList = ParseVariableDeclarationListWithInitialIdentifier(firstIdentifier);
                    Consume(TokenType.Semicolon);
                    IExpression test = null;
                    if (!Match(TokenType.Semicolon))
                        test = ParseExpression();
                    Consume(TokenType.Semicolon);
                    IExpression update = null;
                    if (!Match(TokenType.ParenClose))
                        update = ParseExpression();
                    Consume(TokenType.ParenClose);
                    var statement = ParseStatement();
                    return new ForVarIterationStatement(variableDeclarationList, test, update, statement);
                }
                Expected("=, in, or of");
                return null;
            }
            if (Match(TokenType.Let) || Match(TokenType.Const))
            {
                var isConst = Match(TokenType.Const);
                Consume();
                var forBinding = Consume(TokenType.Identifier).Value;
                if (Match(TokenType.In))
                {
                    Consume();
                    var expression = ParseExpression();
                    Consume(TokenType.ParenClose);
                    var statement = ParseStatement();
                    return new ForInLetConstIterationStatement(new ForDeclaration(isConst, forBinding), expression, statement);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    IAssignmentExpression expression = ParseAssignmentExpression();
                    Consume(TokenType.ParenClose);
                    var statement = ParseStatement();
                    return new ForOfLetConstIterationStatement(new ForDeclaration(isConst, forBinding), expression, statement);
                }
                Expected("in or of");
                return null;
            }
            if (!MatchLeftHandSide())
            {
                Expected("For LeftHandSide");
                return null;
            }
            ILeftHandSideExpression lhs = ParseLeftHandSideExpression();
            if (Match(TokenType.In))
            {
                Consume();
                var expression = ParseExpression();
                Consume(TokenType.ParenClose);
                var statement = ParseStatement();
                return new ForInLHSIterationStatement(lhs, expression, statement);
            }
            if (Match(TokenType.Of))
            {
                Consume();
                IAssignmentExpression expression = ParseAssignmentExpression();
                Consume(TokenType.ParenClose);
                var statement = ParseStatement();
                return new ForOfLHSIterationStatement(lhs, expression, statement);
            }
            Expected("in or of");
            return null;
        }

        private SwitchStatement ParseSwitchStatement()
        {
            Consume(TokenType.Switch);
            Consume(TokenType.ParenOpen);
            var expression = ParseExpression();
            Consume(TokenType.ParenClose);
            Consume(TokenType.CurlyOpen);

            if (Match(TokenType.CurlyClose))
            {
                Consume();
                return new SwitchStatement(expression, new CaseBlock(Utils.EmptyList<CaseClause>()));
            }

            bool foundDefault = false;
            var firstCases = new List<CaseClause>();
            DefaultClause defaultClause = null;
            var secondCases = new List<CaseClause>();
            while (true)
            {
                if (Match(TokenType.CurlyClose))
                    break;
                if (Match(TokenType.Default))
                {
                    foundDefault = true;
                    Consume(TokenType.Default);
                    Consume(TokenType.Colon);
                    var defaultStatements = ParseStatementList();
                    defaultClause = new DefaultClause(defaultStatements);
                    continue;
                }
                Consume(TokenType.Case);
                var matchExpression = ParseExpression();
                Consume(TokenType.Colon);
                var statements = ParseStatementList();
                var clause = new CaseClause(matchExpression, statements);
                if (foundDefault)
                    secondCases.Add(clause);
                else
                    firstCases.Add(clause);
            }

            Consume(TokenType.CurlyClose);
            return new SwitchStatement(expression, new CaseBlock(firstCases, defaultClause, secondCases));
        }

        private bool MatchIterationStatement() => Match(TokenType.Do) || Match(TokenType.While) || Match(TokenType.For);

        private ContinueStatement ParseContinueStatement()
        {
            Consume(TokenType.Continue);
            string label = null;
            if (!ParserState.PassedNewLine && MatchBindingIdentifier())
                label = ParseBindingIdentifier();
            if (Match(TokenType.Semicolon))
                Consume();
            if (label == null)
                return new ContinueStatement();
            return new ContinueStatement(label);
        }

        private BreakStatement ParseBreakStatement()
        {
            Consume(TokenType.Break);
            string label = null;
            if (!ParserState.PassedNewLine && MatchBindingIdentifier())
                label = ParseBindingIdentifier();
            if (Match(TokenType.Semicolon))
                Consume();
            if (label == null)
                return new BreakStatement();
            return new BreakStatement(label);
        }

        private ReturnStatement ParseReturnStatement()
        {
            Consume(TokenType.Return);
            IExpression expression = null;
            if (!ParserState.PassedNewLine && MatchExpression())
                expression = ParseExpression();
            if (Match(TokenType.Semicolon))
                Consume();
            if (expression == null)
                return new ReturnStatement();
            return new ReturnStatement(expression);
        }

        private LabelledStatement ParseLabelledStatement()
        {
            var label = ParseBindingIdentifier();
            Consume(TokenType.Colon);
            if (MatchStatement())
                return new LabelledStatement(new Identifier(label), ParseStatement());
            if (Match(TokenType.Function))
                return new LabelledStatement(new Identifier(label), ParseFunctionDeclaration());
            Expected("a labelled statement");
            return null;
        }

        private ThrowStatement ParseThrowStatement()
        {
            Consume(TokenType.Throw);
            if (ParserState.PassedNewLine)
                Expected("an expression to throw");
            var expression = ParseExpression();
            if (Match(TokenType.Semicolon))
                Consume(TokenType.Semicolon);
            return new ThrowStatement(expression);
        }

        private TryStatement ParseTryStatement()
        {
            Consume(TokenType.Try);
            var tryBlock = ParseBlock();

            (string catchParameter, Block catchBlock)? catchClause = null;
            Block finallyBlock = null;
            bool hasRequiredContent = false;
            if (Match(TokenType.Catch))
            {
                catchClause = ParseCatch();
                hasRequiredContent = true;
            }
            if (Match(TokenType.Finally))
            {
                Consume(TokenType.Finally);
                finallyBlock = ParseBlock();
                hasRequiredContent = true;
            }
            if (!hasRequiredContent)
                Expected("catch or finally clause");

            if (catchClause.HasValue && finallyBlock == null)
            {
                if (catchClause.Value.catchParameter != null)
                    return TryStatement.TryCatch(tryBlock, new Identifier(catchClause.Value.catchParameter), catchClause.Value.catchBlock);
                else
                    return TryStatement.TryCatch(tryBlock, catchClause.Value.catchBlock);
            }
            else if (!catchClause.HasValue && finallyBlock != null)
                return TryStatement.TryFinally(tryBlock, finallyBlock);
            else
            {
                if (catchClause.Value.catchParameter != null)
                    return TryStatement.TryCatchFinally(tryBlock, new Identifier(catchClause.Value.catchParameter), catchClause.Value.catchBlock, finallyBlock);
                else
                    return TryStatement.TryCatchFinally(tryBlock, catchClause.Value.catchBlock, finallyBlock);
            }
        }

        private (string, Block) ParseCatch()
        {
            Consume();
            string catchParameter = null;
            if (Match(TokenType.ParenOpen))
            {
                Consume();
                catchParameter = ParseBindingIdentifier();
                Consume(TokenType.ParenClose);
            }
            var block = ParseBlock();
            return (catchParameter, block);
        }

        private Declaration ParseDeclaration()
        {
            if (MatchHoistableDeclaration())
                return ParseHoistableDeclaration();
            //TODO class
            if (MatchLexicalDeclaration())
                return ParseLexicalDeclaration();
            Expected("hoistable declaration");
            return null;
        }

        private bool MatchDeclaration() => MatchHoistableDeclaration() || MatchLexicalDeclaration();

        private HoistableDeclaration ParseHoistableDeclaration()
        {
            if (MatchFunctionDeclaration())
                return ParseFunctionDeclaration();
            //todo generator and async
            Expected("function declaration");
            return null;
        }

        private bool MatchHoistableDeclaration() => MatchFunctionDeclaration();

        private FunctionDeclaration ParseFunctionDeclaration()
        {
            Consume(TokenType.Function);
            string functionName = null;
            if (MatchBindingIdentifier())
                functionName = ParseBindingIdentifier();
            Consume(TokenType.ParenOpen);
            var parameters = ParseFormalParameters();
            Consume(TokenType.ParenClose);
            Consume(TokenType.CurlyOpen);
            var statements = new FunctionStatementList(ParseStatementList().statements);
            Consume(TokenType.CurlyClose);

            if (functionName != null)
                return new FunctionDeclaration(new Identifier(functionName), parameters, statements);
            return new FunctionDeclaration(parameters, statements);
        }

        private FormalParameters ParseFormalParameters()
        {
            string restParameter = null;
            var list = new List<FormalParameter>();
            while (true)
            {
                if (Match(TokenType.Ellipsis))
                {
                    Consume();
                    restParameter = ParseBindingIdentifier();
                    continue;
                }

                if (!MatchBindingIdentifier())
                {
                    Expected("a parameter");
                    return null;
                }

                if (restParameter != null)
                {
                    Expected("no more parameters after the rest parameter");
                    return null;
                }

                var name = ParseBindingIdentifier();
                if (Match(TokenType.Equals))
                {
                    var initializer = ParseInitializer();
                    list.Add(new FormalParameter(new Identifier(name), initializer));
                }
                else
                    list.Add(new FormalParameter(new Identifier(name)));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }
            if (restParameter != null)
            {
                return new FormalParameters(list, new Identifier(restParameter));
            }
            return new FormalParameters(list);
        }

        private bool MatchFunctionDeclaration() => Match(TokenType.Function);

        private LexicalDeclaration ParseLexicalDeclaration()
        {
            LexicalDeclarationType lexicalDeclarationType;
            if (Match(TokenType.Let))
                lexicalDeclarationType = LexicalDeclarationType.Let;
            else if (Match(TokenType.Const))
                lexicalDeclarationType = LexicalDeclarationType.Const;
            else
            {
                Expected("let or const");
                return null;
            }
            Consume();

            var list = new List<LexicalDeclarationItem>();

            while (true)
            {
                var variableName = ParseBindingIdentifier();
                IAssignmentExpression initializer = null;
                if (Match(TokenType.Equals))
                    initializer = ParseInitializer();
                else if (lexicalDeclarationType == LexicalDeclarationType.Const)
                {
                    Expected("an initializer for a const declaration");
                    return null;
                }
                list.Add(new LexicalDeclarationItem(lexicalDeclarationType, variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return new LexicalDeclaration(lexicalDeclarationType, list);
        }

        private bool MatchLexicalDeclaration() => Match(TokenType.Let) || Match(TokenType.Const);

        private IExpression ParseExpression()
        {
            IAssignmentExpression expression = ParseAssignmentExpression();
            if (!Match(TokenType.Comma))
                return expression;
            var list = new List<IAssignmentExpression>() { expression };
            do
            {
                Consume(TokenType.Comma);
                list.Add(ParseAssignmentExpression());
            } while (Match(TokenType.Comma));

            return new CommaExpression(list);
        }

        private bool MatchExpression() => MatchAssignmentExpression();

        private IAssignmentExpression ParseAssignmentExpression()
        {
            if (MatchConditionalExpression())
                return ParseConditionalExpression();
            if (MatchLeftHandSideExpression())
            {
                ILeftHandSideExpression lhs = ParseLeftHandSideExpression();
                if (Match(TokenType.Equals))
                {
                    Consume();
                    var rhs = ParseAssignmentExpression();
                    return new AssignmentExpression(lhs, rhs);
                }
                if (MatchAssignmentOperator())
                {
                    var op = ParseAssignmentOperator();
                    var rhs = ParseAssignmentExpression();
                    return new OperatorAssignmentExpression(lhs, op, rhs);
                }
            }
            Expected("assignment expression");
            return null;
        }

        public bool MatchAssignmentExpression() => MatchConditionalExpression() || MatchLeftHandSideExpression();

        private bool MatchAssignmentOperator()
        {
            return Match(TokenType.AsteriskEquals) ||
                Match(TokenType.SlashEquals) ||
                Match(TokenType.PercentEquals) ||
                Match(TokenType.PlusEquals) ||
                Match(TokenType.MinusEquals) ||
                Match(TokenType.ShiftLeftEquals) ||
                Match(TokenType.ShiftRightEquals) ||
                Match(TokenType.UnsignedShiftRightEquals) ||
                Match(TokenType.AmpersandEquals) ||
                Match(TokenType.CaretEquals) ||
                Match(TokenType.PipeEquals) ||
                Match(TokenType.AsteriskAsteriskEquals);
        }

        private AssignmentOperator ParseAssignmentOperator()
        {
            var type = ParserState.CurentToken.Type;
            switch (type)
            {
                case TokenType.AsteriskEquals:
                    return AssignmentOperator.Multiply;
                case TokenType.SlashEquals:
                    return AssignmentOperator.Divide;
                case TokenType.PercentEquals:
                    return AssignmentOperator.Modulus;
                case TokenType.PlusEquals:
                    return AssignmentOperator.Plus;
                case TokenType.MinusEquals:
                    return AssignmentOperator.Minus;
                case TokenType.ShiftLeftEquals:
                    return AssignmentOperator.ShiftLeft;
                case TokenType.ShiftRightEquals:
                    return AssignmentOperator.ShiftRight;
                case TokenType.UnsignedShiftRightEquals:
                    return AssignmentOperator.ShiftRightUnsigned;
                case TokenType.AmpersandEquals:
                    return AssignmentOperator.BitwiseAnd;
                case TokenType.CaretEquals:
                    return AssignmentOperator.BitwiseXor;
                case TokenType.PipeEquals:
                    return AssignmentOperator.BitwiseOr;
                case TokenType.AsteriskAsteriskEquals:
                    return AssignmentOperator.Exponentiate;
            }
            Expected("an assignment operator, like +=");
            return AssignmentOperator.Multiply;
        }

        private IConditionalExpression ParseConditionalExpression()
        {
            if (MatchLogicalOrExpression())
            {
                var expression = ParseLogicalOrExpression();
                if (Match(TokenType.QuestionMark))
                {
                    Consume();
                    var trueExpr = ParseAssignmentExpression();
                    Consume(TokenType.Colon);
                    var falseExpr = ParseAssignmentExpression();
                    return new ConditionalExpression(expression, trueExpr, falseExpr);
                }
                return expression;
            }
            Expected("logical or expression");
            return null;
        }

        private bool MatchConditionalExpression() => MatchLogicalOrExpression();

        private ILogicalOrExpression ParseLogicalOrExpression()
        {
            var lhs = ParseLogicalAndExpression();
            var tail = ParseLogicalOrTail();

            if (tail == null)
                return lhs;

            var or = new LogicalOrExpression(lhs, tail.RHS);
            if (tail.Tail != null)
            {
                tail = tail.Tail;
                or = new LogicalOrExpression(or, tail.RHS);
            }
            return or;
        }

        private TailContainer<ILogicalAndExpression> ParseLogicalOrTail()
        {
            if (!Match(TokenType.DoublePipe))
                return null;
            Consume();
            var rhs = ParseLogicalAndExpression();
            var tail = ParseLogicalOrTail();
            return new TailContainer<ILogicalAndExpression>(rhs, tail);
        }

        private bool MatchLogicalOrExpression() => MatchLogicalAndExpression();
    }
}
