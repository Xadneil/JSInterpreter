using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public interface ISharedFunctions : IHasLexicallyScopedDeclarations, IHasEvaluate
    {
        IReadOnlyList<string> VarDeclaredNames();
        IReadOnlyList<string> TopLevelVarDeclaredNames();
        IReadOnlyList<IScopedDeclaration> VarScopedDeclarations();
        IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations();
        IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations();
    }

    public abstract class Statement : IStatementListItem, ILabelledItem
    {
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
        public abstract IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations();
        public abstract IReadOnlyList<string> TopLevelVarDeclaredNames();
        public abstract IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations();
        public abstract IReadOnlyList<string> VarDeclaredNames();
        public abstract IReadOnlyList<IScopedDeclaration> VarScopedDeclarations();
    }

    public class ExpressionStatement : Statement
    {
        public readonly IExpression expression;

        public ExpressionStatement(IExpression expression)
        {
            this.expression = expression;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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

    }

    public class VariableStatement : Statement
    {
        public readonly VariableDeclarationList variableDeclarations;

        public VariableStatement(VariableDeclarationList variableDeclarations)
        {
            this.variableDeclarations = variableDeclarations;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return variableDeclarations.BoundNames();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return Utils.EmptyList<string>();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return variableDeclarations.Cast<IScopedDeclaration>().ToList();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return Utils.EmptyList<IScopedDeclaration>();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            foreach (var variable in variableDeclarations.Where(v => v.assignmentExpression != null))
            {
                var lhsComp = interpreter.ResolveBinding(variable.name);
                if (lhsComp.IsAbrupt()) return lhsComp;
                if (!(lhsComp.value is ReferenceValue lhs))
                    throw new InvalidOperationException("ResolveBinding did not return a reference");
                Completion value;
                if (variable.assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
                    value = functionExpression.NamedEvaluate(interpreter, variable.name);
                else
                    value = variable.assignmentExpression.Evaluate(interpreter).GetValue();
                if (value.IsAbrupt()) return value;
                lhs.PutValue(value.value);
            }
            return Completion.NormalCompletion(UndefinedValue.Instance);
        }
    }

    public class VariableStatementItem
    {
        public readonly string name;
        public readonly IAssignmentExpression assignmentExpression;

        public VariableStatementItem(string name, IAssignmentExpression assignmentExpression)
        {
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }
    }

    public class ContinueStatement : Statement
    {
        public readonly bool hasLabel;
        public readonly string label;

        public ContinueStatement()
        {
            hasLabel = false;
        }

        public ContinueStatement(string label)
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

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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

    public class BreakStatement : Statement
    {
        public readonly bool hasLabel;
        public readonly string label;

        public BreakStatement()
        {
            hasLabel = false;
        }

        public BreakStatement(string label)
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

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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

    public class ReturnStatement : Statement
    {
        public readonly IExpression expression;

        public ReturnStatement() { }
        public ReturnStatement(IExpression expression)
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

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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

    public class LabelledStatement : Statement
    {
        public readonly Identifier identifier;
        public readonly ILabelledItem labelledItem;

        public LabelledStatement(Identifier identifier, ILabelledItem labelledItem)
        {
            this.identifier = identifier;
            this.labelledItem = labelledItem;
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return labelledItem.VarDeclaredNames();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return labelledItem.TopLevelVarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return labelledItem.VarScopedDeclarations();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return labelledItem.TopLevelVarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return labelledItem.LexicallyScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
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

    public class ThrowStatement : Statement
    {
        public readonly IExpression expression;

        public ThrowStatement(IExpression expression)
        {
            this.expression = expression;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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

    public class EmptyStatement : Statement
    {
        public override Completion Evaluate(Interpreter interpreter)
        {
            return Completion.NormalCompletion();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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
        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return VarScopedDeclarations();
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
