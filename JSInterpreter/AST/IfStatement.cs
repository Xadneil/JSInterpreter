using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public sealed class IfStatement : Statement
    {
        public readonly bool hasElse;
        public readonly AbstractExpression conditionExpression;
        public readonly Statement trueStatement;
        public readonly Statement? falseStatement;

        public IfStatement(AbstractExpression conditionExpression, Statement trueStatement, bool isStrictMode) : base(isStrictMode)
        {
            hasElse = false;
            this.conditionExpression = conditionExpression;
            this.trueStatement = trueStatement;
        }

        public IfStatement(AbstractExpression conditionExpression, Statement trueStatement, Statement falseStatement, bool isStrictMode) : base(isStrictMode)
        {
            hasElse = true;
            this.conditionExpression = conditionExpression;
            this.trueStatement = trueStatement;
            this.falseStatement = falseStatement;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (hasElse)
            {
                var conditionComp = conditionExpression.Evaluate(interpreter).GetValue();
                if (conditionComp.IsAbrupt()) return conditionComp;
                var condition = conditionComp.value!.ToBoolean();
                Completion stmtCompletion;
                if (condition.boolean)
                {
                    stmtCompletion = trueStatement.Evaluate(interpreter);
                }
                else
                {
                    stmtCompletion = falseStatement!.Evaluate(interpreter);
                }
                return stmtCompletion.UpdateEmpty(UndefinedValue.Instance);
            }
            else
            {
                var conditionComp = conditionExpression.Evaluate(interpreter).GetValue();
                if (conditionComp.IsAbrupt()) return conditionComp;
                var condition = conditionComp.value!.ToBoolean();
                if (!condition.boolean)
                {
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                }
                else
                {
                    var stmtCompletion = trueStatement.Evaluate(interpreter);
                    return stmtCompletion.UpdateEmpty(UndefinedValue.Instance);
                }
            }
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            if (hasElse)
                return trueStatement.VarDeclaredNames().Concat(falseStatement!.VarDeclaredNames()).ToList();
            return trueStatement.VarDeclaredNames();

        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            if (hasElse)
                return trueStatement.VarScopedDeclarations().Concat(falseStatement!.VarScopedDeclarations()).ToList();
            return trueStatement.VarScopedDeclarations();
        }
    }
}
