using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public enum IterationKind
    {
        Enumerate,
        Iterate
    }

    public enum IteratorKind
    {
        Sync, Async
    }

    public enum LHSKind
    {
        Assignment,
        VarBinding,
        LexicalBinding
    }

    public interface IForInOfInitializer : IHasEvaluate
    {
    }

    public class ForDeclaration : IForInOfInitializer
    {
        public bool isConst;
        public string name;

        public ForDeclaration(bool isConst, string name)
        {
            this.isConst = isConst;
            this.name = name;
        }

        public void BindingInstantiation(LexicalEnvironment environment)
        {
            var envRec = environment.EnvironmentRecord;
            //TODO assert envRec is Declarative
            if (isConst)
            {
                envRec.CreateImmutableBinding(name, true);
            }
            else
            {
                envRec.CreateMutableBinding(name, false);
            }
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveBinding(name);
        }
    }

    public abstract class IterationStatement : BreakableStatement
    {
        public override Completion Evaluate(Interpreter interpreter)
        {
            throw new InvalidOperationException("Evaluate should not be called on an iteration statement. Only LabelledEvaluate should be called.");
        }

        protected bool LoopContinues(Completion completion, List<string> labelSet)
        {
            if (completion.completionType == CompletionType.Normal) return true;
            if (completion.completionType != CompletionType.Continue) return false;
            if (completion.target == null) return true;
            if (labelSet.Contains(completion.target)) return true;
            return false;
        }

        protected Completion ForBodyEvaluation(IExpression test, IExpression increment, Statement stmt, IEnumerable<string> perIterationBindings, List<string> labelSet)
        {
            IValue V = UndefinedValue.Instance;
            var comp = CreatePerIterationEnvironment(perIterationBindings);
            if (comp.IsAbrupt()) return comp;
            while (true)
            {
                if (test != null)
                {
                    var testComp = test.Evaluate(Interpreter.Instance());
                    var testValueComp = testComp.GetValue();
                    if (testValueComp.IsAbrupt()) return testValueComp;
                    if (!testValueComp.value.ToBoolean().boolean) return Completion.NormalCompletion(V);
                }
                var result = stmt.Evaluate(Interpreter.Instance());
                if (!LoopContinues(result, labelSet))
                    return result.UpdateEmpty(V);
                if (result.value != null)
                    V = result.value;
                comp = CreatePerIterationEnvironment(perIterationBindings);
                if (comp.IsAbrupt()) return comp;
                if (increment != null)
                {
                    var inc = increment.Evaluate(Interpreter.Instance()).GetValue();
                    if (inc.IsAbrupt()) return inc;
                }
            }
        }

        private Completion CreatePerIterationEnvironment(IEnumerable<string> perIterationBindings)
        {
            if (perIterationBindings.Any())
            {
                var lastIterationEnv = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
                var lastIterationEnvRec = lastIterationEnv.EnvironmentRecord;
                var outer = lastIterationEnv.Outer;
                if (outer == null)
                    throw new InvalidOperationException("Spec 13.7.4.9 step 1d");
                var thisIterationEnv = outer.NewDeclarativeEnvironment();
                var thisIterationEnvRec = thisIterationEnv.EnvironmentRecord;
                foreach (var bn in perIterationBindings)
                {
                    thisIterationEnvRec.CreateMutableBinding(bn, false);
                    var lastValue = lastIterationEnvRec.GetBindingValue(bn, true);
                    if (lastValue.IsAbrupt()) return lastValue;
                    thisIterationEnvRec.InitializeBinding(bn, lastValue.value);
                }
                Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = thisIterationEnv;
            }
            return Completion.NormalCompletion(UndefinedValue.Instance);
        }

        protected CompletionOr<IEnumerator<Completion>> ForInOfHeadEvaluation(IEnumerable<string> TDZNames, IExpression expr, IterationKind iterationKind)
        {
            var oldEnv = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
            if (TDZNames.Any())
            {
                if (TDZNames.Distinct().Count() < TDZNames.Count())
                    throw new InvalidOperationException("Spec 13.7.5.12 step 2a");
                var TDZ = oldEnv.NewDeclarativeEnvironment();
                var TDZEnvRec = TDZ.EnvironmentRecord;
                foreach (var name in TDZNames)
                {
                    TDZEnvRec.CreateMutableBinding(name, false);
                }
                Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = TDZ;
            }
            var exprRef = expr.Evaluate(Interpreter.Instance());
            Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = oldEnv;
            var exprValue = exprRef.GetValue();
            if (exprValue.IsAbrupt()) return exprValue.WithEmpty<IEnumerator<Completion>>();
            if (iterationKind == IterationKind.Enumerate)
            {
                if (exprValue.value == UndefinedValue.Instance || exprValue.value == NullValue.Instance)
                    return new Completion(CompletionType.Break, null, null).WithEmpty<IEnumerator<Completion>>();
                var obj = exprValue.value.ToObject().value as Object;
                return obj.EnumerateObjectProperties();
            }
            else
            {
                return (exprValue.value.ToObject().value as Object).GetIterator();
            }
        }

        protected Completion ForInOfBodyEvaluation(IForInOfInitializer lhs, Statement stmt, IEnumerator<Completion> iteratorRecord, IterationKind iterationKind, LHSKind lhsKind, List<string> labelSet, IteratorKind? iteratorKindNullable = null)
        {
            var iteratorKind = iteratorKindNullable.GetValueOrDefault(IteratorKind.Sync);
            var oldEnv = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
            IValue V = UndefinedValue.Instance;
            // TODO assuming destructuring is false
            bool destructuring = false;
            while (true)
            {
                var done = iteratorRecord.MoveNext();
                var nextResultComp = iteratorRecord.Current;
                if (nextResultComp.IsAbrupt()) return nextResultComp;
                var nextResult = nextResultComp.value;
                if (iteratorKind == IteratorKind.Async)
                    throw new NotImplementedException("async");
                if (!(nextResult is Object nextResultObject))
                    return Completion.ThrowTypeError();
                if (done)
                    return Completion.NormalCompletion(V);
                var nextValueComp = nextResultObject.Get("value");
                if (nextValueComp.IsAbrupt()) return nextValueComp;
                var nextValue = nextValueComp.value;
                Completion lhsRef = Completion.NormalCompletion();
                if (lhsKind == LHSKind.Assignment || lhsKind == LHSKind.VarBinding)
                {
                    if (!destructuring)
                        lhsRef = lhs.Evaluate(Interpreter.Instance());
                }
                else
                {
                    if (lhsKind != LHSKind.LexicalBinding)
                        throw new InvalidOperationException("Spec 13.7.5.13 step 6hi");
                    if (!(lhs is ForDeclaration forDeclaration))
                        throw new InvalidOperationException("Spec 13.7.5.13 step 6hii");
                    var iterationEnv = oldEnv.NewDeclarativeEnvironment();
                    forDeclaration.BindingInstantiation(iterationEnv);
                    Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = iterationEnv;
                    if (!destructuring)
                        lhsRef = Interpreter.Instance().ResolveBinding(forDeclaration.name);
                }
                Completion status;
                if (!destructuring)
                {
                    if (lhsRef.IsAbrupt())
                        status = lhsRef;
                    else if (lhsKind == LHSKind.LexicalBinding)
                        status = (lhsRef.value as ReferenceValue).InitializeReferencedBinding(nextValue);
                    else
                        status = (lhsRef.value as ReferenceValue).PutValue(nextValue);
                }
                else
                {
                    throw new NotImplementedException("destructuring");
                }
                if (status.IsAbrupt())
                {
                    Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = oldEnv;
                    if (iteratorKind == IteratorKind.Async)
                        throw new NotImplementedException("async");
                    if (iterationKind == IterationKind.Enumerate)
                        return status;
                    else
                    {
                        var oldCurrent = iteratorRecord.Current;
                        iteratorRecord.Dispose();
                        if (oldCurrent.value != iteratorRecord.Current.value)
                            return iteratorRecord.Current;
                        return status;
                    }
                }
                var result = stmt.Evaluate(Interpreter.Instance());
                Interpreter.Instance().RunningExecutionContext().LexicalEnvironment = oldEnv;
                if (!LoopContinues(result, labelSet))
                {
                    if (iterationKind == IterationKind.Enumerate)
                    {
                        return result.UpdateEmpty(V);
                    }
                    else
                    {
                        status = result.UpdateEmpty(V);
                        if (iteratorKind == IteratorKind.Async)
                            throw new NotImplementedException("async");
                        var oldCurrent = iteratorRecord.Current;
                        iteratorRecord.Dispose();
                        if (oldCurrent.value != iteratorRecord.Current.value)
                            return iteratorRecord.Current;
                        return status;
                    }
                }
                if (result.value != null)
                    V = result.value;
            }
        }
    }

    public class ForBinding : IForInOfInitializer, IScopedDeclaration
    {
        public readonly string name;

        public ForBinding(string name)
        {
            this.name = name;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveBinding(name);
        }
    }

    public class DoWhileIterationStatement : IterationStatement
    {
        public readonly Statement doStatement;
        public readonly IExpression whileExpression;

        public DoWhileIterationStatement(Statement doStatement, IExpression whileExpression)
        {
            this.doStatement = doStatement;
            this.whileExpression = whileExpression;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            IValue V = UndefinedValue.Instance;
            while (true)
            {
                var stmtResult = doStatement.Evaluate(interpreter);
                if (!LoopContinues(stmtResult, labels))
                    return stmtResult.UpdateEmpty(V);
                if (stmtResult.value != null)
                    V = stmtResult.value;
                var conditionComp = whileExpression.Evaluate(interpreter);
                var condition = conditionComp.GetValue();
                if (condition.IsAbrupt()) return condition;
                if (!condition.value.ToBoolean().boolean) return Completion.NormalCompletion(V);
            }
        }
    }

    public class WhileIterationStatement : IterationStatement
    {
        public readonly IExpression whileExpression;
        public readonly Statement doStatement;

        public WhileIterationStatement(IExpression whileExpression, Statement doStatement)
        {
            this.doStatement = doStatement;
            this.whileExpression = whileExpression;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            IValue V = UndefinedValue.Instance;
            while (true)
            {
                var conditionComp = whileExpression.Evaluate(interpreter);
                var condition = conditionComp.GetValue();
                if (condition.IsAbrupt()) return condition;
                if (!condition.value.ToBoolean().boolean) return Completion.NormalCompletion(V);

                var stmtResult = doStatement.Evaluate(interpreter);
                if (!LoopContinues(stmtResult, labels))
                    return stmtResult.UpdateEmpty(V);
                if (stmtResult.value != null)
                    V = stmtResult.value;
            }
        }
    }

    public class ForExpressionIterationStatement : IterationStatement
    {
        public readonly Statement doStatement;
        public readonly IExpression forExpression;
        public readonly IExpression conditionExpression;
        public readonly IExpression endExpression;

        public ForExpressionIterationStatement(IExpression forExpression, IExpression conditionExpression, IExpression endExpression, Statement doStatement)
        {
            this.doStatement = doStatement;
            this.forExpression = forExpression;
            this.conditionExpression = conditionExpression;
            this.endExpression = endExpression;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            if (forExpression != null)
            {
                var forExprComp = forExpression.Evaluate(interpreter).GetValue();
                if (forExprComp.IsAbrupt()) return forExprComp;
            }
            return ForBodyEvaluation(conditionExpression, endExpression, doStatement, Utils.EmptyList<string>(), labels);
        }
    }

    public class ForVarIterationStatement : IterationStatement
    {
        public readonly Statement doStatement;
        public readonly VariableDeclarationList variableDeclarations;
        public readonly IExpression conditionExpression;
        public readonly IExpression endExpression;

        public ForVarIterationStatement(VariableDeclarationList variableDeclarations, IExpression conditionExpression, IExpression endExpression, Statement doStatement)
        {
            this.doStatement = doStatement;
            this.variableDeclarations = variableDeclarations;
            this.conditionExpression = conditionExpression;
            this.endExpression = endExpression;
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
            return variableDeclarations.Select(v => v.name).Concat(doStatement.VarDeclaredNames()).ToList();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return variableDeclarations.VarScopedDeclarations().Concat(doStatement.VarScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var varDecls = variableDeclarations.Evaluate(interpreter);
            if (varDecls.IsAbrupt()) return varDecls;
            return ForBodyEvaluation(conditionExpression, endExpression, doStatement, Utils.EmptyList<string>(), labels);
        }
    }

    public class ForLexicalIterationStatement : IterationStatement
    {
        public readonly Statement doStatement;
        public readonly LexicalDeclaration lexicalDeclaration;
        public readonly IExpression leftExpression;
        public readonly IExpression rightExpression;

        public ForLexicalIterationStatement(LexicalDeclaration lexicalDeclaration, IExpression leftExpression, IExpression rightExpression, Statement doStatement)
        {
            this.doStatement = doStatement;
            this.lexicalDeclaration = lexicalDeclaration;
            this.leftExpression = leftExpression;
            this.rightExpression = rightExpression;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var oldEnv = interpreter.RunningExecutionContext().LexicalEnvironment;
            var loopEnv = oldEnv.NewDeclarativeEnvironment();
            var loopEnvRec = loopEnv.EnvironmentRecord;
            var isConst = lexicalDeclaration.IsConstantDeclaration();
            var boundNames = lexicalDeclaration.BoundNames();
            foreach (var dn in boundNames)
            {
                if (isConst)
                    loopEnvRec.CreateImmutableBinding(dn.name, true);
                else
                    loopEnvRec.CreateMutableBinding(dn.name, false);
            }
            interpreter.RunningExecutionContext().LexicalEnvironment = loopEnv;
            var forDecl = lexicalDeclaration.Evaluate(interpreter);
            if (forDecl.IsAbrupt())
            {
                interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
                return forDecl;
            }
            var perIterationLets = isConst ? Utils.EmptyList<string>() : boundNames.Select(b => b.name);
            var bodyResult = ForBodyEvaluation(leftExpression, rightExpression, doStatement, perIterationLets, labels);
            interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
            return bodyResult;
        }
    }

    public class ForInLHSIterationStatement : IterationStatement
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly IExpression inExpression;
        public readonly Statement doStatement;

        public ForInLHSIterationStatement(ILeftHandSideExpression leftHandSideExpression, IExpression inExpression, Statement doStatement)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.inExpression = inExpression;
            this.doStatement = doStatement;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), inExpression, IterationKind.Enumerate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(leftHandSideExpression, doStatement, keyResult.Other, IterationKind.Enumerate, LHSKind.Assignment, labels);
        }
    }

    public class ForInVarIterationStatement : IterationStatement
    {
        public readonly ForBinding forVar;
        public readonly IExpression forInExpression;
        public readonly Statement doStatement;

        public ForInVarIterationStatement(ForBinding forVar, IExpression forInExpression, Statement doStatement)
        {
            this.forVar = forVar;
            this.forInExpression = forInExpression;
            this.doStatement = doStatement;
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
            return new[] { forVar.name }.Concat(doStatement.VarDeclaredNames()).ToList();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return new List<IScopedDeclaration> { forVar }.Concat(doStatement.VarScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), forInExpression, IterationKind.Enumerate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(forVar, doStatement, keyResult.Other, IterationKind.Enumerate, LHSKind.VarBinding, labels);
        }
    }

    public class ForInLetConstIterationStatement : IterationStatement
    {
        public readonly ForDeclaration forDeclaration;
        public readonly IExpression forInExpression;
        public readonly Statement doStatement;

        public ForInLetConstIterationStatement(ForDeclaration forDeclaration, IExpression forInExpression, Statement doStatement)
        {
            this.forDeclaration = forDeclaration;
            this.forInExpression = forInExpression;
            this.doStatement = doStatement;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), forInExpression, IterationKind.Enumerate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(forDeclaration, doStatement, keyResult.Other, IterationKind.Enumerate, LHSKind.LexicalBinding, labels);
        }
    }

    public class ForOfLHSIterationStatement : IterationStatement
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly IAssignmentExpression ofExpression;
        public readonly Statement doStatement;

        public ForOfLHSIterationStatement(ILeftHandSideExpression leftHandSideExpression, IAssignmentExpression ofExpression, Statement doStatement)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.ofExpression = ofExpression;
            this.doStatement = doStatement;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), ofExpression, IterationKind.Iterate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(leftHandSideExpression, doStatement, keyResult.Other, IterationKind.Iterate, LHSKind.Assignment, labels);
        }
    }

    public class ForOfVarIterationStatement : IterationStatement
    {
        public readonly ForBinding forVar;
        public readonly IAssignmentExpression forOfExpression;
        public readonly Statement doStatement;

        public ForOfVarIterationStatement(ForBinding forVar, IAssignmentExpression forOfExpression, Statement doStatement)
        {
            this.forVar = forVar;
            this.forOfExpression = forOfExpression;
            this.doStatement = doStatement;
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
            return new[] { forVar.name }.Concat(doStatement.VarDeclaredNames()).ToList();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return new List<IScopedDeclaration> { forVar }.Concat(doStatement.VarScopedDeclarations()).ToList();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), forOfExpression, IterationKind.Iterate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(forVar, doStatement, keyResult.Other, IterationKind.Iterate, LHSKind.VarBinding, labels);
        }
    }

    public class ForOfLetConstIterationStatement : IterationStatement
    {
        public readonly ForDeclaration forDeclaration;
        public readonly IAssignmentExpression forOfExpression;
        public readonly Statement doStatement;

        public ForOfLetConstIterationStatement(ForDeclaration forDeclaration, IAssignmentExpression forOfExpression, Statement doStatement)
        {
            this.forDeclaration = forDeclaration;
            this.forOfExpression = forOfExpression;
            this.doStatement = doStatement;
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
            return doStatement.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return doStatement.VarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var keyResult = ForInOfHeadEvaluation(Utils.EmptyList<string>(), forOfExpression, IterationKind.Iterate);
            if (keyResult.IsAbrupt()) return keyResult;
            return ForInOfBodyEvaluation(forDeclaration, doStatement, keyResult.Other, IterationKind.Iterate, LHSKind.LexicalBinding, labels);
        }
    }
}
