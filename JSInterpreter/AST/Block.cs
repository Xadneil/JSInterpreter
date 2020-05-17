using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public sealed class Block : Statement
    {
        public readonly StatementList statementList;

        public Block(StatementList statementList, bool isStrictMode) : base(isStrictMode)
        {
            this.statementList = statementList;
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            return statementList.VarDeclaredNames();
        }

        public IReadOnlyList<string> TopLevelVarDeclaredNames()
        {
            return statementList.TopLevelVarDeclaredNames();
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            return statementList.VarScopedDeclarations();
        }

        public IReadOnlyList<IScopedDeclaration> TopLevelVarScopedDeclarations()
        {
            return statementList.TopLevelVarScopedDeclarations();
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return statementList.LexicallyScopedDeclarations();
        }

        public IReadOnlyList<IDeclarationPart> TopLevelLexicallyScopedDeclarations()
        {
            return statementList.TopLevelLexicallyScopedDeclarations();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (!statementList.Any())
                return Completion.NormalCompletion();
            var oldEnv = interpreter.RunningExecutionContext().LexicalEnvironment;
            var blockEnv = oldEnv.NewDeclarativeEnvironment();
            BlockDeclarationInstantiation(statementList, blockEnv);
            interpreter.RunningExecutionContext().LexicalEnvironment = blockEnv;
            Completion blockValue = statementList.Evaluate(interpreter);
            interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
            return blockValue;
        }

        public static void BlockDeclarationInstantiation(IHasLexicallyScopedDeclarations code, LexicalEnvironment env)
        {
            var envRec = env.EnvironmentRecord;
            if (!(envRec is DeclarativeEnvironmentRecord))
                throw new InvalidOperationException("Block.BlockDeclarationInstantiation: env must be declarative");
            var declarations = code.LexicallyScopedDeclarations();
            foreach (var d in declarations)
            {
                foreach (var dn in d.BoundNames())
                {
                    if (d.IsConstantDeclaration())
                        envRec.CreateImmutableBinding(dn, true);
                    else
                        envRec.CreateMutableBinding(dn, false);
                }
                if (d is FunctionDeclaration functionDeclaration)
                {
                    var fn = d.BoundNames()[0];
                    var fo = functionDeclaration.InstantiateFunctionObject(env);
                    envRec.InitializeBinding(fn, fo);
                }
            }
        }
    }
}
