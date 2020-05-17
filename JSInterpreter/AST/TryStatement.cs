using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public enum TryStatementType
    {
        TryCatch,
        TryFinally,
        TryCatchFinally
    }

    public class TryStatement : Statement
    {
        public readonly TryStatementType tryStatementType;
        public readonly Block tryBlock;
        public readonly Block? catchBlock;
        public readonly Block? finallyBlock;
        public bool hasCatchParameter;
        public readonly Identifier? catchParameter;

        private TryStatement(TryStatementType tryStatementType, Block tryBlock, bool isStrictMode, Block? catchBlock, Block? finallyBlock, Identifier? catchParameter = null) : base(isStrictMode)
        {
            this.tryStatementType = tryStatementType;
            this.tryBlock = tryBlock;
            this.catchBlock = catchBlock;
            this.finallyBlock = finallyBlock;
            this.catchParameter = catchParameter;
            hasCatchParameter = catchParameter != null;
        }

        public static TryStatement TryCatch(Block tryBlock, Block catchBlock, bool isStrictMode)
        {
            return new TryStatement(TryStatementType.TryCatch, tryBlock, isStrictMode, catchBlock, null);
        }

        public static TryStatement TryCatch(Block tryBlock, Identifier catchParameter, Block catchBlock, bool isStrictMode)
        {
            return new TryStatement(TryStatementType.TryCatch, tryBlock, isStrictMode, catchBlock, null, catchParameter);
        }

        public static TryStatement TryFinally(Block tryBlock, Block finallyBlock, bool isStrictMode)
        {
            return new TryStatement(TryStatementType.TryFinally, tryBlock, isStrictMode, null, finallyBlock);
        }

        public static TryStatement TryCatchFinally(Block tryBlock, Block catchBlock, Block finallyBlock, bool isStrictMode)
        {
            return new TryStatement(TryStatementType.TryCatchFinally, tryBlock, isStrictMode, catchBlock, finallyBlock);
        }

        public static TryStatement TryCatchFinally(Block tryBlock, Identifier catchParameter, Block catchBlock, Block finallyBlock, bool isStrictMode)
        {
            return new TryStatement(TryStatementType.TryCatchFinally, tryBlock, isStrictMode, catchBlock, finallyBlock, catchParameter);
        }

        public override IReadOnlyList<string> VarDeclaredNames()
        {
            switch (tryStatementType)
            {
                case TryStatementType.TryCatch:
                    return tryBlock.VarDeclaredNames().Concat(catchBlock!.VarDeclaredNames()).ToList();
                case TryStatementType.TryFinally:
                    return tryBlock.VarDeclaredNames().Concat(finallyBlock!.VarDeclaredNames()).ToList();
                case TryStatementType.TryCatchFinally:
                    return tryBlock.VarDeclaredNames().Concat(catchBlock!.VarDeclaredNames()).Concat(finallyBlock!.VarDeclaredNames()).ToList();
                default:
                    throw new InvalidOperationException($"TryStatement: tryStatementType is invalid enum with value {(int)tryStatementType}");
            }
        }

        public override IReadOnlyList<IScopedDeclaration> VarScopedDeclarations()
        {
            switch (tryStatementType)
            {
                case TryStatementType.TryCatch:
                    return tryBlock.VarScopedDeclarations().Concat(catchBlock!.VarScopedDeclarations()).ToList();
                case TryStatementType.TryFinally:
                    return tryBlock.VarScopedDeclarations().Concat(finallyBlock!.VarScopedDeclarations()).ToList();
                case TryStatementType.TryCatchFinally:
                    return tryBlock.VarScopedDeclarations().Concat(catchBlock!.VarScopedDeclarations()).Concat(finallyBlock!.VarScopedDeclarations()).ToList();
                default:
                    throw new InvalidOperationException($"TryStatement: tryStatementType is invalid enum with value {(int)tryStatementType}");
            }
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return Utils.EmptyList<IDeclarationPart>();
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (catchBlock != null && finallyBlock == null)
            {
                var B = tryBlock.Evaluate(interpreter);
                Completion C;
                if (B.completionType == CompletionType.Throw)
                    C = CatchClauseEvaluate(interpreter, B.value!);
                else
                    C = B;
                return C.UpdateEmpty(UndefinedValue.Instance);
            }
            else if (catchBlock == null && finallyBlock != null)
            {
                var B = tryBlock.Evaluate(interpreter);
                var F = finallyBlock.Evaluate(interpreter);
                if (F.completionType == CompletionType.Normal)
                    F = B;
                return F.UpdateEmpty(UndefinedValue.Instance);
            }
            else // both catch an finally must be present
            {
                var B = tryBlock.Evaluate(interpreter);
                Completion C;
                if (B.completionType == CompletionType.Throw)
                    C = CatchClauseEvaluate(interpreter, B.value!);
                else
                    C = B;
                var F = finallyBlock!.Evaluate(interpreter);
                if (F.completionType == CompletionType.Normal)
                    F = C;
                return F.UpdateEmpty(UndefinedValue.Instance);
            }
        }

        private Completion CatchClauseEvaluate(Interpreter interpreter, IValue thrownValue)
        {
            if (!hasCatchParameter)
            {
                return catchBlock!.Evaluate(interpreter);
            }
            var oldEnv = interpreter.RunningExecutionContext().LexicalEnvironment;
            var catchEnv = oldEnv.NewDeclarativeEnvironment();
            var catchEnvRec = catchEnv.EnvironmentRecord;
            foreach (var argName in catchParameter!.BoundNames())
            {
                catchEnvRec.CreateMutableBinding(argName, false);
            }
            interpreter.RunningExecutionContext().LexicalEnvironment = catchEnv;
            var status = catchParameter.BindingInitialization(thrownValue, catchEnv);
            if (status.IsAbrupt())
            {
                interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
                return status;
            }
            var B = catchBlock!.Evaluate(interpreter);
            interpreter.RunningExecutionContext().LexicalEnvironment = oldEnv;
            return B;
        }
    }
}
