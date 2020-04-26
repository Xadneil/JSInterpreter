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
            if (proto != null)
                obj.SetPrototypeOf(proto);
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
                        var next = iteratorRecord.MoveNext();
                        if (iteratorRecord.Current.IsAbrupt()) return iteratorRecord.Current.WithEmpty<List<IValue>>();
                        if (!next) break;
                        ret.Add(iteratorRecord.Current.value);
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
                    throw new NotImplementedException("Utils.EvaluateCall: Spec 12.3.4.2 Step 1bii: With statements are not implemented.");
                }
            }
            else
                thisValue = UndefinedValue.Instance;

            var argList = arguments.ArgumentListEvaluation();
            if (argList.IsAbrupt()) return argList;

            if (!(func is Callable functionObject))
                return Completion.ThrowTypeError("Utils.EvaluateCall: func must be a function object.");
            if (tailCall)
            {
                throw new NotImplementedException("Utils.EvaluateCall: tail calls not implemented");
            }
            return functionObject.Call(thisValue, argList.Other);
        }

        internal static Completion OrdinaryCreateFromConstructor(Object constructor, string intrinsicDefaultProto, IEnumerable<string> internalSlotsList = null)
        {
            var protoComp = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);
            if (protoComp.IsAbrupt()) return protoComp;
            var proto = protoComp.value;
            return Completion.NormalCompletion(ObjectCreate(proto, internalSlotsList));
        }

        private static Completion GetPrototypeFromConstructor(Object constructor, string intrinsicDefaultProto)
        {
            if (!(constructor is Callable))
                throw new InvalidOperationException("GetPrototypeFromConstructor: constructor is not callable");
            var protoComp = constructor.InternalGet("prototype", constructor);
            if (protoComp.IsAbrupt()) return protoComp;
            var proto = protoComp.value;
            if (!(proto is Object))
            {
                if (intrinsicDefaultProto == "%ObjectPrototype%")
                {
                    proto = ObjectPrototype.Instance;
                }
            }
            return Completion.NormalCompletion(proto);
        }

        public static FunctionObject CreateBuiltinFunction(Func<Completion> steps, IEnumerable<string> internalSlotsList, Realm realm = null, Object prototype = null)
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

        public class BuiltinFunction : FunctionObject
        {
            private readonly Func<Completion> CallAction;

            public BuiltinFunction(Func<Completion> callAction)
            {
                CallAction = callAction;
            }

            public override Completion InternalCall(IValue @this, IReadOnlyList<IValue> arguments)
            {
                return CallAction();
            }
        }
    }

    public class EmptyListClass<T>
    {
        public static readonly IReadOnlyList<T> Instance = new List<T>(0);
    }
}
