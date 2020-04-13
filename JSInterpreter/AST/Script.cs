using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    class Script : Statement
    {
        public readonly StatementList scriptBody;

        public Script(StatementList scriptBody)
        {
            this.scriptBody = scriptBody;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return scriptBody.TopLevelLexicallyScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return scriptBody.TopLevelLexicallyScopedDeclarations();
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
            return scriptBody.TopLevelVarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return scriptBody.TopLevelVarScopedDeclarations();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (!scriptBody.Any())
                return Completion.NormalCompletion(UndefinedValue.Instance);
            throw new InvalidOperationException("Evaluate is not defined for scripts with bodies. Use ScriptEvaluate instead.");
        }
    }
}
