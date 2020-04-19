using System;
using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    class StatementList : Statement, IHasLexicallyScopedDeclarations
    {
        public readonly IReadOnlyList<IStatementListItem> statements;

        public StatementList(IReadOnlyList<IStatementListItem> statements)
        {
            this.statements = statements;
        }

        public bool Any()
        {
            return statements.Any();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return statements.SelectMany(i => i.TopLevelVarDeclaredNames()).ToList();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            List<IScopedDeclaration> ret = new List<IScopedDeclaration>();
            foreach (var statement in statements)
            {
                if (!(statement is Declaration))
                {
                    ret.AddRange(statement.VarScopedDeclarations());
                }
            }
            return ret;
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return statements.SelectMany(i => i.TopLevelVarScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return statements.SelectMany(i => i.LexicallyScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return statements.SelectMany(i => i.TopLevelLexicallyScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return statements.SelectMany(i => i.VarDeclaredNames()).ToList();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            Completion lastValue = Completion.NormalCompletion();
            foreach (var statement in statements)
            {
                var val = statement.Evaluate(interpreter);
                if (val.IsAbrupt()) return val;
                if (val.value != null)
                    lastValue = val;
            }
            return lastValue;
        }
    }

    class FunctionStatementList : StatementList
    {
        public FunctionStatementList(IReadOnlyList<IStatementListItem> statements) : base(statements) { }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return base.TopLevelVarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return base.TopLevelLexicallyScopedDeclarations();
        }

        public Completion EvaluateBody(FunctionObject functionObject, IReadOnlyList<IValue> arguments)
        {
            functionObject.FunctionDeclarationInstantiation(arguments);
            return Evaluate(Interpreter.Instance());
        }
    }

    interface IStatementListItem : ISharedFunctions
    {
    }
}
