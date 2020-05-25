using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public enum FunctionKind
    {
        Normal,
        NonConstructor,
        ClassConstructor
    }
    public enum ConstructorKind
    {
        Base,
        Derived
    }
    public enum ThisMode
    {
        Lexical,
        Strict,
        Global
    }
    public class FunctionObject : Constructor
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public LexicalEnvironment Environment { get; private set; }
        public FormalParameters FormalParameters { get; private set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public FunctionKind FunctionKind { get; private set; }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public FunctionStatementList Code { get; private set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ConstructorKind ConstructorKind { get; private set; }
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public Realm Realm { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ThisMode ThisMode { get; private set; }
        public bool Strict { get; private set; }
        public IValue? HomeObject { get; internal set; }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            var callerContext = Interpreter.Instance().RunningExecutionContext();
            IValue? thisArgument = null;
            if (ConstructorKind == ConstructorKind.Base)
            {
                var thisComp = Utils.OrdinaryCreateFromConstructor(newTarget, i => i.ObjectPrototype);
                if (thisComp.IsAbrupt()) return thisComp;
                thisArgument = thisComp.value;
            }
            ExecutionContext calleeContext = PrepareForOrdinaryCall(newTarget);
            if (calleeContext != Interpreter.Instance().RunningExecutionContext())
                throw new InvalidOperationException("FunctionObject.PrepareForOrdinaryCall did not perform as expected.");
            if (ConstructorKind == ConstructorKind.Base)
                OrdinaryCallBindThis(calleeContext, thisArgument);
            var constructorEnv = calleeContext.LexicalEnvironment;
            var envRec = (constructorEnv.EnvironmentRecord as FunctionEnvironmentRecord)!;
            var result = OrdinaryCallEvaluateBody(arguments);
            Interpreter.Instance().PopExecutionStack(calleeContext);
            if (callerContext != Interpreter.Instance().RunningExecutionContext())
                throw new InvalidOperationException("Interpreter.PopExecutionStack did not perform as expected.");
            if (result.completionType == CompletionType.Return)
            {
                if (result.value is Object)
                    return Completion.NormalCompletion(result.value);
                if (ConstructorKind == ConstructorKind.Base)
                    return Completion.NormalCompletion(thisArgument);
                if (!(result.value is UndefinedValue))
                    throw new InvalidCastException("FunctionObject.InternalConstruct: Spec 9.2.2 step 13c");
            }
            else if (result.IsAbrupt()) return result;
            return envRec.GetThisBinding();
        }

        public override Completion InternalCall(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (FunctionKind == FunctionKind.ClassConstructor)
                throw new InvalidOperationException("FunctionObject.Call: Spec 9.2.1 step 2");
            var callerContext = Interpreter.Instance().RunningExecutionContext();
            var calleeContext = PrepareForOrdinaryCall(UndefinedValue.Instance);
            if (Interpreter.Instance().RunningExecutionContext() != calleeContext)
                throw new InvalidOperationException("FunctionObject.Call: FunctionObject.PrepareForOrdinaryCall did not perform as expected.");
            OrdinaryCallBindThis(calleeContext, @this);
            var result = OrdinaryCallEvaluateBody(arguments);
            Interpreter.Instance().PopExecutionStack(calleeContext);
            if (callerContext != Interpreter.Instance().RunningExecutionContext())
                throw new InvalidOperationException("Interpreter.PopExecutionStack did not perform as expected.");
            if (result.completionType == CompletionType.Return)
                return Completion.NormalCompletion(result.value);
            if (result.IsAbrupt()) return result;
            return Completion.NormalCompletion(UndefinedValue.Instance);
        }

        private ExecutionContext PrepareForOrdinaryCall(IValue? newTarget)
        {
            if (!(newTarget is UndefinedValue) && !(newTarget is Object))
                throw new InvalidOperationException("Spec 9.2.1.1 Step 1");
            var callerContext = Interpreter.Instance().RunningExecutionContext();
            var calleeContext = new ExecutionContext(Realm);
            //TODO ScriptOrModule
            var localEnv = LexicalEnvironment.NewFunctionalEnvironment(this, newTarget);
            calleeContext.LexicalEnvironment = localEnv;
            calleeContext.VariableEnvironment = localEnv;
            //TODO suspend callerContext
            Interpreter.Instance().PushExecutionStack(calleeContext);
            return calleeContext;
        }

        private Completion OrdinaryCallBindThis(ExecutionContext calleeContext, IValue? thisArgument)
        {
            if (ThisMode == ThisMode.Lexical)
                return Completion.NormalCompletion(UndefinedValue.Instance);
            var calleeRealm = Realm;
            var localEnv = calleeContext.LexicalEnvironment;
            IValue thisValue;
            if (ThisMode == ThisMode.Strict)
                thisValue = thisArgument!;
            else
            {
                if (thisArgument == UndefinedValue.Instance || thisArgument == NullValue.Instance)
                {
                    var globalEnvRec = calleeRealm.GlobalEnv.EnvironmentRecord;
                    if (!(globalEnvRec is GlobalEnvironmentRecord globalEnvironmentRecord))
                        throw new InvalidOperationException("FunctionObject.OrdinaryCallBindThis: Realm.GlobalEnv.environmentRecord is not the global environment record");
                    thisValue = globalEnvironmentRecord.GlobalThisValue;
                }
                else
                {
                    thisValue = thisArgument!.ToObject().value!;
                }
            }
            var envRec = localEnv.EnvironmentRecord;
            if (!(envRec is FunctionEnvironmentRecord functionEnvironmentRecord))
                throw new InvalidOperationException("FunctionObject.OrdinaryCallBindThis: calleeContext.lexicalEnvironment.environmentRecord is not a function environment record");
            return functionEnvironmentRecord.BindThisValue(thisValue);
        }

        private Completion OrdinaryCallEvaluateBody(IReadOnlyList<IValue> arguments)
        {
            if (Code != null)
                return Code.EvaluateBody(this, arguments);
            return Completion.NormalCompletion();
        }

        public Completion FunctionDeclarationInstantiation(IReadOnlyList<IValue> arguments)
        {
            var calleeContext = Interpreter.Instance().RunningExecutionContext();
            var env = calleeContext.LexicalEnvironment;
            var envRec = env.EnvironmentRecord;
            var parameterNames = FormalParameters.BoundNames();
            var hasDuplicates = parameterNames.Distinct().Count() < parameterNames.Count;
            var simpleParameterList = FormalParameters.IsSimpleParameterList();
            var hasParameterExpressions = FormalParameters.formalParameters.Any(p => p.hasInitializer);
            var varNames = Code.VarDeclaredNames();
            var varDeclarations = Code.VarScopedDeclarations();
            var functionNames = new LinkedList<string>();
            var functionsToInitialize = new LinkedList<FunctionDeclaration>();
            foreach (var d in varDeclarations.Reverse())
            {
                if (d is FunctionDeclaration functionDeclaration)
                {
                    var fn = functionDeclaration.BoundNames()[0];
                    if (!functionNames.Contains(fn))
                    {
                        functionNames.AddFirst(fn);
                        functionsToInitialize.AddFirst(functionDeclaration);
                    }
                }
            }
            var argumentsObjectNeeded = true;
            if (ThisMode == ThisMode.Lexical)
            {
                argumentsObjectNeeded = false;
            }
            else if (parameterNames.Contains("arguments"))
            {
                argumentsObjectNeeded = false;
            }
            else if (!hasParameterExpressions)
            {
                if (functionNames.Contains("arguments"))
                {
                    argumentsObjectNeeded = false;
                }
            }
            foreach (var paramName in parameterNames)
            {
                var alreadyDeclared = envRec.HasBinding(paramName).Other == true;
                if (!alreadyDeclared)
                {
                    envRec.CreateMutableBinding(paramName, false);
                    if (hasDuplicates)
                        envRec.InitializeBinding(paramName, UndefinedValue.Instance);
                }
            }
            IReadOnlyList<string> parameterBindings;
            if (argumentsObjectNeeded)
            {
                Object ao;
                if (Strict || !simpleParameterList)
                {
                    ao = CreateUnmappedArgumentsObject(arguments);
                }
                else
                {
                    ao = CreateMappedArgumentsObject(FormalParameters, arguments, envRec);
                }
                if (Strict)
                {
                    envRec.CreateImmutableBinding("arguments", false);
                }
                else
                {
                    envRec.CreateMutableBinding("arguments", false);
                }
                envRec.InitializeBinding("arguments", ao);
                parameterBindings = parameterNames.Concat(new[] { "arguments" }).ToList();
            }
            else
            {
                parameterBindings = parameterNames;
            }
            var iterator = new ArgumentIterator(arguments);
            Completion comp;
            if (hasDuplicates)
                comp = FormalParameters.IteratorBindingInitialization(null, iterator);
            else
                comp = FormalParameters.IteratorBindingInitialization(env, iterator);
            if (comp.IsAbrupt()) return comp;

            LexicalEnvironment varEnv;
            EnvironmentRecord varEnvRec;
            if (!hasParameterExpressions)
            {
                var instantiatedVarNames = new List<string>(parameterNames);
                foreach (var n in varNames)
                {
                    if (!instantiatedVarNames.Contains(n))
                    {
                        instantiatedVarNames.Add(n);
                        envRec.CreateMutableBinding(n, false);
                        envRec.InitializeBinding(n, UndefinedValue.Instance);
                    }
                }
                varEnv = env;
                varEnvRec = envRec;
            }
            else
            {
                varEnv = env.NewDeclarativeEnvironment();
                varEnvRec = varEnv.EnvironmentRecord;
                calleeContext.VariableEnvironment = varEnv;
                var instantiatedVarNames = new List<string>();
                foreach (var n in varNames)
                {
                    if (!instantiatedVarNames.Contains(n))
                    {
                        instantiatedVarNames.Add(n);
                        varEnvRec.CreateMutableBinding(n, false);
                        IValue initialValue;
                        if (!parameterBindings.Contains(n) || functionNames.Contains(n))
                        {
                            initialValue = UndefinedValue.Instance;
                        }
                        else
                        {
                            initialValue = envRec.GetBindingValue(n, false).value!;
                        }
                        varEnvRec.InitializeBinding(n, initialValue);
                    }
                }
            }
            var lexEnv = !Strict ? varEnv.NewDeclarativeEnvironment() : varEnv;
            var lexEnvRec = lexEnv.EnvironmentRecord;
            calleeContext.LexicalEnvironment = lexEnv;
            var lexDeclarations = Code.LexicallyScopedDeclarations();
            foreach (var d in lexDeclarations)
            {
                foreach (var dn in d.BoundNames())
                {
                    if (d.IsConstantDeclaration())
                        lexEnvRec.CreateImmutableBinding(dn, true);
                    else
                        lexEnvRec.CreateMutableBinding(dn, false);
                }
            }
            foreach (var f in functionsToInitialize)
            {
                var fn = f.BoundNames()[0];
                FunctionObject fo = f.InstantiateFunctionObject(lexEnv);
                varEnvRec.SetMutableBinding(fn, fo, false);
            }

            return Completion.NormalCompletion();
        }

        private Object CreateMappedArgumentsObject(FormalParameters formalParameters, IReadOnlyList<IValue> arguments, EnvironmentRecord env)
        {
            var map = Utils.ObjectCreate(null);
            var obj = new MappedArguments(map);
            var parameterNames = formalParameters.BoundNames();
            int index;
            for (index = 0; index < arguments.Count; index++)
            {
                Utils.CreateDataProperty(obj, index.ToString(System.Globalization.CultureInfo.InvariantCulture), arguments[index]);
            }
            obj.DefinePropertyOrThrow("length", new PropertyDescriptor(new NumberValue(arguments.Count), true, false, true));
            var mappedNames = new List<string>();
            for (index = parameterNames.Count - 1; index >= 0; index--)
            {
                var name = parameterNames[index];
                if (!mappedNames.Contains(name))
                {
                    mappedNames.Add(name);
                    if (index < arguments.Count)
                    {
                        var g = MakeArgGetter(name, env);
                        var p = MakeArgSetter(name, env);
                        map.DefineOwnProperty(index.ToString(System.Globalization.CultureInfo.InvariantCulture), new PropertyDescriptor(p, g, false, true));
                    }
                }
            }
            DefinePropertyOrThrow("@@iterator", new PropertyDescriptor(Utils.CreateBuiltinFunction(ArrayPrototype.values), true, false, true));
            DefinePropertyOrThrow("callee", new PropertyDescriptor(this, true, false, true));
            return obj;
        }

        private static FunctionObject MakeArgGetter(string name, EnvironmentRecord env)
        {
            var getter = Utils.CreateBuiltinFunction((@this, arguments) =>
            {
                if (!(@this is GetterSetter thisObj))
                    return Completion.ThrowTypeError("Not allowed to call internal function on non getter/setter");
                return thisObj.Env.GetBindingValue(thisObj.Name, false);
            }, steps => new GetterSetter(steps, name, env));
            return getter;
        }

        private static FunctionObject MakeArgSetter(string name, EnvironmentRecord env)
        {
            var setter = Utils.CreateBuiltinFunction((@this, arguments) =>
            {
                if (!(@this is GetterSetter thisObj))
                    return Completion.ThrowTypeError("Not allowed to call internal function on non getter/setter");
                var value = arguments[0];
                return thisObj.Env.SetMutableBinding(thisObj.Name, value, false);
            }, steps => new GetterSetter(steps, name, env));
            return setter;
        }

        private class GetterSetter : BuiltinFunction
        {
            public string Name { get; set; }
            public EnvironmentRecord Env { get; set; }

            public GetterSetter(Func<IValue, IReadOnlyList<IValue>, Completion> steps, string name, EnvironmentRecord env) : base(steps)
            {
                Name = name;
                Env = env;
            }
        }

        private static Object CreateUnmappedArgumentsObject(IReadOnlyList<IValue> arguments)
        {
            var obj = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ObjectPrototype, () => new UnmappedArgumentsObject());
            obj.DefinePropertyOrThrow("length", new PropertyDescriptor(new NumberValue(arguments.Count), true, false, true));
            for (int i = 0; i < arguments.Count; i++)
            {
                Utils.CreateDataProperty(obj, i.ToString(System.Globalization.CultureInfo.InvariantCulture), arguments[i]);
            }
            return obj;
        }

        private class UnmappedArgumentsObject : Object
        {
            public IValue ParameterMap { get; set; } = UndefinedValue.Instance;
        }

        public enum FunctionCreateKind
        {
            Normal, Method, Arrow
        }

        public void MakeConstructor(bool writablePrototype = true, Object? prototype = null)
        {
            if (prototype == null)
            {
                prototype = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ObjectPrototype);
                prototype.DefinePropertyOrThrow("constructor", new PropertyDescriptor(this, writablePrototype, false, true));
            }
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, writablePrototype, false, false));
        }

        public Completion MakeMethod(Object homeObject)
        {
            HomeObject = homeObject;
            return Completion.NormalCompletion(UndefinedValue.Instance);
        }

        public bool SetFunctionName(string name, string? prefix = null)
        {
            //TODO: allow symbol for prefix, use brackets on symbol description
            return DefinePropertyOrThrow("name", new PropertyDescriptor(new StringValue(name + (prefix ?? "")), false, false, true)).Other;
        }

        public static FunctionObject FunctionCreate(FunctionCreateKind kind, FormalParameters parameters, FunctionStatementList body, LexicalEnvironment scope, bool strict, IValue? prototype = null)
        {
            if (prototype == null)
            {
                prototype = Interpreter.Instance().CurrentRealm().Intrinsics.FunctionPrototype;
            }
            var functionKind = kind == FunctionCreateKind.Normal ? FunctionKind.Normal : FunctionKind.NonConstructor;
            FunctionObject F = FunctionAllocate(prototype, strict, functionKind);
            return FunctionInitialize(F, kind, parameters, body, scope);
        }

        public static FunctionObject FunctionAllocate(IValue prototype, bool strict, FunctionKind functionKind)
        {
            if (!(prototype is Object))
                throw new InvalidOperationException("FunctionObject.FunctionAllocate: non-object prototype is not allowed.");
            bool needsConstruct = functionKind == FunctionKind.Normal;
            if (functionKind == FunctionKind.NonConstructor) functionKind = FunctionKind.Normal;
            var F = new FunctionObject();
            if (needsConstruct)
            {
                F.ConstructorKind = ConstructorKind.Base;
            }
            F.Strict = strict;
            F.FunctionKind = functionKind;
            F.SetPrototypeOf(prototype);
            F.Realm = Interpreter.Instance().RunningExecutionContext().Realm;
            return F;
        }

        public static FunctionObject FunctionInitialize(FunctionObject f, FunctionCreateKind kind, FormalParameters parameters, FunctionStatementList body, LexicalEnvironment scope)
        {
            var len = parameters.ExpectedArgumentCount();
            f.DefinePropertyOrThrow("length", new PropertyDescriptor(new NumberValue(len), false, false, true));
            f.Environment = scope;
            f.FormalParameters = parameters;
            f.Code = body;
            if (kind == FunctionCreateKind.Arrow) f.ThisMode = ThisMode.Lexical;
            else if (f.Strict) f.ThisMode = ThisMode.Strict;
            else f.ThisMode = ThisMode.Global;
            return f;
        }
    }
}
