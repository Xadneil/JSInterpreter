using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public class SwitchStatement : BreakableStatement
    {
        public readonly IExpression switchExpression;
        public readonly CaseBlock caseBlock;


        public SwitchStatement(IExpression switchExpression, CaseBlock caseBlock)
        {
            this.switchExpression = switchExpression;
            this.caseBlock = caseBlock;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return caseBlock.LexicallyScopedDeclarations();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return caseBlock.VarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return caseBlock.VarScopedDeclarations();
        }

        public override Completion LabelledEvaluate(Interpreter interpreter, List<string> labels)
        {
            var stmtResult = Evaluate(interpreter);
            if (stmtResult.completionType == CompletionType.Break && stmtResult.target == null)
            {
                if (stmtResult.value == null)
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                stmtResult = Completion.NormalCompletion(stmtResult.value);
            }
            return stmtResult;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var switchComp = switchExpression.Evaluate(interpreter).GetValue();
            if (switchComp.IsAbrupt()) return switchComp;
            var switchValue = switchComp.value;
            var oldEnv = interpreter.RunningExecutionContext().LexicalEnvironment;
            var blockEnv = oldEnv.NewDeclarativeEnvironment();
            Block.BlockDeclarationInstantiation(caseBlock, blockEnv);
            interpreter.RunningExecutionContext().LexicalEnvironment = blockEnv;
            var R = caseBlock.CaseBlockEvaluation(switchValue);
            interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
            return R;
        }
    }

    public class CaseBlock : IHasLexicallyScopedDeclarations
    {
        public readonly IReadOnlyList<CaseClause> firstCaseClauses;
        public readonly DefaultClause defaultClause;
        public readonly IReadOnlyList<CaseClause> secondCaseClauses;

        public CaseBlock(IReadOnlyList<CaseClause> caseClauses)
        {
            firstCaseClauses = caseClauses;
        }

        public CaseBlock(IReadOnlyList<CaseClause> firstCaseClauses, DefaultClause defaultClause, IReadOnlyList<CaseClause> secondCaseClauses)
        {
            this.firstCaseClauses = firstCaseClauses;
            this.defaultClause = defaultClause;
            this.secondCaseClauses = secondCaseClauses;
        }

        public IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            if (defaultClause != null)
            {
                return firstCaseClauses.SelectMany(c => c.LexicallyScopedDeclarations()).Concat(defaultClause.LexicallyScopedDeclarations()).Concat(secondCaseClauses.SelectMany(c => c.LexicallyScopedDeclarations())).ToList();
            }
            else
            {
                return firstCaseClauses.SelectMany(c => c.LexicallyScopedDeclarations()).ToList();
            }
        }

        public IReadOnlyList<string> VarDeclaredNames()
        {
            var caseClauseVars = firstCaseClauses.SelectMany(c => c.VarDeclaredNames());
            if (defaultClause != null)
            {
                caseClauseVars = caseClauseVars.Concat(defaultClause.VarDeclaredNames());
            }
            caseClauseVars = caseClauseVars.Concat(secondCaseClauses.SelectMany(c => c.VarDeclaredNames()));
            return caseClauseVars.ToList();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            var declarations = firstCaseClauses.SelectMany(c => c.VarScopedDeclarations());
            if (defaultClause != null)
            {
                declarations = declarations.Concat(defaultClause.VarScopedDeclarations());
            }
            declarations = declarations.Concat(secondCaseClauses.SelectMany(c => c.VarScopedDeclarations()));
            return declarations.ToList();
        }

        public Completion CaseBlockEvaluation(IValue input)
        {
            Completion R;
            bool found;
            IValue V;
            if (!firstCaseClauses.Any() && defaultClause == null)
                return Completion.NormalCompletion(UndefinedValue.Instance);
            if (defaultClause == null)
            {
                V = UndefinedValue.Instance;
                found = false;
                foreach (var C in firstCaseClauses)
                {
                    if (!found)
                    {
                        var foundComp = CaseClauseIsSelected(C, input);
                        if (foundComp.IsAbrupt()) return foundComp;
                        found = foundComp.Other;
                    }
                    if (found)
                    {
                        R = C.Evaluate(Interpreter.Instance());
                        if (R.value != null) V = R.value;
                        if (R.IsAbrupt()) return R.UpdateEmpty(V);
                    }
                }
                return Completion.NormalCompletion(V);
            }
            V = UndefinedValue.Instance;
            found = false;
            foreach (var C in firstCaseClauses)
            {
                if (!found)
                {
                    var foundComp = CaseClauseIsSelected(C, input);
                    if (foundComp.IsAbrupt()) return foundComp;
                    found = foundComp.Other;
                }
                if (found)
                {
                    R = C.Evaluate(Interpreter.Instance());
                    if (R.value != null) V = R.value;
                    if (R.IsAbrupt()) return R.UpdateEmpty(V);
                }
            }
            var foundInB = false;
            if (!found)
            {
                foreach (var C in secondCaseClauses)
                {
                    if (!foundInB)
                    {
                        var foundComp = CaseClauseIsSelected(C, input);
                        if (foundComp.IsAbrupt()) return foundComp;
                        found = foundComp.Other;
                    }
                    if (foundInB)
                    {
                        R = C.Evaluate(Interpreter.Instance());
                        if (R.value != null) V = R.value;
                        if (R.IsAbrupt()) return R.UpdateEmpty(V);
                    }
                }
            }
            if (foundInB)
                return Completion.NormalCompletion(V);
            R = defaultClause.Evaluate(Interpreter.Instance());
            if (R.value != null)
                V = R.value;
            if (R.IsAbrupt()) return R.UpdateEmpty(V);
            foreach (var C in secondCaseClauses)
            {
                R = C.Evaluate(Interpreter.Instance());
                if (R.value != null) V = R.value;
                if (R.IsAbrupt()) return R.UpdateEmpty(V);
            }
            return Completion.NormalCompletion(V);
        }

        private static BooleanCompletion CaseClauseIsSelected(CaseClause c, IValue input)
        {
            var selector = c.Evaluate(Interpreter.Instance()).GetValue();
            if (selector.IsAbrupt()) return selector.WithEmptyBool();
            return EqualityExpression.StrictAbstractEquality(selector.value, input);
        }
    }

    public class CaseClause
    {
        public readonly IExpression matchExpression;
        public readonly StatementList statementList;

        public CaseClause(IExpression matchExpression, StatementList statementList)
        {
            this.matchExpression = matchExpression;
            this.statementList = statementList;
        }

        public IReadOnlyList<string> VarDeclaredNames()
        {
            return statementList.VarDeclaredNames();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return statementList.VarScopedDeclarations();
        }

        public IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return statementList.LexicallyScopedDeclarations();
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (statementList == null)
                return Completion.NormalCompletion();
            return statementList.Evaluate(interpreter);
        }
    }

    public class DefaultClause
    {
        public readonly StatementList statementList;

        public DefaultClause(StatementList statementList)
        {
            this.statementList = statementList;
        }

        public IReadOnlyList<string> VarDeclaredNames()
        {
            return statementList.VarDeclaredNames();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return statementList.VarScopedDeclarations();
        }

        public IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return statementList.LexicallyScopedDeclarations();
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (statementList == null)
                return Completion.NormalCompletion();
            return statementList.Evaluate(interpreter);
        }
    }
}
