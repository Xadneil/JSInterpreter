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
        private ParseFailureException? exception;
        private readonly Lexer.Lexer lexer;

        public Parser(string source)
        {
            lexer = new Lexer.Lexer(source);
            lexer.Next();
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
            if (CurrentToken.Trivia.Contains('\n', StringComparison.InvariantCulture))
                return;
            // ...token is a closing curly brace
            if (Match(TokenType.CurlyClose))
                return;
            // ...token is eof
            if (Match(TokenType.Eof))
                return;

            // No rule for semicolon insertion applies -> syntax error
            var currentToken = CurrentToken;
            throw new ParseFailureException($"Syntax Error: Unexpected token {currentToken.Type}. Expected Semicolon (line: {currentToken.LineNumber}, column: {currentToken.LineColumn})");
        }

        private Token? Consume(TokenType expectedType)
        {
            if (CurrentToken.Type != expectedType)
            {
                return Expected<Token>(expectedType.ToString());
            }
            return Consume();
        }

        private bool Match(TokenType type) => CurrentToken.Type == type;

        private T? Expected<T>(string what) where T : class
        {
            var currentToken = CurrentToken;
            exception = new ParseFailureException($"Syntax Error: Unexpected token {currentToken.Type}. Expected {what} (line: {currentToken.LineNumber}, column: {currentToken.LineColumn})");
            return null;
        }

        public Script ParseScript()
        {
            var statementList = ParseStatementList();
            if (CurrentToken.Type != TokenType.Eof && exception != null)
                throw exception;
            if (statementList == null && exception != null)
                throw exception;
            if (statementList == null)
                throw new ParseFailureException("Got null Statement List but no exception");
            return new Script(new ScriptStatementList(statementList.statements));
        }

        public FunctionStatementList ParseFunctionBody()
        {
            var statementList = ParseStatementList();
            if (CurrentToken.Type != TokenType.Eof && exception != null)
                throw exception;
            if (statementList == null && exception != null)
                throw exception;
            if (statementList == null)
                throw new ParseFailureException("Got null Statement List but no exception");
            return new FunctionStatementList(statementList.statements);
        }

        private IStatementListItem? ParseStatementListItem()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                var item = ParseDeclaration();
                if (lr.Success = item != null)
                    return item;
            }
            return ParseStatement();
        }

        private Statement? ParseStatement()
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
                    using (var lr = new LexerRewinder(lexer))
                    {
                        Statement? statement = ParseLabelledStatement();
                        if (lr.Success = statement != null)
                            return statement;
                    }
                    IExpression expr;
                    using (var lr = new LexerRewinder(lexer))
                    {
                        var tmp = ParseExpression();
                        lr.Success = tmp != null;
                        if (tmp == null)
                            return null;
                        else lr.Success = true;
                        expr = tmp;
                    }
                    ConsumeOrInsertSemicolon();
                    return new ExpressionStatement(expr);
            }
        }

        private Block? ParseBlock()
        {
            Consume(TokenType.CurlyOpen);
            var statementList = ParseStatementList();
            if (statementList == null) return null;
            if (Consume(TokenType.CurlyClose) == null) return null;

            return new Block(statementList);
        }

        private StatementList? ParseStatementList()
        {
            var statementItems = new List<IStatementListItem>();
            while (!Match(TokenType.CurlyClose) && !Match(TokenType.Eof))
            {
                IStatementListItem? item = ParseStatementListItem();
                if (item == null) return null;
                statementItems.Add(item);
            }
            return new StatementList(statementItems);
        }

        private VariableStatement? ParseVariableStatement()
        {
            Consume(TokenType.Var);
            VariableDeclarationList? variableDeclarationList = ParseVariableDeclarationList();
            if (variableDeclarationList == null) return null;
            if (Match(TokenType.Semicolon))
                Consume();
            return new VariableStatement(variableDeclarationList);
        }

        private VariableDeclarationList? ParseVariableDeclarationList()
        {
            var list = new VariableDeclarationList();

            while (true)
            {
                var variableName = ParseBindingIdentifier();
                if (variableName == null) return null;
                IAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                list.Add(new VariableDeclaration(variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return list;
        }

        private VariableDeclarationList? ParseVariableDeclarationListWithInitialIdentifier(string firstIdentifier)
        {
            var list = new VariableDeclarationList();

            bool first = true;
            while (true)
            {
                string? variableName;
                if (first)
                {
                    variableName = firstIdentifier;
                    first = false;
                }
                else
                {
                    variableName = ParseBindingIdentifier();
                    if (variableName == null) return null;
                }
                IAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                list.Add(new VariableDeclaration(variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return list;
        }

        private string? ParseBindingIdentifier()
        {
            if (Match(TokenType.Identifier))
                return Consume().Value;
            else if (Match(TokenType.Yield))
                return "yield";
            else if (Match(TokenType.Await))
                return "await";
            return Expected<string>("Binding Identifier");
        }

        private bool MatchBindingIdentifier() => Match(TokenType.Identifier) || Match(TokenType.Yield) || Match(TokenType.Await);

        private IAssignmentExpression? ParseInitializer()
        {
            Consume(TokenType.Equals);
            return ParseAssignmentExpression();
        }

        private IfStatement? ParseIfStatement()
        {
            Consume(TokenType.If);
            if (Consume(TokenType.ParenOpen) == null) return null;
            var test = ParseExpression();
            if (test == null) return null;
            if (Consume(TokenType.ParenClose) == null) return null;
            var trueStatement = ParseStatement();
            if (trueStatement == null) return null;
            if (Match(TokenType.Else))
            {
                Consume();
                var falseStatement = ParseStatement();
                if (falseStatement == null) return null;
                return new IfStatement(test, trueStatement, falseStatement);
            }
            return new IfStatement(test, trueStatement);
        }

        private BreakableStatement? ParseBreakableStatement()
        {
            if (Match(TokenType.Switch))
                return ParseSwitchStatement();
            return ParseIterationStatement();
        }

        private IterationStatement? ParseIterationStatement()
        {
            if (Match(TokenType.Do))
            {
                Consume();
                Statement? statement = ParseStatement();
                if (statement == null) return null;
                if (Consume(TokenType.While) == null) return null;
                if (Consume(TokenType.ParenOpen) == null) return null;
                IExpression? test = ParseExpression();
                if (test == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                if (Match(TokenType.Semicolon))
                    Consume();
                return new DoWhileIterationStatement(statement, test);
            }
            if (Match(TokenType.While))
            {
                Consume();
                if (Consume(TokenType.ParenOpen) == null) return null;
                IExpression? test = ParseExpression();
                if (test == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                Statement? statement = ParseStatement();
                if (statement == null) return null;
                return new WhileIterationStatement(test, statement);
            }
            if (!Match(TokenType.For))
                return Expected<IterationStatement>("For");
            Consume();
            //TODO await
            if (Consume(TokenType.ParenOpen) == null) return null;

            var forExpressionIterationStatement = ParseForExpression();
            if (forExpressionIterationStatement != null) return forExpressionIterationStatement;

            if (Match(TokenType.Var))
            {
                Consume();

                var firstIdentifier = ParseBindingIdentifier();
                if (firstIdentifier == null) return null;
                if (Match(TokenType.In))
                {
                    Consume();
                    var expression = ParseExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForInVarIterationStatement(new ForBinding(firstIdentifier), expression, statement);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    IAssignmentExpression? expression = ParseAssignmentExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForOfVarIterationStatement(new ForBinding(firstIdentifier), expression, statement);
                }
                if (Match(TokenType.Equals))
                {
                    //don't consume the =, since ParseInitializer will do it.
                    var variableDeclarationList = ParseVariableDeclarationListWithInitialIdentifier(firstIdentifier);
                    if (variableDeclarationList == null) return null;
                    if (Consume(TokenType.Semicolon) == null) return null;
                    IExpression? test = null;
                    if (!Match(TokenType.Semicolon))
                    {
                        test = ParseExpression();
                        if (test == null) return null;
                    }
                    if (Consume(TokenType.Semicolon) == null) return null;
                    IExpression? update = null;
                    if (!Match(TokenType.ParenClose))
                    {
                        update = ParseExpression();
                        if (update == null) return null;
                    }
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForVarIterationStatement(variableDeclarationList, test, update, statement);
                }
                return Expected<IterationStatement>("=, in, or of");
            }
            if (Match(TokenType.Let) || Match(TokenType.Const))
            {
                var isConst = Match(TokenType.Const);
                Consume();
                var forToken = Consume(TokenType.Identifier);
                if (forToken == null) return null;
                var forBinding = forToken.Value;
                if (Match(TokenType.In))
                {
                    Consume();
                    var expression = ParseExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForInLetConstIterationStatement(new ForDeclaration(isConst, forBinding), expression, statement);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    IAssignmentExpression? expression = ParseAssignmentExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForOfLetConstIterationStatement(new ForDeclaration(isConst, forBinding), expression, statement);
                }
                return Expected<IterationStatement>("in or of");
            }
            ILeftHandSideExpression? lhs = ParseLeftHandSideExpression();
            if (lhs == null) return null;

            if (Match(TokenType.In))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                var statement = ParseStatement();
                if (statement == null) return null;
                return new ForInLHSIterationStatement(lhs, expression, statement);
            }
            if (Match(TokenType.Of))
            {
                Consume();
                IAssignmentExpression? expression = ParseAssignmentExpression();
                if (expression == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                var statement = ParseStatement();
                if (statement == null) return null;
                return new ForOfLHSIterationStatement(lhs, expression, statement);
            }
            return Expected<IterationStatement>("in or of");
        }

        private ForExpressionIterationStatement? ParseForExpression()
        {
            IExpression? start = null;
            if (!Match(TokenType.Semicolon))
            {
                start = ParseExpression();
                if (start == null) return null;
            }
            if (Consume(TokenType.Semicolon) == null) return null;
            IExpression? test = null;
            if (!Match(TokenType.Semicolon))
            {
                test = ParseExpression();
                if (test == null) return null;
            }
            if (Consume(TokenType.Semicolon) == null) return null;
            IExpression? update = null;
            if (!Match(TokenType.ParenClose))
            {
                update = ParseExpression();
                if (update == null) return null;
            }
            if (Consume(TokenType.ParenClose) == null) return null;
            var statement = ParseStatement();
            if (statement == null) return null;
            return new ForExpressionIterationStatement(start, test, update, statement);
        }

        private SwitchStatement? ParseSwitchStatement()
        {
            if (Consume(TokenType.Switch) == null) return null;
            if (Consume(TokenType.ParenOpen) == null) return null;
            var expression = ParseExpression();
            if (expression == null) return null;
            if (Consume(TokenType.ParenClose) == null) return null;
            if (Consume(TokenType.CurlyOpen) == null) return null;

            if (Match(TokenType.CurlyClose))
            {
                Consume();
                return new SwitchStatement(expression, new CaseBlock(Utils.EmptyList<CaseClause>()));
            }

            bool foundDefault = false;
            var firstCases = new List<CaseClause>();
            DefaultClause? defaultClause = null;
            var secondCases = new List<CaseClause>();
            while (true)
            {
                if (Match(TokenType.CurlyClose))
                    break;
                if (Match(TokenType.Default))
                {
                    foundDefault = true;
                    Consume(TokenType.Default);
                    if (Consume(TokenType.Colon) == null) return null;
                    var defaultStatements = ParseStatementList();
                    if (defaultStatements == null) return null;
                    defaultClause = new DefaultClause(defaultStatements);
                    continue;
                }
                if (Consume(TokenType.Case) == null) return null;
                var matchExpression = ParseExpression();
                if (matchExpression == null) return null;
                if (Consume(TokenType.Colon) == null) return null;
                var statements = ParseStatementList();
                if (statements == null) return null;
                var clause = new CaseClause(matchExpression, statements);
                if (foundDefault)
                    secondCases.Add(clause);
                else
                    firstCases.Add(clause);
            }

            if (Consume(TokenType.CurlyClose) == null) return null;
            return new SwitchStatement(expression, new CaseBlock(firstCases, defaultClause, secondCases));
        }

        private ContinueStatement ParseContinueStatement()
        {
            Consume(TokenType.Continue);
            string? label = null;
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
            string? label = null;
            if (!CurrentToken.PassedNewLine && MatchBindingIdentifier())
                label = ParseBindingIdentifier();
            if (Match(TokenType.Semicolon))
                Consume();
            if (label == null)
                return new BreakStatement();
            return new BreakStatement(label);
        }

        private ReturnStatement? ParseReturnStatement()
        {
            Consume(TokenType.Return);
            IExpression? expression = null;
            if (!CurrentToken.PassedNewLine && !Match(TokenType.Semicolon))
            {
                expression = ParseExpression();
                if (expression == null) return null;
            }
            if (Match(TokenType.Semicolon))
                Consume();
            if (expression == null)
                return new ReturnStatement();
            return new ReturnStatement(expression);
        }

        private LabelledStatement? ParseLabelledStatement()
        {
            var label = ParseBindingIdentifier();
            if (label == null) return null;
            if (Consume(TokenType.Colon) == null) return null;
            if (Match(TokenType.Function))
            {
                var function = ParseFunctionDeclaration();
                if (function == null) return null;
                return new LabelledStatement(new Identifier(label), function);
            }
            var statement = ParseStatement();
            if (statement == null) return null;
            return new LabelledStatement(new Identifier(label), statement);
        }

        private ThrowStatement? ParseThrowStatement()
        {
            Consume(TokenType.Throw);
            if (CurrentToken.PassedNewLine)
                return Expected<ThrowStatement>("an expression to throw");
            var expression = ParseExpression();
            if (expression == null) return null;
            if (Match(TokenType.Semicolon))
                Consume(TokenType.Semicolon);
            return new ThrowStatement(expression);
        }

        private TryStatement? ParseTryStatement()
        {
            Consume(TokenType.Try);
            var tryBlock = ParseBlock();
            if (tryBlock == null) return null;

            (string? catchParameter, Block catchBlock)? catchClause = null;
            Block? finallyBlock = null;
            bool hasRequiredContent = false;
            if (Match(TokenType.Catch))
            {
                catchClause = ParseCatch();
                if (catchClause == null) return null;
                hasRequiredContent = true;
            }
            if (Match(TokenType.Finally))
            {
                Consume(TokenType.Finally);
                finallyBlock = ParseBlock();
                if (finallyBlock == null) return null;
                hasRequiredContent = true;
            }
            if (!hasRequiredContent)
                return Expected<TryStatement>("catch or finally clause");

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
                if (catchClause!.Value.catchParameter != null)
                    return TryStatement.TryCatchFinally(tryBlock, new Identifier(catchClause.Value.catchParameter), catchClause.Value.catchBlock, finallyBlock!);
                else
                    return TryStatement.TryCatchFinally(tryBlock, catchClause.Value.catchBlock, finallyBlock!);
            }
        }

        private (string?, Block)? ParseCatch()
        {
            Consume();
            string? catchParameter = null;
            if (Match(TokenType.ParenOpen))
            {
                Consume();
                catchParameter = ParseBindingIdentifier();
                if (catchParameter == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
            }
            var block = ParseBlock();
            if (block == null) return null;
            return (catchParameter, block);
        }

        private Declaration? ParseDeclaration()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                var declaration = ParseHoistableDeclaration();
                if (lr.Success = declaration != null)
                    return declaration;
            }
            using (var lr = new LexerRewinder(lexer))
            {
                //TODO class
                var declaration = ParseLexicalDeclaration();
                if (lr.Success = declaration != null)
                    return declaration;
            }
            return null;
        }

        private HoistableDeclaration? ParseHoistableDeclaration()
        {
            return ParseFunctionDeclaration();
        }

        private FunctionDeclaration? ParseFunctionDeclaration()
        {
            if (Consume(TokenType.Function) == null) return null;
            string? functionName = null;
            if (MatchBindingIdentifier())
                functionName = ParseBindingIdentifier();
            if (Consume(TokenType.ParenOpen) == null) return null;
            FormalParameters parameters;
            if (Match(TokenType.ParenClose))
                parameters = new FormalParameters();
            else
            {
                var tmp = ParseFormalParameters();
                if (tmp == null) return null;
                parameters = tmp;
            }
            if (Consume(TokenType.ParenClose) == null) return null;
            if (Consume(TokenType.CurlyOpen) == null) return null;
            var statementList = ParseStatementList();
            if (statementList == null) return null;
            var statements = new FunctionStatementList(statementList.statements);
            if (Consume(TokenType.CurlyClose) == null) return null;

            if (functionName != null)
                return new FunctionDeclaration(new Identifier(functionName), parameters, statements);
            return new FunctionDeclaration(parameters, statements);
        }

        public FormalParameters? ParseFormalParameters()
        {
            string? restParameter = null;
            var list = new List<FormalParameter>();
            while (true)
            {
                if (Match(TokenType.Ellipsis))
                {
                    Consume();
                    restParameter = ParseBindingIdentifier();
                    if (restParameter == null) return null;
                    continue;
                }
                if (!MatchBindingIdentifier())
                {
                    return Expected<FormalParameters>("a parameter");
                }

                if (restParameter != null)
                {
                    return Expected<FormalParameters>("no more parameters after the rest parameter");
                }

                var name = ParseBindingIdentifier()!;
                if (Match(TokenType.Equals))
                {
                    var initializer = ParseInitializer();
                    if (initializer == null) return null;
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

        private LexicalDeclaration? ParseLexicalDeclaration()
        {
            LexicalDeclarationType lexicalDeclarationType;
            if (Match(TokenType.Let))
                lexicalDeclarationType = LexicalDeclarationType.Let;
            else if (Match(TokenType.Const))
                lexicalDeclarationType = LexicalDeclarationType.Const;
            else
            {
                return Expected<LexicalDeclaration>("let or const");
            }
            Consume();

            var list = new List<LexicalDeclarationItem>();

            while (true)
            {
                var variableName = ParseBindingIdentifier();
                if (variableName == null) return null;
                IAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                else if (lexicalDeclarationType == LexicalDeclarationType.Const)
                {
                    return Expected<LexicalDeclaration>("an initializer for a const declaration");
                }
                list.Add(new LexicalDeclarationItem(lexicalDeclarationType, variableName, initializer));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return new LexicalDeclaration(lexicalDeclarationType, list);
        }

        private IExpression? ParseExpression()
        {
            IAssignmentExpression? expression = ParseAssignmentExpression();
            if (expression == null) return null;
            if (!Match(TokenType.Comma))
                return expression;
            var list = new List<IAssignmentExpression>() { expression };
            do
            {
                Consume(TokenType.Comma);
                var nextExpression = ParseAssignmentExpression();
                if (nextExpression == null) return null;
                list.Add(nextExpression);
            } while (Match(TokenType.Comma));

            return new CommaExpression(list);
        }

        private IAssignmentExpression? ParseAssignmentExpression()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                ILeftHandSideExpression? lhs = ParseLeftHandSideExpression();
                if (lhs != null)
                {
                    if (Match(TokenType.Equals))
                    {
                        Consume();
                        var rhs = ParseAssignmentExpression();
                        if (lr.Success = rhs != null)
                            return new AssignmentExpression(lhs, rhs!);
                    }
                    if (MatchAssignmentOperator())
                    {
                        var op = ParseAssignmentOperator();
                        var rhs = ParseAssignmentExpression();
                        if (lr.Success = rhs != null)
                            return new OperatorAssignmentExpression(lhs, op, rhs!);
                    }
                }
            }
            return ParseConditionalExpression();
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
            return type switch
            {
                TokenType.AsteriskEquals => AssignmentOperator.Multiply,
                TokenType.SlashEquals => AssignmentOperator.Divide,
                TokenType.PercentEquals => AssignmentOperator.Modulus,
                TokenType.PlusEquals => AssignmentOperator.Plus,
                TokenType.MinusEquals => AssignmentOperator.Minus,
                TokenType.ShiftLeftEquals => AssignmentOperator.ShiftLeft,
                TokenType.ShiftRightEquals => AssignmentOperator.ShiftRight,
                TokenType.UnsignedShiftRightEquals => AssignmentOperator.ShiftRightUnsigned,
                TokenType.AmpersandEquals => AssignmentOperator.BitwiseAnd,
                TokenType.CaretEquals => AssignmentOperator.BitwiseXor,
                TokenType.PipeEquals => AssignmentOperator.BitwiseOr,
                TokenType.AsteriskAsteriskEquals => AssignmentOperator.Exponentiate,
                _ => throw new InvalidOperationException("ParseAssignmentOperator should never fail"),
            };
        }

        private IConditionalExpression? ParseConditionalExpression()
        {
            var expression = ParseLogicalOrExpression();
            if (expression == null) return null;
            if (Consume(TokenType.QuestionMark) == null) return expression;
            var trueExpr = ParseAssignmentExpression();
            if (trueExpr == null) return null;
            if (Consume(TokenType.Colon) == null) return null;
            var falseExpr = ParseAssignmentExpression();
            if (falseExpr == null) return null;
            return new ConditionalExpression(expression, trueExpr, falseExpr);
        }

        private ILogicalOrExpression? ParseLogicalOrExpression()
        {
            var lhs = ParseLogicalAndExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseLogicalOrTail();
            if (!success) return null;

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

        private (bool success, TailContainer<ILogicalAndExpression>? tail) ParseLogicalOrTail()
        {
            if (!Match(TokenType.DoublePipe))
                return (true, null);
            Consume();
            var rhs = ParseLogicalAndExpression();
            if (rhs == null) return (false, null);
            var tail = ParseLogicalOrTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<ILogicalAndExpression>(rhs, tail.tail));
        }

        private ILogicalAndExpression? ParseLogicalAndExpression()
        {
            var lhs = ParseBitwiseOrExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseLogicalAndTail();
            if (!success) return null;

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

        private (bool success, TailContainer<IBitwiseOrExpression>? tail) ParseLogicalAndTail()
        {
            if (!Match(TokenType.DoubleAmpersand))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseOrExpression();
            if (rhs == null) return (false, null);
            var tail = ParseLogicalAndTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<IBitwiseOrExpression>(rhs, tail.tail));
        }

        private IBitwiseOrExpression? ParseBitwiseOrExpression()
        {
            var lhs = ParseBitwiseXorExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseBitwiseOrTail();
            if (!success) return null;

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

        private (bool success, TailContainer<IBitwiseXorExpression>? tail) ParseBitwiseOrTail()
        {
            if (!Match(TokenType.Pipe))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseXorExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseOrTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<IBitwiseXorExpression>(rhs, tail.tail));
        }

        private IBitwiseXorExpression? ParseBitwiseXorExpression()
        {
            var lhs = ParseBitwiseAndExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseBitwiseXorTail();
            if (!success) return null;

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

        private (bool success, TailContainer<IBitwiseAndExpression>? tail) ParseBitwiseXorTail()
        {
            if (!Match(TokenType.Caret))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseAndExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseXorTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<IBitwiseAndExpression>(rhs, tail.tail));
        }

        private IBitwiseAndExpression? ParseBitwiseAndExpression()
        {
            var lhs = ParseEqualityExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseBitwiseAndTail();
            if (!success) return null;

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

        private (bool success, TailContainer<IEqualityExpression>? tail) ParseBitwiseAndTail()
        {
            if (!Match(TokenType.Ampersand))
                return (true, null);
            Consume();
            var rhs = ParseEqualityExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseAndTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<IEqualityExpression>(rhs, tail.tail));
        }

        private IEqualityExpression? ParseEqualityExpression()
        {
            var lhs = ParseRelationalExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseEqualityTail();
            if (!success) return null;

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

        private (bool success, TailOperatorContainer<IRelationalExpression, EqualityOperator>? tail) ParseEqualityTail()
        {
            if (!MatchEqualityOperator())
                return (true, null);
            var op = ParseEqualityOperator();
            var rhs = ParseRelationalExpression();
            if (rhs == null) return (false, null);
            var tail = ParseEqualityTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<IRelationalExpression, EqualityOperator>(rhs, op, tail.tail));
        }

        private EqualityOperator ParseEqualityOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.EqualsEquals => EqualityOperator.Equals,
                TokenType.ExclamationMarkEquals => EqualityOperator.NotEquals,
                TokenType.EqualsEqualsEquals => EqualityOperator.StrictEquals,
                TokenType.ExclamationMarkEqualsEquals => EqualityOperator.StrictNotEquals,
                _ => throw new InvalidOperationException("ParseEqualityOperator should not fail"),
            };
        }

        private bool MatchEqualityOperator()
        {
            return Match(TokenType.EqualsEquals) ||
                Match(TokenType.ExclamationMarkEquals) ||
                Match(TokenType.EqualsEqualsEquals) ||
                Match(TokenType.ExclamationMarkEqualsEquals);
        }

        private IRelationalExpression? ParseRelationalExpression()
        {
            var lhs = ParseShiftExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseRelationalTail();
            if (!success) return null;

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

        private (bool success, TailOperatorContainer<IShiftExpression, RelationalOperator>? tail) ParseRelationalTail()
        {
            if (!MatchRelationalOperator())
                return (true, null);
            var op = ParseRelationalOperator();
            var rhs = ParseShiftExpression();
            if (rhs == null) return (false, null);
            var tail = ParseRelationalTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<IShiftExpression, RelationalOperator>(rhs, op, tail.tail));
        }

        private RelationalOperator ParseRelationalOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.LessThan => RelationalOperator.LessThan,
                TokenType.GreaterThan => RelationalOperator.GreaterThan,
                TokenType.LessThanEquals => RelationalOperator.LessThanOrEqual,
                TokenType.GreaterThanEquals => RelationalOperator.GreaterThanOrEqual,
                TokenType.Instanceof => RelationalOperator.Instanceof,
                TokenType.In => RelationalOperator.In,
                _ => throw new InvalidOperationException("ParseRelationalOperator should not fail"),
            };
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

        private IShiftExpression? ParseShiftExpression()
        {
            var lhs = ParseAdditiveExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseShiftTail();
            if (!success) return null;

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

        private (bool success, TailOperatorContainer<IAdditiveExpression, ShiftOperator>? tail) ParseShiftTail()
        {
            if (!MatchShiftOperator())
                return (true, null);
            var op = ParseShiftOperator();
            var rhs = ParseAdditiveExpression();
            if (rhs == null) return (false, null);
            var tail = ParseShiftTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<IAdditiveExpression, ShiftOperator>(rhs, op, tail.tail));
        }

        private ShiftOperator ParseShiftOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.ShiftLeft => ShiftOperator.ShiftLeft,
                TokenType.ShiftRight => ShiftOperator.ShiftRight,
                TokenType.UnsignedShiftRight => ShiftOperator.ShiftRightUnsigned,
                _ => throw new InvalidOperationException("ParseShiftOperator should not fail"),
            };
        }

        private bool MatchShiftOperator()
        {
            return Match(TokenType.ShiftLeft) ||
                Match(TokenType.ShiftRight) ||
                Match(TokenType.UnsignedShiftRight);
        }

        private IAdditiveExpression? ParseAdditiveExpression()
        {
            var lhs = ParseMultiplicativeExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseAdditiveTail();
            if (!success) return null;

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

        private (bool success, TailOperatorContainer<IMultiplicativeExpression, AdditiveOperator>? tail) ParseAdditiveTail()
        {
            if (!MatchAdditiveOperator())
                return (true, null);
            var op = ParseAdditiveOperator();
            var rhs = ParseMultiplicativeExpression();
            if (rhs == null) return (false, null);
            var tail = ParseAdditiveTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<IMultiplicativeExpression, AdditiveOperator>(rhs, op, tail.tail));
        }

        private AdditiveOperator ParseAdditiveOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.Plus => AdditiveOperator.Add,
                TokenType.Minus => AdditiveOperator.Subtract,
                _ => throw new InvalidOperationException("ParseAdditiveOperator should not fail"),
            };
        }

        private bool MatchAdditiveOperator() => Match(TokenType.Plus) || Match(TokenType.Minus);

        private IMultiplicativeExpression? ParseMultiplicativeExpression()
        {
            var lhs = ParseExponentiationExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseMultiplicativeTail();
            if (!success) return null;

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

        private (bool success, TailOperatorContainer<IExponentiationExpression, MultiplicativeOperator>? tail) ParseMultiplicativeTail()
        {
            if (!MatchMultiplicativeOperator())
                return (true, null);
            var op = ParseMultiplicativeOperator();
            var rhs = ParseExponentiationExpression();
            if (rhs == null) return (false, null);
            var tail = ParseMultiplicativeTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<IExponentiationExpression, MultiplicativeOperator>(rhs, op, tail.tail));
        }

        private MultiplicativeOperator ParseMultiplicativeOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.Asterisk => MultiplicativeOperator.Multiply,
                TokenType.Slash => MultiplicativeOperator.Divide,
                TokenType.Percent => MultiplicativeOperator.Modulus,
                _ => throw new InvalidOperationException("ParseMultiplicativeOperator should not fail"),
            };
        }

        private bool MatchMultiplicativeOperator()
        {
            return Match(TokenType.Asterisk) ||
                Match(TokenType.Slash) ||
                Match(TokenType.Percent);
        }

        private IExponentiationExpression? ParseExponentiationExpression()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                IUpdateExpression? lhs = ParseUpdateExpression();
                if (lhs != null)
                {
                    if (Consume(TokenType.DoubleAsterisk) != null)
                    {
                        var rhs = ParseExponentiationExpression();
                        if (lr.Success = rhs != null)
                            return new ExponentiationExpression(lhs, rhs!);
                    }
                }
            }
            return ParseUnaryExpression();
        }

        private IUnaryExpression? ParseUnaryExpression()
        {
            if (!MatchUnaryOperator())
            {
                return ParseUpdateExpression();
            }
            var op = ParseUnaryOperator();
            var expression = ParseUnaryExpression();
            if (expression == null) return null;
            return new OperatorUnaryExpression(op, expression);
        }

        private UnaryOperator ParseUnaryOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.Delete => UnaryOperator.Delete,
                TokenType.Void => UnaryOperator.Void,
                TokenType.Typeof => UnaryOperator.Typeof,
                TokenType.Plus => UnaryOperator.Plus,
                TokenType.Minus => UnaryOperator.Negate,
                TokenType.Tilde => UnaryOperator.BitwiseNot,
                TokenType.ExclamationMark => UnaryOperator.LogicalNot,
                _ => throw new InvalidOperationException("ParseUnaryOperator should not fail"),
            };
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

        private IUpdateExpression? ParseUpdateExpression()
        {
            if (MatchUpdateOperator())
            {
                var op = ParseUpdateOperator();
                var expression = ParseUnaryExpression();
                if (expression == null) return null;
                return new PrefixUpdateExpression(expression, op);
            }

            var lhs = ParseLeftHandSideExpression();
            if (lhs == null) return null;
            if (MatchUpdateOperator() && !CurrentToken.PassedNewLine)
            {
                var op = ParseUpdateOperator();
                return new PostfixUpdateExpression(lhs, op);
            }
            return lhs;
        }

        private UpdateOperator ParseUpdateOperator()
        {
            var type = Consume().Type;
            return type switch
            {
                TokenType.PlusPlus => UpdateOperator.Increment,
                TokenType.MinusMinus => UpdateOperator.Decrement,
                _ => throw new InvalidOperationException("ParseUpdateOperator should not fail"),
            };
        }

        private bool MatchUpdateOperator() => Match(TokenType.PlusPlus) || Match(TokenType.MinusMinus);

        private ILeftHandSideExpression? ParseLeftHandSideExpression()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                var callExpression = ParseCallExpression();
                if (lr.Success = callExpression != null)
                    return callExpression;
            }
            return ParseNewExpression();
        }

        private INewExpression? ParseNewExpression()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                if (Consume(TokenType.New) != null)
                {
                    var innerExpression = ParseNewExpression();
                    if (innerExpression != null)
                    {
                        if (lr.Success = CurrentToken.Type != TokenType.ParenOpen)
                            return new NewExpression(innerExpression);
                    }
                }
            }
            return ParseMemberExpression();
        }

        private ICallExpression? ParseCallExpression()
        {
            ICallExpression? lhs = null;
            using (var lr = new LexerRewinder(lexer))
            {
                lhs = ParseCoveredCallExpression();
                lr.Success = lhs != null;
            }
            if (lhs == null)
                lhs = ParseSuperCallExpression();
            if (lhs == null) return null;

            var (success, tail) = ParseCallExpressionTail();
            if (!success) return null;

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
                    return Expected<ICallExpression>("call expression tail component");
                }

                tail = tail.Tail;
            } while (tail != null);

            return lhs;
        }

        private (bool success, CallExpressionTail? tail) ParseCallExpressionTail()
        {
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return (false, null);
                if (Consume(TokenType.BracketClose) != null) return (false, null);
                var tail = ParseCallExpressionTail();
                if (!tail.success) return (false, null);
                return (true, new CallExpressionTail(expression, tail.tail));
            }
            else if (Match(TokenType.Period))
            {
                Consume();
                var identifierName = Consume().Value;
                var tail = ParseCallExpressionTail();
                if (!tail.success) return (false, null);
                return (true, new CallExpressionTail(identifierName, tail.tail));
            }
            Arguments? arguments = null;
            using (var lr = new LexerRewinder(lexer))
            {
                arguments = ParseArguments();
                lr.Success = arguments != null;
            }
            if (arguments != null)
            {
                var tail = ParseCallExpressionTail();
                if (!tail.success) return (false, null);
                return (true, new CallExpressionTail(arguments, tail.tail));
            }
            return (true, null);
        }

        private SuperCall? ParseSuperCallExpression()
        {
            if (Consume(TokenType.Super) == null) return null;
            var arguments = ParseArguments();
            if (arguments == null) return null;
            return new SuperCall(arguments);
        }

        private MemberCallExpression? ParseCoveredCallExpression()
        {
            var memberExpression = ParseMemberExpression();
            if (memberExpression == null) return null;
            var arguments = ParseArguments();
            if (arguments == null) return null;
            return new MemberCallExpression(memberExpression, arguments);
        }

        private Arguments? ParseArguments()
        {
            if (Consume(TokenType.ParenOpen) == null) return null;
            if (Match(TokenType.ParenClose))
            {
                Consume();
                return new Arguments(Utils.EmptyList<IArgumentItem>());
            }
            var arguments = new List<IArgumentItem>();
            while (true)
            {
                var item = ParseArgumentItem();
                if (item == null) return null;
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

        private IArgumentItem? ParseArgumentItem()
        {
            if (Match(TokenType.Ellipsis))
            {
                Consume();
                var assignmentExpression = ParseAssignmentExpression();
                if (assignmentExpression == null) return null;
                return new SpreadElement(assignmentExpression);
            }
            return ParseAssignmentExpression();
        }

        private IMemberExpression? ParseMemberExpression()
        {
            if (Match(TokenType.New))
            {
                Consume();
                var left = ParseMemberExpression();
                if (left == null) return null;
                var arguments = ParseArguments();
                if (arguments == null) return null;
                return new NewMemberExpression(left, arguments);
            }
            if (Match(TokenType.Super))
            {
                return ParseSuperProperty();
            }
            IMemberExpression? lhs = ParsePrimaryExpression();
            if (lhs == null) return null;
            var (success, tail) = ParseMemberExpressionTail();
            if (!success) return null;

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
                    return Expected<IMemberExpression>("member expression tail component");
                }

                tail = tail.Tail;
            } while (tail != null);

            return lhs;
        }

        private (bool success, MemberExpressionTail? tail) ParseMemberExpressionTail()
        {
            if (!Match(TokenType.BracketOpen) && !Match(TokenType.Period))
                return (true, null);
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return (false, null);
                Consume(TokenType.BracketClose);
                var tail = ParseMemberExpressionTail();
                if (!tail.success) return (false, null);
                return (true, new MemberExpressionTail(expression, tail.tail));
            }
            if (Match(TokenType.Period))
            {
                Consume();
                var name = Consume().Value;
                var tail = ParseMemberExpressionTail();

                if (!tail.success) return (false, null);
                return (true, new MemberExpressionTail(name, tail.tail));
            }
            Expected<object>("member expression tail");
            return (false, null);
        }

        private IMemberExpression? ParseSuperProperty()
        {
            Consume(TokenType.Super);
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return null;
                if (Consume(TokenType.BracketClose) == null) return null;
                return new SuperIndexMemberExpression(expression);
            }
            if (Match(TokenType.Period))
            {
                Consume();
                return new SuperDotMemberExpression(Consume().Value);
            }
            return Expected<IMemberExpression>("super property");
        }

        private IPrimaryExpression? ParsePrimaryExpression()
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

            return Expected<IPrimaryExpression>("primary expression");
        }

        private RegularExpressionLiteral? ParseRegularExpressionLiteral()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                ConsumeNextRegex(CurrentToken);
                if (CurrentToken == null)
                {
                    exception = new ParseFailureException("Regex is not valid.");
                    return null;
                }
                var regex = Consume().RegexValue();
                lr.Success = true;
                return regex;
            }
        }

        private ArrayLiteral? ParseArrayLiteral()
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
                if (Match(TokenType.BracketClose))
                    break;
                if (Match(TokenType.Ellipsis))
                {
                    Consume();
                    var assignmentExpression = ParseAssignmentExpression();
                    if (assignmentExpression == null) return null;
                    items.Add(new SpreadElement(assignmentExpression));
                    if (Match(TokenType.Comma))
                        Consume();
                    continue;
                }
                var assignmentExpression2 = ParseAssignmentExpression();
                if (assignmentExpression2 == null) return null;
                items.Add(assignmentExpression2);
                if (Match(TokenType.Comma))
                    Consume();
            }
            Consume(TokenType.BracketClose);
            return new ArrayLiteral(items);
        }

        private ObjectLiteral? ParseObjectLiteral()
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
                    var assignmentExpression = ParseAssignmentExpression();
                    if (assignmentExpression == null) return null;
                    definitions.Add(new SpreadElement(assignmentExpression));
                    if (Match(TokenType.Comma))
                        Consume();
                    continue;
                }

                IPropertyDefinition? propertyDefinition = null;

                using (var lr = new LexerRewinder(lexer))
                {
                    propertyDefinition = ParseMethodDefinition();
                    lr.Success = propertyDefinition != null;
                }
                if (propertyDefinition == null)
                    using (var lr = new LexerRewinder(lexer))
                    {
                        var propertyName = Consume();
                        string identifier;
                        if (propertyName.Type == TokenType.NumericLiteral)
                            identifier = propertyName.DoubleValue().ToString(System.Globalization.CultureInfo.InvariantCulture);
                        else if (propertyName.Type == TokenType.StringLiteral)
                            identifier = propertyName.StringValue();
                        else
                            identifier = propertyName.Value;
                        Consume(TokenType.Colon);
                        var assignmentExpression = ParseAssignmentExpression();
                        if (lr.Success = assignmentExpression != null)
                            propertyDefinition = new PropertyDefinition(identifier, assignmentExpression!);
                    }
                if (propertyDefinition == null)
                    using (var lr = new LexerRewinder(lexer))
                    {
                        var propertyName = Consume();
                        string identifier;
                        if (propertyName.Type == TokenType.NumericLiteral)
                            identifier = propertyName.DoubleValue().ToString(System.Globalization.CultureInfo.InvariantCulture);
                        else if (propertyName.Type == TokenType.StringLiteral)
                            identifier = propertyName.StringValue();
                        else
                            identifier = propertyName.Value;
                        var initializer = ParseInitializer();
                        if (lr.Success = initializer != null)
                            propertyDefinition = new PropertyDefinition(identifier, initializer!);
                    }
                if (propertyDefinition == null)
                    using (var lr = new LexerRewinder(lexer))
                    {
                        var identifier = Consume(TokenType.Identifier);
                        if (lr.Success = identifier != null)
                            propertyDefinition = new IdentifierReference(new Identifier(identifier!.Value));
                    }
                if (propertyDefinition == null)
                    return Expected<ObjectLiteral>("object literal property definition");

                definitions.Add(propertyDefinition);

                if (Match(TokenType.Comma))
                    Consume();
            }
            Consume(TokenType.CurlyClose);

            return new ObjectLiteral(definitions);
        }

        private IMethodDefinition? ParseMethodDefinition()
        {
            if (CurrentToken.Type != TokenType.Identifier)
                return Expected<IMethodDefinition>("method name, get, or set");
            var identifier = Consume().Value;

            if (identifier == "get")
            {
                using (var lr = new LexerRewinder(lexer))
                {
                    var propNameId = Consume();
                    var propertyName = propNameId.Value;
                    if (Consume(TokenType.ParenOpen) != null && Consume(TokenType.ParenClose) != null && Consume(TokenType.CurlyOpen) != null)
                    {
                        var body = ParseStatementList();
                        if (body != null && Consume(TokenType.CurlyClose) != null)
                        {
                            lr.Success = true;
                            return new Getter(propertyName, new FunctionStatementList(body.statements));
                        }
                    }
                }
            }
            if (identifier == "set")
            {
                using (var lr = new LexerRewinder(lexer))
                {
                    var propNameId = Consume();
                    var propertyName = propNameId.Value;
                    if (Consume(TokenType.ParenOpen) != null)
                    {
                        var name = ParseBindingIdentifier();
                        if (name != null)
                        {
                            FormalParameter? formalParameter;
                            if (Match(TokenType.Equals))
                            {
                                var initializer = ParseInitializer();
                                if (initializer == null)
                                {
                                    formalParameter = null;
                                }
                                else
                                    formalParameter = new FormalParameter(new Identifier(name), initializer);
                            }
                            else
                                formalParameter = new FormalParameter(new Identifier(name));
                            if (formalParameter != null && Consume(TokenType.ParenClose) != null && Consume(TokenType.CurlyOpen) != null)
                            {
                                var body = ParseStatementList();
                                if (body != null && Consume(TokenType.CurlyClose) != null)
                                {
                                    lr.Success = true;
                                    return new Setter(propertyName, formalParameter, new FunctionStatementList(body.statements));
                                }
                            }
                        }
                    }
                }
            }
            {
                if (Consume(TokenType.ParenOpen) == null) return null;
                var parameters = ParseFormalParameters();
                if (parameters == null || Consume(TokenType.ParenClose) == null || Consume(TokenType.CurlyOpen) == null) return null;
                var body = ParseStatementList();
                if (body == null) return null;
                if (Consume(TokenType.CurlyClose) == null) return null;
                return new MethodDefinition(identifier, parameters, new FunctionStatementList(body.statements));
            }
        }

        private FunctionExpression? ParseFunctionExpression()
        {
            Consume(TokenType.Function);
            string? functionName = null;
            if (MatchBindingIdentifier())
                functionName = ParseBindingIdentifier();
            if (Consume(TokenType.ParenOpen) == null) return null;
            FormalParameters parameters;
            if (Match(TokenType.ParenClose))
                parameters = new FormalParameters();
            else
            {
                var tmp = ParseFormalParameters();
                if (tmp == null) return null;
                parameters = tmp;
            }
            if (Consume(TokenType.ParenClose) == null || Consume(TokenType.CurlyOpen) == null) return null;
            var statementList = ParseStatementList();
            if (statementList == null) return null;
            var statements = new FunctionStatementList(statementList.statements);
            if (Consume(TokenType.CurlyClose) == null) return null;

            if (functionName != null)
                return new FunctionExpression(new Identifier(functionName), parameters, statements);
            return new FunctionExpression(parameters, statements);
        }

        private ParenthesizedExpression? ParseParenthesizedExpression()
        {
            Consume(TokenType.ParenOpen);
            var expression = ParseExpression();
            if (expression == null) return null;
            if (Consume(TokenType.ParenClose) == null) return null;
            return new ParenthesizedExpression(expression);
        }
    }
}
