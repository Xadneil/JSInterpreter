using System;
using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    public class StatementList : Statement
    {
        public readonly IReadOnlyList<IStatementListItem> statements;

        public StatementList(IReadOnlyList<IStatementListItem> statements, bool isStrictMode) : base(isStrictMode)
        {
            this.statements = statements;
        }

        public bool Any()
        {
            return statements.Any();
        }

        public IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return statements.SelectMany(i =>
            {
                if (i is HoistableDeclaration h)
                    return h.BoundNames();
                else if (i is Declaration)
                    return Utils.EmptyList<string>();
                else if (i is LabelledStatement l)
                    return l.TopLevelVarDeclaredNames();
                return i.VarDeclaredNames();
            }).ToList();
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

        public IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return statements.SelectMany(i =>
            {
                if (i is HoistableDeclaration h)
                    return new List<IScopedDeclaration>() { h };
                else if (i is Declaration)
                    return Utils.EmptyList<IScopedDeclaration>();
                else if (i is LabelledStatement l)
                    return l.TopLevelVarScopedDeclarations();
                return i.VarScopedDeclarations();
            }).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return statements.SelectMany(i => i.LexicallyScopedDeclarations()).ToList();
        }

        public IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return statements.SelectMany(i =>
            {
                if (i is Declaration d && !(i is HoistableDeclaration))
                    return new List<IDeclarationPart>() { d };
                return Utils.EmptyList<IDeclarationPart>();
            }).ToList();
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

        public virtual IReadOnlyList<string> LexicallyDeclaredNames()
        {
            return statements.SelectMany(i => i.LexicallyDeclaredNames()).ToList();
        }

        public IReadOnlyList<string> TopLevelLexicallyDeclaredNames()
        {
            return statements.SelectMany(i => i.TopLevelLexicallyDeclaredNames()).ToList();
        }
    }

    public sealed class FunctionStatementList : StatementList
    {
        public FunctionStatementList(StatementList statementList) : base(statementList.statements, statementList.IsStrictMode) { }

        public override IReadOnlyList<string> LexicallyDeclaredNames()
        {
            return TopLevelLexicallyDeclaredNames();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return TopLevelLexicallyScopedDeclarations();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return TopLevelVarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return TopLevelVarScopedDeclarations();
        }

        public Completion EvaluateBody(FunctionObject functionObject, IReadOnlyList<IValue> arguments)
        {
            var comp = functionObject.FunctionDeclarationInstantiation(arguments);
            if (comp.IsAbrupt()) return comp;
            return Evaluate(Interpreter.Instance());
        }
    }

    public sealed class ScriptStatementList : StatementList
    {
        public ScriptStatementList(StatementList statementList) : base(statementList.statements, statementList.IsStrictMode) { }

        public override IReadOnlyList<string> LexicallyDeclaredNames()
        {
            return TopLevelLexicallyDeclaredNames();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return TopLevelLexicallyScopedDeclarations();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return TopLevelVarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return TopLevelVarScopedDeclarations();
        }

    }

    public interface IStatementListItem : ISharedFunctions
    {
        public IReadOnlyList<string> LexicallyDeclaredNames()
        {
            if (this is LabelledStatement l)
                return l.LexicallyDeclaredNames();
            if (this is IDeclarationPart d)
            {
                return d.BoundNames();
            }
            return Utils.EmptyList<string>();
        }

        public IReadOnlyList<string> TopLevelLexicallyDeclaredNames()
        {
            if (this is IDeclarationPart d && !(this is HoistableDeclaration))
                return d.BoundNames();
            return Utils.EmptyList<string>();
        }
    }
}
