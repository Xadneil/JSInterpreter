using JSInterpreter.AST;
using JSInterpreter.Lexer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace JSInterpreter.Parser
{
    public class Parser
    {
        private bool success;
        private ParseFailureException exception;
        private readonly Lexer.Lexer lexer;

        public Parser(string source)
        {
            lexer = new Lexer.Lexer(source);
            lexer.Next();
        }

        private Parser Parse(Action what)
        {
            lexer.SaveState();
            try
            {
                what();
                success = true;
                exception = null;
                lexer.DeleteState();
            }
            catch (ParseFailureException e)
            {
                lexer.RestoreState();
                success = false;
                if (exception == null)
                    exception = e;
            }
            return this;
        }

        private Parser Or(Action what)
        {
            if (!success)
            {
                var ret = Parse(what);
                if (success)
                    exception = null;
                return ret;
            }
            return this;
        }

        private Parser OrThrow(string message)
        {
            if (!success)
            {
                Expected(message);
            }
            return this;
        }

        private Token Consume()
        {
            var oldToken = lexer.CurrentToken;
            lexer.Next();
            return oldToken;
        }

        private Token ConsumeNextRegex(Token regexStart)
        {
            var oldToken = lexer.CurrentToken;
            lexer.NextRegex(regexStart);
            return oldToken;
        }


        private Token CurrentToken => lexer.CurrentToken;

        private void ConsumeOrInsertSemicolon()
        {
            // Semicolon was found and will be Consumed
            if (Match(TokenType.Semicolon))
            {
                Consume();
                return;
            }
            if (CurrentToken.Type == TokenType.Eof)
                return;
            // Insert semicolon if...
            // ...token is preceeded by one or more newlines
            if (CurrentToken.Trivia.Contains('\n'))
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
            if (CurrentToken.Type != expectedType)
            {
                Expected(expectedType.ToString());
            }
            return Consume();
        }

        private bool Match(TokenType type) => CurrentToken.Type == type;

        private void Expected(string what)
        {
            var currentToken = CurrentToken;
            throw new ParseFailureException($"Syntax Error: Unexpected token {currentToken.Type}. Expected {what} (line: {currentToken.LineNumber}, column: {currentToken.LineColumn})");
        }

        public Script ParseScript()
        {
            var statementList = ParseStatementList();
            if (CurrentToken.Type != TokenType.Eof && exception != null)
                throw exception;
            return new Script(statementList);
        }

        private IStatementListItem ParseStatementListItem()
        {
            IStatementListItem item = null;
            Parse(() =>
            {
                item = ParseDeclaration();
            }).Or(() =>
            {
                item = ParseStatement();
            }).OrThrow("Statement List Item");
            return item;
        }

        private Statement ParseStatement()
        {
            switch (CurrentToken.Type)
            {
                case TokenType.CurlyOpen:
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
                case TokenType.Throw:
                    return ParseThrowStatement();
                case TokenType.Try:
                    return ParseTryStatement();
                case TokenType.Debugger:
                    Consume();
                    return new DebuggerStatement();
                default:
                    Statement statement = null;
                    Parse(() =>
                    {
                        statement = ParseLabelledStatement();
                    }).Or(() =>
                    {
                        IExpression expr = ParseExpression();
                        ConsumeOrInsertSemicolon();
                        statement = new ExpressionStatement(expr);
                    }).OrThrow("Catch-all Expression Statement");
                    return statement;
            }
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
            while (true)
            {
                IStatementListItem item = null;
                var parser = Parse(() =>
                {
                    item = ParseStatementListItem();
                });
                if (item == null)
                    break;
                statementItems.Add(item);
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
            if (Match(TokenType.Switch))
                return ParseSwitchStatement();

            IterationStatement iterationStatement = null;
            Parse(() =>
            {
                iterationStatement = ParseIterationStatement();
            }).OrThrow("Iteration or Switch statement");
            return iterationStatement;
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

            ForExpressionIterationStatement forExpressionIterationStatement = null;
            Parse(() =>
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
                forExpressionIterationStatement = new ForExpressionIterationStatement(start, test, update, statement);
            });
            if (forExpressionIterationStatement != null) return forExpressionIterationStatement;
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
                    //don't consume the =, since ParseInitializer will do it.
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
            ILeftHandSideExpression lhs = null;
            Parse(() =>
            {
                lhs = ParseLeftHandSideExpression();
            }).OrThrow("For LeftHandSide");

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

        private ContinueStatement ParseContinueStatement()
        {
            Consume(TokenType.Continue);
            string label = null;
            if (!CurrentToken.PassedNewLine && MatchBindingIdentifier())
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
            if (!CurrentToken.PassedNewLine && MatchBindingIdentifier())
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
            if (!CurrentToken.PassedNewLine)
                Parse(() =>
                {
                    expression = ParseExpression();
                });
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
            if (Match(TokenType.Function))
                return new LabelledStatement(new Identifier(label), ParseFunctionDeclaration());
            return new LabelledStatement(new Identifier(label), ParseStatement());
        }

        private ThrowStatement ParseThrowStatement()
        {
            Consume(TokenType.Throw);
            if (CurrentToken.PassedNewLine)
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
            Declaration declaration = null;
            Parse(() =>
            {
                declaration = ParseHoistableDeclaration();
            }).Or(() =>
            {
                //TODO class
                declaration = ParseLexicalDeclaration();
            }).OrThrow("declaration");
            return declaration;
        }

        private HoistableDeclaration ParseHoistableDeclaration()
        {
            HoistableDeclaration declaration = null;
            Parse(() =>
            {
                declaration = ParseFunctionDeclaration();
            }).OrThrow("function declaration");
            //todo generator and async
            return declaration;
        }

        private FunctionDeclaration ParseFunctionDeclaration()
        {
            Consume(TokenType.Function);
            string functionName = null;
            if (MatchBindingIdentifier())
                functionName = ParseBindingIdentifier();
            Consume(TokenType.ParenOpen);
            FormalParameters parameters;
            if (Match(TokenType.ParenClose))
                parameters = new FormalParameters();
            else
                parameters = ParseFormalParameters();
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

        private IAssignmentExpression ParseAssignmentExpression()
        {
            IAssignmentExpression expression = null;
            Parse(() =>
            {
                ILeftHandSideExpression lhs = ParseLeftHandSideExpression();
                if (Match(TokenType.Equals))
                {
                    Consume();
                    var rhs = ParseAssignmentExpression();
                    expression = new AssignmentExpression(lhs, rhs);
                    return;
                }
                if (MatchAssignmentOperator())
                {
                    var op = ParseAssignmentOperator();
                    var rhs = ParseAssignmentExpression();
                    expression = new OperatorAssignmentExpression(lhs, op, rhs);
                    return;
                }
                throw new ParseFailureException();
            }).Or(() =>
            {
                expression = ParseConditionalExpression();
            }).OrThrow("assignment expression");
            return expression;
        }

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
            var type = Consume().Type;
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
            IConditionalExpression conditionalExpression = null;
            Parse(() =>
            {
                var expression = ParseLogicalOrExpression();
                if (Match(TokenType.QuestionMark))
                {
                    Consume();
                    var trueExpr = ParseAssignmentExpression();
                    Consume(TokenType.Colon);
                    var falseExpr = ParseAssignmentExpression();
                    conditionalExpression = new ConditionalExpression(expression, trueExpr, falseExpr);
                    return;
                }
                conditionalExpression = expression;
            }).OrThrow("conditional expression");
            return conditionalExpression;
        }

        private ILogicalOrExpression ParseLogicalOrExpression()
        {
            var lhs = ParseLogicalAndExpression();
            var tail = ParseLogicalOrTail();

            if (tail == null)
                return lhs;

            var or = new LogicalOrExpression(lhs, tail.RHS);
            while (tail.Tail != null)
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

        private ILogicalAndExpression ParseLogicalAndExpression()
        {
            var lhs = ParseBitwiseOrExpression();
            var tail = ParseLogicalAndTail();

            if (tail == null)
                return lhs;

            var and = new LogicalAndExpression(lhs, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                and = new LogicalAndExpression(and, tail.RHS);
            }
            return and;
        }

        private TailContainer<IBitwiseOrExpression> ParseLogicalAndTail()
        {
            if (!Match(TokenType.DoubleAmpersand))
                return null;
            Consume();
            var rhs = ParseBitwiseOrExpression();
            var tail = ParseLogicalAndTail();
            return new TailContainer<IBitwiseOrExpression>(rhs, tail);
        }

        private IBitwiseOrExpression ParseBitwiseOrExpression()
        {
            var lhs = ParseBitwiseXorExpression();
            var tail = ParseBitwiseOrTail();

            if (tail == null)
                return lhs;

            var bitwiseOr = new BitwiseOrExpression(lhs, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseOr = new BitwiseOrExpression(bitwiseOr, tail.RHS);
            }
            return bitwiseOr;
        }

        private TailContainer<IBitwiseXorExpression> ParseBitwiseOrTail()
        {
            if (!Match(TokenType.Pipe))
                return null;
            Consume();
            var rhs = ParseBitwiseXorExpression();
            var tail = ParseBitwiseOrTail();
            return new TailContainer<IBitwiseXorExpression>(rhs, tail);
        }

        private IBitwiseXorExpression ParseBitwiseXorExpression()
        {
            var lhs = ParseBitwiseAndExpression();
            var tail = ParseBitwiseXorTail();

            if (tail == null)
                return lhs;

            var bitwiseXor = new BitwiseXorExpression(lhs, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseXor = new BitwiseXorExpression(bitwiseXor, tail.RHS);
            }
            return bitwiseXor;
        }

        private TailContainer<IBitwiseAndExpression> ParseBitwiseXorTail()
        {
            if (!Match(TokenType.Caret))
                return null;
            Consume();
            var rhs = ParseBitwiseAndExpression();
            var tail = ParseBitwiseXorTail();
            return new TailContainer<IBitwiseAndExpression>(rhs, tail);
        }

        private IBitwiseAndExpression ParseBitwiseAndExpression()
        {
            var lhs = ParseEqualityExpression();
            var tail = ParseBitwiseAndTail();

            if (tail == null)
                return lhs;

            var bitwiseAnd = new BitwiseAndExpression(lhs, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseAnd = new BitwiseAndExpression(bitwiseAnd, tail.RHS);
            }
            return bitwiseAnd;
        }

        private TailContainer<IEqualityExpression> ParseBitwiseAndTail()
        {
            if (!Match(TokenType.Ampersand))
                return null;
            Consume();
            var rhs = ParseEqualityExpression();
            var tail = ParseBitwiseAndTail();
            return new TailContainer<IEqualityExpression>(rhs, tail);
        }

        private IEqualityExpression ParseEqualityExpression()
        {
            var lhs = ParseRelationalExpression();
            var tail = ParseEqualityTail();

            if (tail == null)
                return lhs;

            var expression = new EqualityExpression(lhs, tail.Op, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                expression = new EqualityExpression(expression, tail.Op, tail.RHS);
            }
            return expression;
        }

        private TailOperatorContainer<IRelationalExpression, EqualityOperator> ParseEqualityTail()
        {
            if (!MatchEqualityOperator())
                return null;
            var op = ParseEqualityOperator();
            var rhs = ParseRelationalExpression();
            var tail = ParseEqualityTail();
            return new TailOperatorContainer<IRelationalExpression, EqualityOperator>(rhs, op, tail);
        }

        private EqualityOperator ParseEqualityOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.EqualsEquals:
                    return EqualityOperator.Equals;
                case TokenType.ExclamationMarkEquals:
                    return EqualityOperator.NotEquals;
                case TokenType.EqualsEqualsEquals:
                    return EqualityOperator.StrictEquals;
                case TokenType.ExclamationMarkEqualsEquals:
                    return EqualityOperator.StrictNotEquals;
            }
            Expected("equality operator like ==");
            return EqualityOperator.Equals;
        }

        private bool MatchEqualityOperator()
        {
            return Match(TokenType.EqualsEquals) ||
                Match(TokenType.ExclamationMarkEquals) ||
                Match(TokenType.EqualsEqualsEquals) ||
                Match(TokenType.ExclamationMarkEqualsEquals);
        }

        private IRelationalExpression ParseRelationalExpression()
        {
            var lhs = ParseShiftExpression();
            var tail = ParseRelationalTail();

            if (tail == null)
                return lhs;

            var expression = new RelationalExpression(lhs, tail.Op, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                expression = new RelationalExpression(expression, tail.Op, tail.RHS);
            }
            return expression;
        }

        private TailOperatorContainer<IShiftExpression, RelationalOperator> ParseRelationalTail()
        {
            if (!MatchRelationalOperator())
                return null;
            var op = ParseRelationalOperator();
            var rhs = ParseShiftExpression();
            var tail = ParseRelationalTail();
            return new TailOperatorContainer<IShiftExpression, RelationalOperator>(rhs, op, tail);
        }

        private RelationalOperator ParseRelationalOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.LessThan:
                    return RelationalOperator.LessThan;
                case TokenType.GreaterThan:
                    return RelationalOperator.GreaterThan;
                case TokenType.LessThanEquals:
                    return RelationalOperator.LessThanOrEqual;
                case TokenType.GreaterThanEquals:
                    return RelationalOperator.GreaterThanOrEqual;
                case TokenType.Instanceof:
                    return RelationalOperator.Instanceof;
                case TokenType.In:
                    return RelationalOperator.In;
            }
            Expected("relational operator like <");
            return RelationalOperator.LessThan;
        }

        private bool MatchRelationalOperator()
        {
            return Match(TokenType.LessThan) ||
                Match(TokenType.GreaterThan) ||
                Match(TokenType.LessThanEquals) ||
                Match(TokenType.GreaterThanEquals) ||
                Match(TokenType.Instanceof) ||
                Match(TokenType.In);
        }

        private IShiftExpression ParseShiftExpression()
        {
            var lhs = ParseAdditiveExpression();
            var tail = ParseShiftTail();

            if (tail == null)
                return lhs;

            var expression = new ShiftExpression(lhs, tail.Op, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                expression = new ShiftExpression(expression, tail.Op, tail.RHS);
            }
            return expression;
        }

        private TailOperatorContainer<IAdditiveExpression, ShiftOperator> ParseShiftTail()
        {
            if (!MatchShiftOperator())
                return null;
            var op = ParseShiftOperator();
            var rhs = ParseAdditiveExpression();
            var tail = ParseShiftTail();
            return new TailOperatorContainer<IAdditiveExpression, ShiftOperator>(rhs, op, tail);
        }

        private ShiftOperator ParseShiftOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.ShiftLeft:
                    return ShiftOperator.ShiftLeft;
                case TokenType.ShiftRight:
                    return ShiftOperator.ShiftRight;
                case TokenType.UnsignedShiftRight:
                    return ShiftOperator.ShiftRightUnsigned;
            }
            Expected("shift operator like <<");
            return ShiftOperator.ShiftLeft;
        }

        private bool MatchShiftOperator()
        {
            return Match(TokenType.ShiftLeft) ||
                Match(TokenType.ShiftRight) ||
                Match(TokenType.UnsignedShiftRight);
        }

        private IAdditiveExpression ParseAdditiveExpression()
        {
            var lhs = ParseMultiplicativeExpression();
            var tail = ParseAdditiveTail();

            if (tail == null)
                return lhs;

            var expression = new AdditiveExpression(lhs, tail.Op, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                expression = new AdditiveExpression(expression, tail.Op, tail.RHS);
            }
            return expression;
        }

        private TailOperatorContainer<IMultiplicativeExpression, AdditiveOperator> ParseAdditiveTail()
        {
            if (!MatchAdditiveOperator())
                return null;
            var op = ParseAdditiveOperator();
            var rhs = ParseMultiplicativeExpression();
            var tail = ParseAdditiveTail();
            return new TailOperatorContainer<IMultiplicativeExpression, AdditiveOperator>(rhs, op, tail);
        }

        private AdditiveOperator ParseAdditiveOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.Plus:
                    return AdditiveOperator.Add;
                case TokenType.Minus:
                    return AdditiveOperator.Subtract;
            }
            Expected("additive operator like +");
            return AdditiveOperator.Add;
        }

        private bool MatchAdditiveOperator()
        {
            return Match(TokenType.Plus) || Match(TokenType.Minus);
        }

        private IMultiplicativeExpression ParseMultiplicativeExpression()
        {
            var lhs = ParseExponentiationExpression();
            var tail = ParseMultiplicativeTail();

            if (tail == null)
                return lhs;

            var expression = new MultiplicativeExpression(lhs, tail.Op, tail.RHS);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                expression = new MultiplicativeExpression(expression, tail.Op, tail.RHS);
            }
            return expression;
        }

        private TailOperatorContainer<IExponentiationExpression, MultiplicativeOperator> ParseMultiplicativeTail()
        {
            if (!MatchMultiplicativeOperator())
                return null;
            var op = ParseMultiplicativeOperator();
            var rhs = ParseExponentiationExpression();
            var tail = ParseMultiplicativeTail();
            return new TailOperatorContainer<IExponentiationExpression, MultiplicativeOperator>(rhs, op, tail);
        }

        private MultiplicativeOperator ParseMultiplicativeOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.Asterisk:
                    return MultiplicativeOperator.Multiply;
                case TokenType.Slash:
                    return MultiplicativeOperator.Divide;
                case TokenType.Percent:
                    return MultiplicativeOperator.Modulus;
            }
            Expected("multiplicative operator like *");
            return MultiplicativeOperator.Multiply;
        }

        private bool MatchMultiplicativeOperator()
        {
            return Match(TokenType.Asterisk) ||
                Match(TokenType.Slash) ||
                Match(TokenType.Percent);
        }

        private IExponentiationExpression ParseExponentiationExpression()
        {
            IExponentiationExpression expression = null;
            Parse(() =>
            {
                IUpdateExpression lhs = ParseUpdateExpression();
                Consume(TokenType.DoubleAsterisk);
                var rhs = ParseExponentiationExpression();
                expression = new ExponentiationExpression(lhs, rhs);
            }).Or(() =>
            {
                expression = ParseUnaryExpression();
            }).OrThrow("Update or Unary expression");
            return expression;
        }

        private IUnaryExpression ParseUnaryExpression()
        {
            if (!MatchUnaryOperator())
            {
                return ParseUpdateExpression();
            }
            var op = ParseUnaryOperator();
            var expression = ParseUnaryExpression();
            return new OperatorUnaryExpression(op, expression);
        }

        private UnaryOperator ParseUnaryOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.Delete:
                    return UnaryOperator.Delete;
                case TokenType.Void:
                    return UnaryOperator.Void;
                case TokenType.Typeof:
                    return UnaryOperator.Typeof;
                case TokenType.Plus:
                    return UnaryOperator.Plus;
                case TokenType.Minus:
                    return UnaryOperator.Negate;
                case TokenType.Tilde:
                    return UnaryOperator.BitwiseNot;
                case TokenType.ExclamationMark:
                    return UnaryOperator.LogicalNot;
            }
            Expected("unary opertor like +, -, or delete");
            return UnaryOperator.Delete;
        }

        private bool MatchUnaryOperator()
        {
            return
                Match(TokenType.Delete) ||
                Match(TokenType.Void) ||
                Match(TokenType.Typeof) ||
                Match(TokenType.Plus) ||
                Match(TokenType.Minus) ||
                Match(TokenType.Tilde) ||
                Match(TokenType.ExclamationMark);
        }

        private IUpdateExpression ParseUpdateExpression()
        {
            if (MatchUpdateOperator())
            {
                var op = ParseUpdateOperator();
                var expression = ParseUnaryExpression();
                return new PrefixUpdateExpression(expression, op);
            }
            IUpdateExpression updateExpression = null;
            Parse(() =>
            {
                var lhs = ParseLeftHandSideExpression();
                if (MatchUpdateOperator() && !CurrentToken.PassedNewLine)
                {
                    var op = ParseUpdateOperator();
                    updateExpression = new PostfixUpdateExpression(lhs, op);
                    return;
                }
                updateExpression = lhs;
            }).OrThrow("update expression");
            return updateExpression;
        }

        private UpdateOperator ParseUpdateOperator()
        {
            var type = Consume().Type;
            switch (type)
            {
                case TokenType.PlusPlus:
                    return UpdateOperator.Increment;
                case TokenType.MinusMinus:
                    return UpdateOperator.Decrement;
                default:
                    Expected("++ or --");
                    return UpdateOperator.Increment;
            }
        }

        private bool MatchUpdateOperator()
        {
            return Match(TokenType.PlusPlus) || Match(TokenType.MinusMinus);
        }

        private ILeftHandSideExpression ParseLeftHandSideExpression()
        {
            ILeftHandSideExpression lhs = null;
            Parse(() =>
            {
                lhs = ParseCallExpression();
            }).Or(() =>
            {
                lhs = ParseNewExpression();
            }).OrThrow("left hand side expression");
            return lhs;
        }

        private INewExpression ParseNewExpression()
        {
            if (Match(TokenType.New))
            {
                Consume();
                var expression = ParseNewExpression();
                return new NewExpression(expression);
            }
            return ParseMemberExpression();
        }

        private ICallExpression ParseCallExpression()
        {
            ICallExpression lhs = null;
            Parse(() =>
            {
                lhs = ParseCoveredCallExpression();
            }).Or(() =>
            {
                lhs = ParseSuperCallExpression();
            }).OrThrow("call expression");
            var tail = ParseCallExpressionTail();
            if (tail == null)
                return lhs;

            do
            {
                if (tail.Arguments != null)
                    lhs = new RecursiveCallExpression(lhs, tail.Arguments);
                else if (tail.IdentifierName != null)
                    lhs = new DotCallExpression(lhs, tail.IdentifierName);
                else if (tail.Expression != null)
                    lhs = new IndexCallExpression(lhs, tail.Expression);
                else
                {
                    Expected("call expression tail component");
                    return null;
                }

                tail = tail.Tail;
            } while (tail != null);

            return lhs;
        }

        private CallExpressionTail ParseCallExpressionTail()
        {
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                Consume(TokenType.BracketClose);
                return new CallExpressionTail(expression, ParseCallExpressionTail());
            }
            else if (Match(TokenType.Period))
            {
                Consume();
                var identifierName = Consume().Value;
                return new CallExpressionTail(identifierName, ParseCallExpressionTail());
            }
            Arguments arguments = null;
            Parse(() =>
            {
                arguments = ParseArguments();
            });
            if (arguments != null)
            {
                return new CallExpressionTail(arguments, ParseCallExpressionTail());
            }
            return null;
        }

        private SuperCall ParseSuperCallExpression()
        {
            Consume(TokenType.Super);
            return new SuperCall(ParseArguments());
        }

        private MemberCallExpression ParseCoveredCallExpression()
        {
            var memberExpression = ParseMemberExpression();
            var arguments = ParseArguments();
            return new MemberCallExpression(memberExpression, arguments);
        }

        private Arguments ParseArguments()
        {
            Consume(TokenType.ParenOpen);
            if (Match(TokenType.ParenClose))
            {
                Consume();
                return new Arguments(Utils.EmptyList<IArgumentItem>());
            }
            var arguments = new List<IArgumentItem>();
            while (true)
            {
                var item = ParseArgumentItem();
                arguments.Add(item);
                if (Match(TokenType.Comma))
                    Consume();
                if (Match(TokenType.ParenClose))
                {
                    Consume();
                    break;
                }
            }
            return new Arguments(arguments);
        }

        private IArgumentItem ParseArgumentItem()
        {
            if (Match(TokenType.Ellipsis))
            {
                Consume();
                return new SpreadElement(ParseAssignmentExpression());
            }
            return ParseAssignmentExpression();
        }

        private IMemberExpression ParseMemberExpression()
        {
            if (Match(TokenType.New))
            {
                Consume();
                return new NewMemberExpression(ParseMemberExpression(), ParseArguments());
            }
            if (Match(TokenType.Super))
            {
                return ParseSuperProperty();
            }
            IMemberExpression lhs = ParsePrimaryExpression();
            var tail = ParseMemberExpressionTail();
            if (tail == null)
                return lhs;

            do
            {
                if (tail.IdentifierName != null)
                    lhs = new DotMemberExpression(lhs, tail.IdentifierName);
                else if (tail.Expression != null)
                    lhs = new IndexMemberExpression(lhs, tail.Expression);
                else
                {
                    Expected("member expression tail component");
                    return null;
                }

                tail = tail.Tail;
            } while (tail != null);

            return lhs;
        }

        private MemberExpressionTail ParseMemberExpressionTail()
        {
            if (!Match(TokenType.BracketOpen) && !Match(TokenType.Period))
                return null;
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                Consume(TokenType.BracketClose);
                return new MemberExpressionTail(expression, ParseMemberExpressionTail());
            }
            if (Match(TokenType.Period))
            {
                Consume();
                return new MemberExpressionTail(Consume().Value, ParseMemberExpressionTail());
            }
            Expected("member expression tail");
            return null;
        }

        private IMemberExpression ParseSuperProperty()
        {
            Consume(TokenType.Super);
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                Consume(TokenType.BracketClose);
                return new SuperIndexMemberExpression(expression);
            }
            if (Match(TokenType.Period))
            {
                Consume();
                return new SuperDotMemberExpression(Consume().Value);
            }
            Expected("super property");
            return null;
        }

        private IPrimaryExpression ParsePrimaryExpression()
        {
            switch (CurrentToken.Type)
            {
                case TokenType.This:
                    Consume();
                    return ThisExpression.Instance;
                case TokenType.Identifier:
                    return new IdentifierReference(new Identifier(Consume().Value));
                case TokenType.BoolLiteral:
                    return new Literal(Consume().BoolValue());
                case TokenType.NullLiteral:
                    Consume();
                    return Literal.NullLiteral;
                case TokenType.NumericLiteral:
                    return new Literal(Consume().DoubleValue());
                case TokenType.Slash:
                case TokenType.SlashEquals:
                    return ParseRegularExpressionLiteral();
                case TokenType.StringLiteral:
                    return new Literal(Consume().StringValue());
                case TokenType.BracketOpen:
                    return ParseArrayLiteral();
                case TokenType.CurlyOpen:
                    return ParseObjectLiteral();
                case TokenType.Function:
                    return ParseFunctionExpression();
                case TokenType.ParenOpen:
                    return ParseParenthesizedExpression();
            }

            Expected("primary expression");
            return null;
        }

        private RegularExpressionLiteral ParseRegularExpressionLiteral()
        {
            ConsumeNextRegex(CurrentToken);
            if (CurrentToken == null)
                throw new ParseFailureException("Regex is not valid.");
            return Consume().RegexValue();
        }

        private ArrayLiteral ParseArrayLiteral()
        {
            Consume(TokenType.BracketOpen);
            var items = new List<IArrayLiteralItem>();
            while (true)
            {
                if (Match(TokenType.BracketClose))
                    break;
                int width = 0;
                while (Match(TokenType.Comma))
                {
                    Consume();
                    width++;
                }
                if (width > 0)
                    items.Add(new Elision(width));
                if (Match(TokenType.Ellipsis))
                {
                    Consume();
                    items.Add(new SpreadElement(ParseAssignmentExpression()));
                    if (Match(TokenType.Comma))
                        Consume();
                    continue;
                }
                items.Add(ParseAssignmentExpression());
                if (Match(TokenType.Comma))
                    Consume();
            }
            Consume(TokenType.BracketClose);
            return new ArrayLiteral(items);
        }

        private ObjectLiteral ParseObjectLiteral()
        {
            Consume(TokenType.CurlyOpen);
            if (Match(TokenType.CurlyClose))
            {
                Consume();
                return new ObjectLiteral(Utils.EmptyList<IPropertyDefinition>());
            }
            var definitions = new List<IPropertyDefinition>();
            while (true)
            {
                if (Match(TokenType.CurlyClose))
                    break;

                if (Match(TokenType.Ellipsis))
                {
                    Consume();
                    definitions.Add(new SpreadElement(ParseAssignmentExpression()));
                    if (Match(TokenType.Comma))
                        Consume();
                    continue;
                }

                IPropertyDefinition propertyDefinition = null;
                Parse(() =>
                {
                    var propertyName = Consume().Value;
                    Consume(TokenType.Colon);
                    propertyDefinition = new PropertyDefinition(propertyName, ParseAssignmentExpression());
                }).Or(() =>
                {
                    var identifier = Consume().Value;
                    var initializer = ParseInitializer();
                    propertyDefinition = new PropertyDefinition(identifier, initializer);
                }).Or(() =>
                {
                    propertyDefinition = new IdentifierReference(new Identifier(Consume().Value));
                }).OrThrow("object literal property definition");
                definitions.Add(propertyDefinition);

                if (Match(TokenType.Comma))
                    Consume();
            }
            Consume(TokenType.CurlyClose);

            return new ObjectLiteral(definitions);
        }

        private FunctionExpression ParseFunctionExpression()
        {
            Consume(TokenType.Function);
            string functionName = null;
            if (MatchBindingIdentifier())
                functionName = ParseBindingIdentifier();
            Consume(TokenType.ParenOpen);
            FormalParameters parameters;
            if (Match(TokenType.ParenClose))
                parameters = new FormalParameters();
            else
                parameters = ParseFormalParameters();
            Consume(TokenType.ParenClose);
            Consume(TokenType.CurlyOpen);
            var statements = new FunctionStatementList(ParseStatementList().statements);
            Consume(TokenType.CurlyClose);

            if (functionName != null)
                return new FunctionExpression(new Identifier(functionName), parameters, statements);
            return new FunctionExpression(parameters, statements);
        }

        private ParenthesizedExpression ParseParenthesizedExpression()
        {
            Consume(TokenType.ParenOpen);
            var expression = ParseExpression();
            Consume(TokenType.ParenClose);
            return new ParenthesizedExpression(expression);
        }
    }
}
