using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public interface ISharedFunctions : IHasLexicallyScopedDeclarations, IHasEvaluate
    {
        IReadOnlyList<string> VarDeclaredNames();
        IReadOnlyList<IScopedDeclaration> VarScopedDeclarations();
    }

    public abstract class Statement : ParseNode, IStatementListItem, ILabelledItem
    {
        protected Statement(bool isStrictMode) : base(isStrictMode)
        {
        }

        public abstract Completion Evaluate(Interpreter interpreter);

        public virtual Completion LabelledEvaluate(Interpreter interpreter, List<string> labelSet)
        {
            if (this is LabelledStatement l)
            {
                return l.LabelledEvaluate(interpreter, labelSet);
            }
            if (this is BreakableStatement b)
            {
                return b.LabelledEvaluate(interpreter, labelSet);
            }
            return this.Evaluate(interpreter);
        }

        public abstract IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations();
        public abstract IReadOnlyList<string> VarDeclaredNames();
        public abstract IReadOnlyList<IScopedDeclaration> VarScopedDeclarations();
    }

    public sealed class ExpressionStatement : Statement
    {
        public readonly AbstractExpression expression;

        public ExpressionStatement(AbstractExpression expression, bool isStrictMode) : base(isStrictMode)
        {
            this.expression = expression;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return expression.Evaluate(interpreter).GetValue();
        }
    }

    public abstract class BreakableStatement : Statement
    {
        protected BreakableStatement(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class VariableStatement : Statement
    {
        public readonly VariableDeclarationList variableDeclarations;

        public VariableStatement(VariableDeclarationList variableDeclarations, bool isStrictMode) : base(isStrictMode)
        {
            this.variableDeclarations = variableDeclarations;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return variableDeclarations.BoundNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return variableDeclarations.Cast<IScopedDeclaration>().ToList();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var comp = variableDeclarations.Evaluate(interpreter);
            if (comp.IsAbrupt()) return comp;
            return Completion.NormalCompletion();
        }
    }

    public class VariableStatementItem
    {
        public readonly string name;
        public readonly AbstractAssignmentExpression assignmentExpression;

        public VariableStatementItem(string name, AbstractAssignmentExpression assignmentExpression)
        {
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }
    }

    public sealed class ContinueStatement : Statement
    {
        public readonly bool hasLabel;
        public readonly string? label;

        public ContinueStatement(bool isStrictMode) : base(isStrictMode)
        {
            hasLabel = false;
        }

        public ContinueStatement(string label, bool isStrictMode) : base(isStrictMode)
        {
            hasLabel = true;
            this.label = label;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (hasLabel)
                return new Completion(CompletionType.Continue, null, label);
            return new Completion(CompletionType.Continue, null, null);
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }
    }

    public sealed class BreakStatement : Statement
    {
        public readonly bool hasLabel;
        public readonly string? label;

        public BreakStatement(bool isStrictMode) : base(isStrictMode)
        {
            hasLabel = false;
        }

        public BreakStatement(string label, bool isStrictMode) : base(isStrictMode)
        {
            hasLabel = true;
            this.label = label;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (hasLabel)
                return new Completion(CompletionType.Break, null, label);
            return new Completion(CompletionType.Break, null, null);
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }
    }

    public sealed class ReturnStatement : Statement
    {
        public readonly AbstractExpression? expression;

        public ReturnStatement(bool isStrictMode) : base(isStrictMode) { }
        public ReturnStatement(AbstractExpression expression, bool isStrictMode) : base(isStrictMode)
        {
            this.expression = expression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (expression == null)
                return new Completion(CompletionType.Return, UndefinedValue.Instance, null);
            var exprComp = expression.Evaluate(interpreter).GetValue();
            if (exprComp.IsAbrupt()) return exprComp;
            //TODO async (spec 13.10.1 step 3)
            return new Completion(CompletionType.Return, exprComp.value, null);
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }
    }

    public interface ILabelledItem : ISharedFunctions
    {
        Completion LabelledEvaluate(Interpreter interpreter, List<string> labelSet);
    }

    public sealed class LabelledStatement : Statement
    {
        public readonly Identifier identifier;
        public readonly ILabelledItem labelledItem;

        public LabelledStatement(Identifier identifier, ILabelledItem labelledItem, bool isStrictMode) : base(isStrictMode)
        {
            this.identifier = identifier;
            this.labelledItem = labelledItem;
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return labelledItem.VarDeclaredNames();
        }

        public IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            if (labelledItem is LabelledStatement l)
                return l.TopLevelVarDeclaredNames();
            if (labelledItem is Statement)
                return labelledItem.VarDeclaredNames();
            if (labelledItem is FunctionDeclaration f)
                return f.BoundNames();
            throw new InvalidOperationException($"Invalid ILabelledItem {labelledItem.GetType()}");
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return labelledItem.VarScopedDeclarations();
        }

        public IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            if (labelledItem is LabelledStatement l)
                return l.TopLevelVarScopedDeclarations();
            if (labelledItem is Statement)
                return labelledItem.VarScopedDeclarations();
            if (labelledItem is FunctionDeclaration f)
                return new List<IScopedDeclaration> { f };
            throw new InvalidOperationException($"Invalid ILabelledItem {labelledItem.GetType()}");
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return labelledItem.LexicallyScopedDeclarations();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labelSet)
        {
            labelSet.Add(identifier.name);
            var stmtResult = labelledItem.LabelledEvaluate(interpreter, labelSet);
            if (stmtResult.completionType == CompletionType.Break && identifier.name == stmtResult.target)
            {
                stmtResult = Completion.NormalCompletion(stmtResult.value);
            }
            return stmtResult;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return LabelledEvaluate(interpreter, new List<string>());
        }

        public IReadOnlyList<string> LexicallyDeclaredNames()
        {
            if (labelledItem is FunctionDeclaration f)
                return f.BoundNames();
            return Utils.EmptyList<string>();
        }
    }

    public sealed class ThrowStatement : Statement
    {
        public readonly AbstractExpression expression;

        public ThrowStatement(AbstractExpression expression, bool isStrictMode) : base(isStrictMode)
        {
            this.expression = expression;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var exprComp = expression.Evaluate(interpreter).GetValue();
            if (exprComp.IsAbrupt()) return exprComp;
            return new Completion(CompletionType.Throw, exprComp.value, null);
        }
    }

    public sealed class EmptyStatement : Statement
    {
        public EmptyStatement() : base(false)
        {
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return Completion.NormalCompletion();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }
    }

    public class DebuggerStatement : Statement
    {
        public DebuggerStatement() : base(false)
        {
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
            return Completion.NormalCompletion();
        }
    }
}
