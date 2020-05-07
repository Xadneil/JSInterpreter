using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    static public class Utils
    {
        public static IReadOnlyList<T> EmptyList<T>()
        {
            return EmptyListClass<T>.Instance;
        }

        public static BooleanCompletion CreateDataProperty(IValue o, string p, IValue v)
        {
            if (!(o is Object O))
                throw new InvalidOperationException("Spec 7.3.4 step 1");
            return O.DefineOwnProperty(p, new PropertyDescriptor(v, true, true, true));
        }

        public static BooleanCompletion CreateDataPropertyOrThrow(Object O, string P, IValue V)
        {
            var success = CreateDataProperty(O, P, V);
            if (success.IsAbrupt()) return success;
            if (success.Other == false) return Completion.ThrowTypeError($"CreateDataPropertyOrThrow {P} failed").WithEmptyBool();
            return success;
        }

        public static Completion CopyDataProperties(Object target, IValue source, IReadOnlyList<string> excludedItems)
        {
            if (source is UndefinedValue || source is NullValue)
                return Completion.NormalCompletion(target);
            var from = source.ToObject().value as Object;
            IReadOnlyList<string> keys = from.OwnPropertyKeys();
            foreach (var nextKey in keys)
            {
                var excluded = excludedItems.Contains(nextKey);
                if (!excluded)
                {
                    var descComp = from.GetOwnProperty(nextKey);
                    if (descComp.IsAbrupt()) return descComp;
                    var desc = descComp.Other;
                    if (desc != null && desc.Enumerable.HasValue && desc.Enumerable.Value)
                    {
                        var propValue = from.Get(nextKey);
                        if (propValue.IsAbrupt()) return propValue;
                        CreateDataProperty(target, nextKey, propValue.value);
                    }
                }
            }
            return Completion.NormalCompletion(target);
        }

        public static Object ObjectCreate(IValue proto, IEnumerable<string> internalSlotsList = null)
        {
            var obj = new Object();
            if (proto is Object o)
                obj.prototype = o;
            if (internalSlotsList != null)
            {
                obj.AddCustomInternalSlots(internalSlotsList);
            }
            return obj;
        }

        public static CompletionOr<List<IValue>> EvaluateArgument(Interpreter interpreter, IArgumentItem a)
        {
            List<IValue> ret = new List<IValue>();
            Completion valueComp;
            IValue value;
            switch (a)
            {
                case SpreadElement spreadElement:
                    valueComp = spreadElement.assignmentExpression.Evaluate(interpreter).GetValue();
                    if (valueComp.IsAbrupt()) return valueComp.WithEmpty<List<IValue>>();
                    value = valueComp.value;
                    if (!(value is Object @object))
                        throw new InvalidOperationException($"NewMemberExpression: tried to create an argument list using a spread on a non-object");
                    var iteratorRecordComp = @object.GetIterator();
                    if (iteratorRecordComp.IsAbrupt()) return iteratorRecordComp.WithEmpty<List<IValue>>();
                    var iteratorRecord = iteratorRecordComp.Other;
                    while (true)
                    {
                        var next = iteratorRecord.IteratorStep();
                        if (next.IsAbrupt()) return next.WithEmpty<List<IValue>>();
                        if (next.value == BooleanValue.False)
                            break;
                        var nextArg = IteratorRecord.IteratorValue(next.value);
                        if (nextArg.IsAbrupt()) return nextArg.WithEmpty<List<IValue>>();
                        ret.Add(nextArg.value);
                    }
                    break;
                case IAssignmentExpression assignmentExpression:
                    valueComp = assignmentExpression.Evaluate(interpreter).GetValue();
                    if (valueComp.IsAbrupt()) return valueComp.WithEmpty<List<IValue>>();
                    value = valueComp.value;
                    ret.Add(value);
                    break;
                default:
                    throw new InvalidOperationException($"NewMemberExpression: unhandled IArgumentItem type {a.GetType()}");
            }
            return Completion.NormalWith(ret);
        }

        public static Completion EvaluateCall(IValue func, IValue @ref, Arguments arguments, bool tailCall)
        {
            IValue thisValue;
            if (@ref is ReferenceValue reference)
            {
                if (reference.IsPropertyReference())
                    thisValue = reference.GetThisValue();
                else
                {
                    if (!(reference.baseValue is EnvironmentRecord envRec))
                        throw new InvalidOperationException("Utils.EvaluateCall: reference.baseValue is not a recognized IReferenceable");
                    thisValue = envRec.WithBaseObject();
                }
            }
            else
                thisValue = UndefinedValue.Instance;

            var argList = arguments.ArgumentListEvaluation();
            if (argList.IsAbrupt()) return argList;

            if (!(func is Callable functionObject))
            {
                return Completion.ThrowTypeError("Utils.EvaluateCall: func must be a function object.");
            }
            if (tailCall)
            {
                throw new NotImplementedException("Utils.EvaluateCall: tail calls not implemented");
            }
            return functionObject.Call(thisValue, argList.Other);
        }

        internal static Completion OrdinaryCreateFromConstructor(Object constructor, Func<Intrinsics, Object> intrinsicDefaultProto, IEnumerable<string> internalSlotsList = null)
        {
            var protoComp = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);
            if (protoComp.IsAbrupt()) return protoComp;
            var proto = protoComp.value;
            return Completion.NormalCompletion(ObjectCreate(proto, internalSlotsList));
        }

        public static Completion GetPrototypeFromConstructor(Object constructor, Func<Intrinsics, Object> intrinsicDefaultProto)
        {
            if (!(constructor is Callable))
                throw new InvalidOperationException("GetPrototypeFromConstructor: constructor is not callable");
            var protoComp = constructor.InternalGet("prototype", constructor);
            if (protoComp.IsAbrupt()) return protoComp;
            var proto = protoComp.value;
            if (!(proto is Object))
            {
                Realm realm = GetFunctionRealm(constructor as Callable);
                proto = intrinsicDefaultProto(realm.Intrinsics);
            }
            return Completion.NormalCompletion(proto);
        }

        private static Realm GetFunctionRealm(Callable obj)
        {
            if (obj is FunctionObject o)
                return o.Realm;
            //TODO bound function exotic, proxy exotic
            return Interpreter.Instance().CurrentRealm();
        }

        internal static Completion IteratorBindingInitializationBindingRestIdentifier(Identifier restParameterIdentifier, LexicalEnvironment env, ArgumentIterator arguments)
        {
            var lhsComp = Interpreter.Instance().ResolveBinding(restParameterIdentifier.name, env);
            if (lhsComp.IsAbrupt()) return lhsComp;
            var lhs = lhsComp.value as ReferenceValue;
            var A = ArrayObject.ArrayCreate(0);
            int n = 0;
            for (; ; n++)
            {
                if (arguments.Done)
                {
                    if (env == null)
                        return lhs.PutValue(A);
                    return lhs.InitializeReferencedBinding(A);
                }
                var nextValue = arguments.Next();
                var status = Utils.CreateDataProperty(A, n.ToString(System.Globalization.CultureInfo.InvariantCulture), nextValue);
                if (!status.Other)
                    throw new InvalidOperationException("BindingRestElement IteratorBindingInitialization: assert step 4g");
            }
        }

        internal static Completion IteratorBindingInitializationSingleNameBinding(Identifier identifier, IAssignmentExpression initializer, LexicalEnvironment env, ArgumentIterator arguments)
        {
            var lhsComp = Interpreter.Instance().ResolveBinding(identifier.name, env);
            if (lhsComp.IsAbrupt()) return lhsComp;
            var lhs = lhsComp.value as ReferenceValue;
            IValue v;
            if (!arguments.Done)
                v = arguments.Next();
            else
                v = UndefinedValue.Instance;
            if (initializer != null && v == UndefinedValue.Instance)
            {
                if (initializer is FunctionExpression f && f.isAnonymous)
                {
                    var comp = f.NamedEvaluate(Interpreter.Instance(), identifier.name);
                    if (comp.IsAbrupt()) return comp;
                    v = comp.value;
                }
                else
                {
                    var comp = initializer.Evaluate(Interpreter.Instance()).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    v = comp.value;
                }
            }
            if (env == null)
                return lhs.PutValue(v);
            return lhs.InitializeReferencedBinding(v);
        }

        public static FunctionObject CreateBuiltinFunction(Func<IValue, IReadOnlyList<IValue>, Completion> steps, IEnumerable<string> internalSlotsList, Realm realm = null, Object prototype = null)
        {
            if (realm == null)
                realm = Interpreter.Instance().CurrentRealm();
            if (prototype == null)
                prototype = realm.Intrinsics.FunctionPrototype;
            var func = new BuiltinFunction(steps);
            func.AddCustomInternalSlots(internalSlotsList);
            func.Realm = realm;
            func.prototype = prototype;
            func.IsExtensible = true;
            //TODO ScriptOrModule
            return func;
        }

        private class BuiltinFunction : FunctionObject
        {
            private readonly Func<IValue, IReadOnlyList<IValue>, Completion> CallAction;

            public BuiltinFunction(Func<IValue, IReadOnlyList<IValue>, Completion> callAction)
            {
                CallAction = callAction;
            }

            public override Completion InternalCall(IValue @this, IReadOnlyList<IValue> arguments)
            {
                return CallAction(@this, arguments);
            }
        }

        public static Completion CheckArguments(IReadOnlyList<IValue> arguments, int requiredCount)
        {
            if (arguments.Count < requiredCount)
                return Completion.ThrowTypeError($"{requiredCount} arguments are required.");
            return Completion.NormalCompletion();
        }

        public static Completion CheckArguments<T>(IReadOnlyList<IValue> arguments) where T : IValue
        {
            if (arguments.Count < 1)
                return Completion.ThrowTypeError("1 argument is required.");
            if (!(arguments.ElementAt(0) is T))
                return Completion.ThrowTypeError($"Argument 1 must be a {nameof(T)}.");
            return Completion.NormalCompletion();
        }

        public static Completion CheckArguments<T1, T2, T3>(IReadOnlyList<IValue> arguments) where T1 : IValue where T2 : IValue where T3 : IValue
        {
            if (arguments.Count < 3)
                return Completion.ThrowTypeError("3 arguments are required.");
            if (!(arguments.ElementAt(0) is T1))
                return Completion.ThrowTypeError($"Argument 1 must be a {nameof(T1)}.");
            if (!(arguments.ElementAt(1) is T2))
                return Completion.ThrowTypeError($"Argument 2 must be a {nameof(T2)}.");
            if (!(arguments.ElementAt(2) is T3))
                return Completion.ThrowTypeError($"Argument 3 must be a {nameof(T3)}.");
            return Completion.NormalCompletion();
        }
    }

    public static class EmptyListClass<T>
    {
        public static readonly IReadOnlyList<T> Instance = new List<T>(0);
    }
}
