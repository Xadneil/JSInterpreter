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
        public bool Strict = false;

        public Parser(string source)
        {
            lexer = new Lexer.Lexer(source);
            lexer.Next();
        }

        private Token Consume()
        {
            var oldToken = lexer.CurrentToken;
            if (Strict && oldToken.Type == TokenType.Identifier &&
                    (oldToken.Value == "implements" ||
                    oldToken.Value == "interface" ||
                    oldToken.Value == "package" ||
                    oldToken.Value == "protected" ||
                    oldToken.Value == "private" ||
                    oldToken.Value == "public"))
            {
                throw new ParseFailureException($"invalid use of future reserved word {oldToken.Value} in strict code");
            }
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
            var oldToken = lexer.CurrentToken;
            if (expectedType == TokenType.Identifier && Strict && oldToken.Type == TokenType.Identifier &&
                (oldToken.Value == "static" || oldToken.Value == "yield"))
            {
                throw new ParseFailureException($"{oldToken.Value} is not allowed as an identifier in strict code");
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
            var statementList = ParseStatementList(setStrict: true);
            if (CurrentToken.Type != TokenType.Eof && exception != null)
                throw exception;
            if (statementList == null && exception != null)
                throw exception;
            if (statementList == null)
                throw new ParseFailureException("Got null Statement List but no exception");
            return new Script(new ScriptStatementList(statementList));
        }

        public FunctionStatementList ParseFunctionBody()
        {
            var statementList = ParseStatementList(setStrict: !Strict);
            if (CurrentToken.Type != TokenType.Eof && exception != null)
                throw exception;
            if (statementList == null && exception != null)
                throw exception;
            if (statementList == null)
                throw new ParseFailureException("Got null Statement List but no exception");
            return new FunctionStatementList(statementList);
        }

        private IStatementListItem? ParseStatementListItem()
        {
            if (MatchDeclaration())
            {
                using (var lr = new LexerRewinder(lexer))
                {
                    var item = ParseDeclaration();
                    if (lr.Success = item != null)
                        return item;
                }
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
                    AbstractExpression expr;
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
                    return new ExpressionStatement(expr, Strict);
            }
        }

        private Block? ParseBlock()
        {
            Consume(TokenType.CurlyOpen);
            var statementList = ParseStatementList();
            if (statementList == null) return null;
            if (Consume(TokenType.CurlyClose) == null) return null;

            return new Block(statementList, Strict);
        }

        private StatementList? ParseStatementList(HashSet<TokenType>? stopTypes = null, bool setStrict = false)
        {
            var statementItems = new List<IStatementListItem>();
            bool inDirectivePrologues = setStrict && Strict == false;
            bool unsetStrict = false;

            if (stopTypes == null)
                stopTypes = StatementListStopTypes.CurlyClose;

            while (!stopTypes.Contains(CurrentToken.Type) && !Match(TokenType.Eof))
            {
                IStatementListItem? item;
                using (var lr = new LexerRewinder(lexer))
                {
                    item = ParseStatementListItem();
                    if (item == null)
                    {
                        return null;
                    }
                    lr.Success = true;
                }
                if (inDirectivePrologues)
                {
                    if (item is ExpressionStatement statement && statement.expression is Literal l && l.literalType == LiteralType.String)
                    {
                        if (l.@string == "use strict")
                        {
                            Strict = true;
                            unsetStrict = true;
                        }
                    }
                    else
                    {
                        inDirectivePrologues = false;
                    }
                }
                statementItems.Add(item);
            }
            var statementList = new StatementList(statementItems, Strict);
            if (unsetStrict)
                Strict = false;
            return statementList;
        }

        private VariableStatement? ParseVariableStatement()
        {
            Consume(TokenType.Var);
            VariableDeclarationList? variableDeclarationList = ParseVariableDeclarationList();
            if (variableDeclarationList == null) return null;
            if (Match(TokenType.Semicolon))
                Consume();
            return new VariableStatement(variableDeclarationList, Strict);
        }

        private VariableDeclarationList? ParseVariableDeclarationList()
        {
            var list = new VariableDeclarationList();

            while (true)
            {
                var variableName = ParseBindingIdentifier();
                if (variableName == null) return null;
                AbstractAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                list.Add(new VariableDeclaration(variableName, initializer, Strict));

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
                AbstractAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                list.Add(new VariableDeclaration(variableName, initializer, Strict));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return list;
        }

        private string? ParseBindingIdentifier()
        {
            if (Match(TokenType.Identifier))
                return Consume(TokenType.Identifier)!.Value;
            else if (Match(TokenType.Yield))
                return "yield";
            else if (Match(TokenType.Await))
                return "await";
            return Expected<string>("Binding Identifier");
        }

        private bool MatchBindingIdentifier() => Match(TokenType.Identifier) || Match(TokenType.Yield) || Match(TokenType.Await);

        private AbstractAssignmentExpression? ParseInitializer()
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
                return new IfStatement(test, trueStatement, falseStatement, Strict);
            }
            return new IfStatement(test, trueStatement, Strict);
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
                AbstractExpression? test = ParseExpression();
                if (test == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                if (Match(TokenType.Semicolon))
                    Consume();
                return new DoWhileIterationStatement(statement, test, Strict);
            }
            if (Match(TokenType.While))
            {
                Consume();
                if (Consume(TokenType.ParenOpen) == null) return null;
                AbstractExpression? test = ParseExpression();
                if (test == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                Statement? statement = ParseStatement();
                if (statement == null) return null;
                return new WhileIterationStatement(test, statement, Strict);
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
                    return new ForInVarIterationStatement(new ForBinding(firstIdentifier, Strict), expression, statement, Strict);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    AbstractAssignmentExpression? expression = ParseAssignmentExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForOfVarIterationStatement(new ForBinding(firstIdentifier, Strict), expression, statement, Strict);
                }
                if (Match(TokenType.Equals) || Match(TokenType.Semicolon))
                {
                    VariableDeclarationList? variableDeclarationList;
                    if (Match(TokenType.Equals))
                    {
                        //don't consume the =, since ParseInitializer will do it.
                        variableDeclarationList = ParseVariableDeclarationListWithInitialIdentifier(firstIdentifier);
                        if (variableDeclarationList == null) return null;
                    }
                    else
                    {
                        variableDeclarationList = new VariableDeclarationList() { new VariableDeclaration(firstIdentifier, null, Strict) };
                    }
                    if (Consume(TokenType.Semicolon) == null) return null;
                    AbstractExpression? test = null;
                    if (!Match(TokenType.Semicolon))
                    {
                        test = ParseExpression();
                        if (test == null) return null;
                    }
                    if (Consume(TokenType.Semicolon) == null) return null;
                    AbstractExpression? update = null;
                    if (!Match(TokenType.ParenClose))
                    {
                        update = ParseExpression();
                        if (update == null) return null;
                    }
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForVarIterationStatement(variableDeclarationList, test, update, statement, Strict);
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
                    return new ForInLetConstIterationStatement(new ForDeclaration(isConst, forBinding, Strict), expression, statement, Strict);
                }
                if (Match(TokenType.Of))
                {
                    Consume();
                    AbstractAssignmentExpression? expression = ParseAssignmentExpression();
                    if (expression == null) return null;
                    if (Consume(TokenType.ParenClose) == null) return null;
                    var statement = ParseStatement();
                    if (statement == null) return null;
                    return new ForOfLetConstIterationStatement(new ForDeclaration(isConst, forBinding, Strict), expression, statement, Strict);
                }
                return Expected<IterationStatement>("in or of");
            }
            AbstractLeftHandSideExpression? lhs = ParseLeftHandSideExpression();
            if (lhs == null) return null;

            if (Match(TokenType.In))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                var statement = ParseStatement();
                if (statement == null) return null;
                return new ForInLHSIterationStatement(lhs, expression, statement, Strict);
            }
            if (Match(TokenType.Of))
            {
                Consume();
                AbstractAssignmentExpression? expression = ParseAssignmentExpression();
                if (expression == null) return null;
                if (Consume(TokenType.ParenClose) == null) return null;
                var statement = ParseStatement();
                if (statement == null) return null;
                return new ForOfLHSIterationStatement(lhs, expression, statement, Strict);
            }
            return Expected<IterationStatement>("in or of");
        }

        private ForExpressionIterationStatement? ParseForExpression()
        {
            using var lr = new LexerRewinder(lexer);
            AbstractExpression? start = null;
            if (!Match(TokenType.Semicolon))
            {
                start = ParseExpression();
                if (start == null) return null;
            }
            if (Consume(TokenType.Semicolon) == null) return null;
            AbstractExpression? test = null;
            if (!Match(TokenType.Semicolon))
            {
                test = ParseExpression();
                if (test == null) return null;
            }
            if (Consume(TokenType.Semicolon) == null) return null;
            AbstractExpression? update = null;
            if (!Match(TokenType.ParenClose))
            {
                update = ParseExpression();
                if (update == null) return null;
            }
            if (Consume(TokenType.ParenClose) == null) return null;
            var statement = ParseStatement();
            if (statement == null) return null;
            lr.Success = true;
            return new ForExpressionIterationStatement(start, test, update, statement, Strict);
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
                return new SwitchStatement(expression, new CaseBlock(Utils.EmptyList<CaseClause>()), Strict);
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
                    var defaultStatements = ParseStatementList(StatementListStopTypes.CaseDefaultCurlyClose);
                    if (defaultStatements == null) return null;
                    defaultClause = new DefaultClause(defaultStatements);
                    continue;
                }
                if (Consume(TokenType.Case) == null) return null;
                var matchExpression = ParseExpression();
                if (matchExpression == null) return null;
                if (Consume(TokenType.Colon) == null) return null;
                var statements = ParseStatementList(StatementListStopTypes.CaseDefaultCurlyClose);
                if (statements == null) return null;
                var clause = new CaseClause(matchExpression, statements);
                if (foundDefault)
                    secondCases.Add(clause);
                else
                    firstCases.Add(clause);
            }

            if (Consume(TokenType.CurlyClose) == null) return null;
            return new SwitchStatement(expression, new CaseBlock(firstCases, defaultClause, secondCases), Strict);
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
                return new ContinueStatement(Strict);
            return new ContinueStatement(label, Strict);
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
                return new BreakStatement(Strict);
            return new BreakStatement(label, Strict);
        }

        private ReturnStatement? ParseReturnStatement()
        {
            Consume(TokenType.Return);
            AbstractExpression? expression = null;
            if (!CurrentToken.PassedNewLine && !Match(TokenType.Semicolon))
            {
                expression = ParseExpression();
                if (expression == null) return null;
            }
            if (Match(TokenType.Semicolon))
                Consume();
            if (expression == null)
                return new ReturnStatement(Strict);
            return new ReturnStatement(expression, Strict);
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
                return new LabelledStatement(new Identifier(label, Strict), function, Strict);
            }
            var statement = ParseStatement();
            if (statement == null) return null;
            return new LabelledStatement(new Identifier(label, Strict), statement, Strict);
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
            return new ThrowStatement(expression, Strict);
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
                    return TryStatement.TryCatch(tryBlock, new Identifier(catchClause.Value.catchParameter, Strict), catchClause.Value.catchBlock, Strict);
                else
                    return TryStatement.TryCatch(tryBlock, catchClause.Value.catchBlock, Strict);
            }
            else if (!catchClause.HasValue && finallyBlock != null)
                return TryStatement.TryFinally(tryBlock, finallyBlock, Strict);
            else
            {
                if (catchClause!.Value.catchParameter != null)
                    return TryStatement.TryCatchFinally(tryBlock, new Identifier(catchClause.Value.catchParameter, Strict), catchClause.Value.catchBlock, finallyBlock!, Strict);
                else
                    return TryStatement.TryCatchFinally(tryBlock, catchClause.Value.catchBlock, finallyBlock!, Strict);
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

        private bool MatchDeclaration()
        {
            return Match(TokenType.Function) || Match(TokenType.Let) || Match(TokenType.Const);
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
            var statementList = ParseStatementList(setStrict: !Strict);
            if (statementList == null) return null;
            var statements = new FunctionStatementList(statementList);
            if (Consume(TokenType.CurlyClose) == null) return null;

            if (functionName != null)
                return new FunctionDeclaration(new Identifier(functionName, Strict), parameters, statements, Strict);
            return new FunctionDeclaration(parameters, statements, Strict);
        }

        public FormalParameters? ParseFormalParameters()
        {
            string? restParameter = null;
            var list = new List<FormalParameter>();
            while (CurrentToken.Type != TokenType.Eof)
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
                    list.Add(new FormalParameter(new Identifier(name, Strict), initializer));
                }
                else
                    list.Add(new FormalParameter(new Identifier(name, Strict)));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }
            if (restParameter != null)
            {
                return new FormalParameters(list, new Identifier(restParameter, Strict));
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
                AbstractAssignmentExpression? initializer = null;
                if (Match(TokenType.Equals))
                {
                    initializer = ParseInitializer();
                    if (initializer == null) return null;
                }
                else if (lexicalDeclarationType == LexicalDeclarationType.Const)
                {
                    return Expected<LexicalDeclaration>("an initializer for a const declaration");
                }
                list.Add(new LexicalDeclarationItem(lexicalDeclarationType, variableName, initializer, Strict));

                if (!Match(TokenType.Comma))
                    break;
                Consume(TokenType.Comma);
            }

            return new LexicalDeclaration(lexicalDeclarationType, list, Strict);
        }

        private AbstractExpression? ParseExpression()
        {
            AbstractAssignmentExpression? expression = ParseAssignmentExpression();
            if (expression == null) return null;
            if (!Match(TokenType.Comma))
                return expression;
            var list = new List<AbstractAssignmentExpression>() { expression };
            do
            {
                Consume(TokenType.Comma);
                var nextExpression = ParseAssignmentExpression();
                if (nextExpression == null) return null;
                list.Add(nextExpression);
            } while (Match(TokenType.Comma));

            return new CommaExpression(list, Strict);
        }

        private AbstractAssignmentExpression? ParseAssignmentExpression()
        {
            AbstractLeftHandSideExpression? lhs;
            using (var lr = new LexerRewinder(lexer))
            {
                lhs = ParseLeftHandSideExpression();
                lr.Success = lhs != null;
            }
            if (lhs == null)
            {
                return ParseConditionalExpression();
            }
            if (Match(TokenType.Equals))
            {
                Consume();
                var rhs = ParseAssignmentExpression();
                if (rhs != null)
                    return new AssignmentExpression(lhs, rhs!, Strict);
                return null;
            }
            if (MatchAssignmentOperator())
            {
                var op = ParseAssignmentOperator();
                var rhs = ParseAssignmentExpression();
                if (rhs != null)
                    return new OperatorAssignmentExpression(lhs, op, rhs!, Strict);
                return null;
            }
            return ParseConditionalExpression(ParseLogicalOrExpression(lhs));
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

        private AbstractConditionalExpression? ParseConditionalExpression(AbstractLogicalOrExpression? expression = null)
        {
            if (expression == null)
            {
                expression = ParseLogicalOrExpression();
                if (expression == null) return null;
            }
            if (Consume(TokenType.QuestionMark) == null) return expression;
            var trueExpr = ParseAssignmentExpression();
            if (trueExpr == null) return null;
            if (Consume(TokenType.Colon) == null) return null;
            var falseExpr = ParseAssignmentExpression();
            if (falseExpr == null) return null;
            return new ConditionalExpression(expression, trueExpr, falseExpr, Strict);
        }

        private AbstractLogicalOrExpression? ParseLogicalOrExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractLogicalAndExpression? expression;
            if (lhs == null)
            {
                expression = ParseLogicalAndExpression();
            }
            else
                expression = ParseLogicalAndExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseLogicalOrTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var or = new LogicalOrExpression(expression, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                or = new LogicalOrExpression(or, tail.RHS, Strict);
            }
            return or;
        }

        private (bool success, TailContainer<AbstractLogicalAndExpression>? tail) ParseLogicalOrTail()
        {
            if (!Match(TokenType.DoublePipe))
                return (true, null);
            Consume();
            var rhs = ParseLogicalAndExpression();
            if (rhs == null) return (false, null);
            var tail = ParseLogicalOrTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<AbstractLogicalAndExpression>(rhs, tail.tail));
        }

        private AbstractLogicalAndExpression? ParseLogicalAndExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractBitwiseOrExpression? expression;
            if (lhs == null)
                expression = ParseBitwiseOrExpression();
            else
                expression = ParseBitwiseOrExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseLogicalAndTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var and = new LogicalAndExpression(expression, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                and = new LogicalAndExpression(and, tail.RHS, Strict);
            }
            return and;
        }

        private (bool success, TailContainer<AbstractBitwiseOrExpression>? tail) ParseLogicalAndTail()
        {
            if (!Match(TokenType.DoubleAmpersand))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseOrExpression();
            if (rhs == null) return (false, null);
            var tail = ParseLogicalAndTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<AbstractBitwiseOrExpression>(rhs, tail.tail));
        }

        private AbstractBitwiseOrExpression? ParseBitwiseOrExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractBitwiseXorExpression? expression;
            if (lhs == null)
                expression = ParseBitwiseXorExpression();
            else
                expression = ParseBitwiseXorExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseBitwiseOrTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var bitwiseOr = new BitwiseOrExpression(expression, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseOr = new BitwiseOrExpression(bitwiseOr, tail.RHS, Strict);
            }
            return bitwiseOr;
        }

        private (bool success, TailContainer<AbstractBitwiseXorExpression>? tail) ParseBitwiseOrTail()
        {
            if (!Match(TokenType.Pipe))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseXorExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseOrTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<AbstractBitwiseXorExpression>(rhs, tail.tail));
        }

        private AbstractBitwiseXorExpression? ParseBitwiseXorExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractBitwiseAndExpression? expression;
            if (lhs == null)
                expression = ParseBitwiseAndExpression();
            else
                expression = ParseBitwiseAndExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseBitwiseXorTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var bitwiseXor = new BitwiseXorExpression(expression, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseXor = new BitwiseXorExpression(bitwiseXor, tail.RHS, Strict);
            }
            return bitwiseXor;
        }

        private (bool success, TailContainer<AbstractBitwiseAndExpression>? tail) ParseBitwiseXorTail()
        {
            if (!Match(TokenType.Caret))
                return (true, null);
            Consume();
            var rhs = ParseBitwiseAndExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseXorTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<AbstractBitwiseAndExpression>(rhs, tail.tail));
        }

        private AbstractBitwiseAndExpression? ParseBitwiseAndExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractEqualityExpression? expression;
            if (lhs == null)
                expression = ParseEqualityExpression();
            else
                expression = ParseEqualityExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseBitwiseAndTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var bitwiseAnd = new BitwiseAndExpression(expression, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                bitwiseAnd = new BitwiseAndExpression(bitwiseAnd, tail.RHS, Strict);
            }
            return bitwiseAnd;
        }

        private (bool success, TailContainer<AbstractEqualityExpression>? tail) ParseBitwiseAndTail()
        {
            if (!Match(TokenType.Ampersand))
                return (true, null);
            Consume();
            var rhs = ParseEqualityExpression();
            if (rhs == null) return (false, null);
            var tail = ParseBitwiseAndTail();
            if (!tail.success) return (false, null);
            return (true, new TailContainer<AbstractEqualityExpression>(rhs, tail.tail));
        }

        private AbstractEqualityExpression? ParseEqualityExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractRelationalExpression? expression;
            if (lhs == null)
                expression = ParseRelationalExpression();
            else
                expression = ParseRelationalExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseEqualityTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var equality = new EqualityExpression(expression, tail.Op, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                equality = new EqualityExpression(equality, tail.Op, tail.RHS, Strict);
            }
            return equality;
        }

        private (bool success, TailOperatorContainer<AbstractRelationalExpression, EqualityOperator>? tail) ParseEqualityTail()
        {
            if (!MatchEqualityOperator())
                return (true, null);
            var op = ParseEqualityOperator();
            var rhs = ParseRelationalExpression();
            if (rhs == null) return (false, null);
            var tail = ParseEqualityTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<AbstractRelationalExpression, EqualityOperator>(rhs, op, tail.tail));
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

        private AbstractRelationalExpression? ParseRelationalExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractShiftExpression? expression;
            if (lhs == null)
                expression = ParseShiftExpression();
            else
                expression = ParseShiftExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseRelationalTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var relational = new RelationalExpression(expression, tail.Op, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                relational = new RelationalExpression(relational, tail.Op, tail.RHS, Strict);
            }
            return relational;
        }

        private (bool success, TailOperatorContainer<AbstractShiftExpression, RelationalOperator>? tail) ParseRelationalTail()
        {
            if (!MatchRelationalOperator())
                return (true, null);
            var op = ParseRelationalOperator();
            var rhs = ParseShiftExpression();
            if (rhs == null) return (false, null);
            var tail = ParseRelationalTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<AbstractShiftExpression, RelationalOperator>(rhs, op, tail.tail));
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

        private AbstractShiftExpression? ParseShiftExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractAdditiveExpression? expression;
            if (lhs == null)
                expression = ParseAdditiveExpression();
            else
                expression = ParseAdditiveExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseShiftTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var shift = new ShiftExpression(expression, tail.Op, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                shift = new ShiftExpression(shift, tail.Op, tail.RHS, Strict);
            }
            return shift;
        }

        private (bool success, TailOperatorContainer<AbstractAdditiveExpression, ShiftOperator>? tail) ParseShiftTail()
        {
            if (!MatchShiftOperator())
                return (true, null);
            var op = ParseShiftOperator();
            var rhs = ParseAdditiveExpression();
            if (rhs == null) return (false, null);
            var tail = ParseShiftTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<AbstractAdditiveExpression, ShiftOperator>(rhs, op, tail.tail));
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

        private AbstractAdditiveExpression? ParseAdditiveExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractMultiplicativeExpression? expression;
            if (lhs == null)
                expression = ParseMultiplicativeExpression();
            else
                expression = ParseMultiplicativeExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseAdditiveTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var additive = new AdditiveExpression(expression, tail.Op, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                additive = new AdditiveExpression(additive, tail.Op, tail.RHS, Strict);
            }
            return additive;
        }

        private (bool success, TailOperatorContainer<AbstractMultiplicativeExpression, AdditiveOperator>? tail) ParseAdditiveTail()
        {
            if (!MatchAdditiveOperator())
                return (true, null);
            var op = ParseAdditiveOperator();
            var rhs = ParseMultiplicativeExpression();
            if (rhs == null) return (false, null);
            var tail = ParseAdditiveTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<AbstractMultiplicativeExpression, AdditiveOperator>(rhs, op, tail.tail));
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

        private AbstractMultiplicativeExpression? ParseMultiplicativeExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            AbstractExponentiationExpression? expression;
            if (lhs == null)
                expression = ParseExponentiationExpression();
            else
                expression = ParseExponentiationExpression(lhs);
            if (expression == null) return null;
            var (success, tail) = ParseMultiplicativeTail();
            if (!success) return null;

            if (tail == null)
                return expression;

            var multiplicative = new MultiplicativeExpression(expression, tail.Op, tail.RHS, Strict);
            while (tail.Tail != null)
            {
                tail = tail.Tail;
                multiplicative = new MultiplicativeExpression(multiplicative, tail.Op, tail.RHS, Strict);
            }
            return multiplicative;
        }

        private (bool success, TailOperatorContainer<AbstractExponentiationExpression, MultiplicativeOperator>? tail) ParseMultiplicativeTail()
        {
            if (!MatchMultiplicativeOperator())
                return (true, null);
            var op = ParseMultiplicativeOperator();
            var rhs = ParseExponentiationExpression();
            if (rhs == null) return (false, null);
            var tail = ParseMultiplicativeTail();
            if (!tail.success) return (false, null);
            return (true, new TailOperatorContainer<AbstractExponentiationExpression, MultiplicativeOperator>(rhs, op, tail.tail));
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

        private AbstractExponentiationExpression? ParseExponentiationExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            if (lhs == null && MatchUnaryOperator())
                return ParseUnaryExpression();

            AbstractUpdateExpression? update;
            if (lhs == null)
                update = ParseUpdateExpression();
            else
                update = ParseUpdateExpression(lhs);
            if (update == null) return null;

            if (Consume(TokenType.DoubleAsterisk) != null)
            {
                var rhs = ParseExponentiationExpression();
                if (rhs == null) return null;
                return new ExponentiationExpression(update, rhs!, Strict);
            }
            return update;
        }

        private AbstractUnaryExpression? ParseUnaryExpression()
        {
            if (!MatchUnaryOperator())
            {
                return ParseUpdateExpression();
            }
            var op = ParseUnaryOperator();
            var expression = ParseUnaryExpression();
            if (expression == null) return null;
            return new OperatorUnaryExpression(op, expression, Strict);
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

        private AbstractUpdateExpression? ParseUpdateExpression(AbstractLeftHandSideExpression? lhs = null)
        {
            if (lhs == null && MatchUpdateOperator())
            {
                var op = ParseUpdateOperator();
                var expression = ParseUnaryExpression();
                if (expression == null) return null;
                return new PrefixUpdateExpression(expression, op, Strict);
            }

            if (lhs == null)
                lhs = ParseLeftHandSideExpression();

            if (lhs == null) return null;
            if (MatchUpdateOperator() && !CurrentToken.PassedNewLine)
            {
                var op = ParseUpdateOperator();
                return new PostfixUpdateExpression(lhs, op, Strict);
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

        private AbstractLeftHandSideExpression? ParseLeftHandSideExpression()
        {
            var (callExpression, memberExpression) = ParseCallExpression();
            if (callExpression != null || memberExpression != null)
                return (AbstractLeftHandSideExpression?)callExpression ?? memberExpression;
            return ParseNewExpression();
        }

        private AbstractNewExpression? ParseNewExpression()
        {
            using (var lr = new LexerRewinder(lexer))
            {
                if (Consume(TokenType.New) != null)
                {
                    var innerExpression = ParseNewExpression();
                    if (innerExpression != null)
                    {
                        if (lr.Success = CurrentToken.Type != TokenType.ParenOpen)
                            return new NewExpression(innerExpression, Strict);
                    }
                }
            }
            return ParseMemberExpression();
        }

        private (AbstractCallExpression?, AbstractMemberExpression?) ParseCallExpression()
        {
            AbstractCallExpression? lhs = null;
            using (var lr = new LexerRewinder(lexer))
            {
                var (callExpression, memberExpression) = ParseCoveredCallExpression();
                lr.Success = callExpression != null || memberExpression != null;
                if (lr.Success)
                {
                    if (callExpression != null)
                        lhs = callExpression;
                    else
                        return (null, memberExpression);
                }
            }
            if (lhs == null)
                lhs = ParseSuperCallExpression();
            if (lhs == null) return (null, null);

            var (success, tail) = ParseCallExpressionTail();
            if (!success) return (null, null);

            if (tail == null)
                return (lhs, null);

            do
            {
                if (tail.Arguments != null)
                    lhs = new RecursiveCallExpression(lhs, tail.Arguments, Strict);
                else if (tail.IdentifierName != null)
                    lhs = new DotCallExpression(lhs, tail.IdentifierName, Strict);
                else if (tail.Expression != null)
                    lhs = new IndexCallExpression(lhs, tail.Expression, Strict);
                else
                {
                    Expected<AbstractCallExpression>("call expression tail component");
                    return (null, null);
                }

                tail = tail.Tail;
            } while (tail != null);

            return (lhs, null);
        }

        private (bool success, CallExpressionTail? tail) ParseCallExpressionTail()
        {
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return (false, null);
                if (Consume(TokenType.BracketClose) == null) return (false, null);
                var tail = ParseCallExpressionTail();
                if (!tail.success) return (false, null);
                return (true, new CallExpressionTail(expression, tail.tail));
            }
            else if (Match(TokenType.Period))
            {
                Consume();
                var identifierName = Consume(TokenType.Identifier)!.Value;
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
            return new SuperCall(arguments, Strict);
        }

        private (MemberCallExpression?, AbstractMemberExpression?) ParseCoveredCallExpression()
        {
            var memberExpression = ParseMemberExpression();
            if (memberExpression == null) return (null, null);
            Arguments? arguments = null;
            using (var lr = new LexerRewinder(lexer))
            {
                arguments = ParseArguments();
                lr.Success = arguments != null;
            }
            if (arguments == null) return (null, memberExpression);
            return (new MemberCallExpression(memberExpression, arguments, Strict), null);
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

        private AbstractMemberExpression? ParseMemberExpression()
        {
            AbstractMemberExpression? lhs = null;
            if (Match(TokenType.New))
            {
                Consume();
                var left = ParseMemberExpression();
                if (left == null) return null;
                var arguments = ParseArguments();
                if (arguments == null) return null;
                lhs = new NewMemberExpression(left, arguments, Strict);
            }
            if (Match(TokenType.Super))
            {
                lhs = ParseSuperProperty();
            }
            if (lhs == null)
            {
                lhs = ParsePrimaryExpression();
            }
            if (lhs == null) return null;
            var (success, tail) = ParseMemberExpressionTail();
            if (!success) return null;

            if (tail == null)
                return lhs;

            do
            {
                if (tail.IdentifierName != null)
                    lhs = new DotMemberExpression(lhs, tail.IdentifierName, Strict);
                else if (tail.Expression != null)
                    lhs = new IndexMemberExpression(lhs, tail.Expression, Strict);
                else
                {
                    return Expected<AbstractMemberExpression>("member expression tail component");
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

        private AbstractMemberExpression? ParseSuperProperty()
        {
            Consume(TokenType.Super);
            if (Match(TokenType.BracketOpen))
            {
                Consume();
                var expression = ParseExpression();
                if (expression == null) return null;
                if (Consume(TokenType.BracketClose) == null) return null;
                return new SuperIndexMemberExpression(expression, Strict);
            }
            if (Match(TokenType.Period))
            {
                Consume();
                return new SuperDotMemberExpression(Consume(TokenType.Identifier)!.Value, Strict);
            }
            return Expected<AbstractMemberExpression>("super property");
        }

        private AbstractPrimaryExpression? ParsePrimaryExpression()
        {
            switch (CurrentToken.Type)
            {
                case TokenType.This:
                    Consume();
                    return ThisExpression.Instance(Strict);
                case TokenType.Identifier:
                    return new IdentifierReference(new Identifier(Consume(TokenType.Identifier)!.Value, Strict), Strict);
                case TokenType.BoolLiteral:
                    return new Literal(Consume().BoolValue());
                case TokenType.NullLiteral:
                    Consume();
                    return Literal.NullLiteral;
                case TokenType.NumericLiteral:
                    return new Literal(Consume().DoubleValue(Strict));
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

            return Expected<AbstractPrimaryExpression>("primary expression");
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
                var regex = Consume().RegexValue(Strict);
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
            return new ArrayLiteral(items, Strict);
        }

        private ObjectLiteral? ParseObjectLiteral()
        {
            Consume(TokenType.CurlyOpen);
            if (Match(TokenType.CurlyClose))
            {
                Consume();
                return new ObjectLiteral(Utils.EmptyList<IPropertyDefinition>(), Strict);
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
                            identifier = propertyName.DoubleValue(Strict).ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                            identifier = propertyName.DoubleValue(Strict).ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                            propertyDefinition = new IdentifierReference(new Identifier(identifier!.Value, Strict), Strict);
                    }
                if (propertyDefinition == null)
                    return Expected<ObjectLiteral>("object literal property definition");

                definitions.Add(propertyDefinition);

                if (Match(TokenType.Comma))
                    Consume();
            }
            Consume(TokenType.CurlyClose);

            return new ObjectLiteral(definitions, Strict);
        }

        private IMethodDefinition? ParseMethodDefinition()
        {
            if (CurrentToken.Type != TokenType.Identifier)
                return Expected<IMethodDefinition>("method name, get, or set");
            var identifier = Consume(TokenType.Identifier)!.Value;

            if (identifier == "get")
            {
                using (var lr = new LexerRewinder(lexer))
                {
                    var propNameId = Consume();
                    var propertyName = propNameId.Value;
                    if (Consume(TokenType.ParenOpen) != null && Consume(TokenType.ParenClose) != null && Consume(TokenType.CurlyOpen) != null)
                    {
                        var body = ParseStatementList(setStrict: !Strict);
                        if (body != null && Consume(TokenType.CurlyClose) != null)
                        {
                            lr.Success = true;
                            return new Getter(propertyName, new FunctionStatementList(body));
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
                                    formalParameter = new FormalParameter(new Identifier(name, Strict), initializer);
                            }
                            else
                                formalParameter = new FormalParameter(new Identifier(name, Strict));
                            if (formalParameter != null && Consume(TokenType.ParenClose) != null && Consume(TokenType.CurlyOpen) != null)
                            {
                                var body = ParseStatementList(setStrict: !Strict);
                                if (body != null && Consume(TokenType.CurlyClose) != null)
                                {
                                    lr.Success = true;
                                    return new Setter(propertyName, formalParameter, new FunctionStatementList(body));
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
                var body = ParseStatementList(setStrict: !Strict);
                if (body == null) return null;
                if (Consume(TokenType.CurlyClose) == null) return null;
                return new MethodDefinition(identifier, parameters, new FunctionStatementList(body));
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
            var statementList = ParseStatementList(setStrict: !Strict);
            if (statementList == null) return null;
            var statements = new FunctionStatementList(statementList);
            if (Consume(TokenType.CurlyClose) == null) return null;

            if (functionName != null)
                return new FunctionExpression(new Identifier(functionName, Strict), parameters, statements, Strict);
            return new FunctionExpression(parameters, statements, Strict);
        }

        private ParenthesizedExpression? ParseParenthesizedExpression()
        {
            Consume(TokenType.ParenOpen);
            var expression = ParseExpression();
            if (expression == null) return null;
            if (Consume(TokenType.ParenClose) == null) return null;
            return new ParenthesizedExpression(expression, Strict);
        }
    }
}
