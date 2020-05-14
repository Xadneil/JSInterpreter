using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public class Script : Statement
    {
        public readonly ScriptStatementList scriptBody;

        public Script(ScriptStatementList scriptBody)
        {
            this.scriptBody = scriptBody;
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return scriptBody.TopLevelLexicallyScopedDeclarations();
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

        public Completion ScriptEvaluate(Interpreter interpreter)
        {
            //TODO get realm from script record
            var globalEnv = interpreter.CurrentRealm().GlobalEnv;
            var scriptCxt = new ExecutionContext(interpreter.CurrentRealm())
            {
                VariableEnvironment = globalEnv,
                LexicalEnvironment = globalEnv
            };
            interpreter.PushExecutionStack(scriptCxt);
            var result = GlobalDeclarationInstantiation(globalEnv);
            if (result.completionType == CompletionType.Normal)
                result = scriptBody.Evaluate(interpreter);
            if (result.completionType == CompletionType.Normal && result.value == null)
                result = Completion.NormalCompletion(UndefinedValue.Instance);
            interpreter.PopExecutionStack(scriptCxt);
            return result;
        }

        private Completion GlobalDeclarationInstantiation(LexicalEnvironment env)
        {
            var envRecAbstract = env.EnvironmentRecord;
            if (!(envRecAbstract is GlobalEnvironmentRecord envRec))
                throw new InvalidOperationException("Spec 15.1.11 step 2");
            var lexNames = scriptBody.LexicallyDeclaredNames();
            var varNames = scriptBody.VarDeclaredNames();
            foreach (var name in lexNames)
            {
                if (envRec.HasVarDeclaration(name))
                    return Completion.ThrowSyntaxError($"variable {name} is already declared");
                if (envRec.HasLexicalDeclaration(name))
                    return Completion.ThrowSyntaxError($"variable {name} is already declared");
                var hasRestrictedGlobal = envRec.HasRestrictedGlobalProperty(name);
                if (hasRestrictedGlobal.IsAbrupt()) return hasRestrictedGlobal;
                if (hasRestrictedGlobal.Other == true)
                    return Completion.ThrowSyntaxError($"variable {name} is already a restricted global");
            }
            foreach (var name in varNames)
            {
                if (envRec.HasLexicalDeclaration(name))
                    return Completion.ThrowSyntaxError($"variable {name} is already declared");
            }
            var varDeclarations = scriptBody.VarScopedDeclarations();
            var functionsToInitialize = new List<FunctionDeclaration>();
            var declaredFunctionNames = new List<string>();
            foreach (var d in varDeclarations.Reverse())
            {
                if (d is FunctionDeclaration f)
                {
                    var fn = f.BoundNames()[0];
                    if (!declaredFunctionNames.Contains(fn))
                    {
                        var fnDefinable = envRec.CanDeclareGlobalFunction(fn);
                        if (fnDefinable.IsAbrupt()) return fnDefinable;
                        if (!fnDefinable.Other)
                            return Completion.ThrowTypeError($"function {fn} cannot be declared.");
                        declaredFunctionNames.Add(fn);
                        functionsToInitialize.Insert(0, f);
                    }
                }
            }
            var declaredVarNames = new List<string>();
            foreach (var d in varDeclarations)
            {
                string vn;
                if (d is VariableDeclaration v)
                    vn = v.name;
                else if (d is ForBinding f)
                    vn = f.name;
                else if (d is Identifier i)
                    vn = i.name;
                else
                    continue;
                if (!declaredVarNames.Contains(vn))
                {
                    var vnDefinable = envRec.CanDeclareGlobalVar(vn);
                    if (vnDefinable.IsAbrupt()) return vnDefinable;
                    if (!vnDefinable.Other)
                        return Completion.ThrowTypeError($"variable {vn} cannot be declared.");
                    declaredVarNames.Add(vn);
                }
            }
            //TODO Annex B.3.3.2
            var lexDeclarations = scriptBody.LexicallyScopedDeclarations();
            foreach (var d in lexDeclarations)
            {
                foreach (var dn in d.BoundNames())
                {
                    Completion comp;
                    if (d.IsConstantDeclaration())
                        comp = envRec.CreateImmutableBinding(dn, true);
                    else
                        comp = envRec.CreateMutableBinding(dn, false);
                    if (comp.IsAbrupt()) return comp;
                }
            }
            foreach (var f in functionsToInitialize)
            {
                var fn = f.BoundNames()[0];
                var fo = f.InstantiateFunctionObject(env);
                var comp = envRec.CreateGlobalFunctionBinding(fn, fo, false);
                if (comp.IsAbrupt()) return comp;
            }
            foreach (var vn in declaredVarNames)
            {
                var comp = envRec.CreateGlobalVarBinding(vn, false);
                if (comp.IsAbrupt()) return comp;
            }
            return Completion.NormalCompletion();
        }
    }
}
