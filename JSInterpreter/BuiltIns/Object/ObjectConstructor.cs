using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ObjectConstructor : Constructor
    {
        public ObjectConstructor()
        {
            prototype = new ObjectPrototype(this);
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

            DefinePropertyOrThrow("length", new PropertyDescriptor(new NumberValue(1), false, false, false));
        }

        public void DefineDeferredProperties(Realm realm)
        {
            DefinePropertyOrThrow("create", new PropertyDescriptor(Utils.CreateBuiltinFunction(create, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("defineProperty", new PropertyDescriptor(Utils.CreateBuiltinFunction(defineProperty, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("getPrototypeOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(getPrototypeOf, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("preventExtensions", new PropertyDescriptor(Utils.CreateBuiltinFunction(preventExtensions, Utils.EmptyList<string>(), realm), true, false, true));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            if (arguments.Count == 0)
            {
                return Construct(arguments);
            }

            if (arguments[0] == NullValue.Instance || arguments[0] == UndefinedValue.Instance)
            {
                return Construct(arguments);
            }

            return arguments[0].ToObject();
        }

        public Completion Construct(IReadOnlyList<IValue> arguments)
        {
            return InternalConstruct(arguments, this);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            if (arguments.Count == 0)
            {
                return Completion.NormalCompletion(Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ObjectPrototype));
            }
            return InternalCall(this, arguments);
        }

        private Completion create(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;

            if (!(arguments[0] is Object) || arguments[0] == NullValue.Instance)
                return Completion.ThrowTypeError("An object or null is required as the first argument to Object.create");
            var obj = Utils.ObjectCreate(arguments[0]);

            if (arguments.Count > 1 && arguments[1] != UndefinedValue.Instance)
                return ObjectDefineProperties(obj, arguments[1]);
            return Completion.NormalCompletion(obj);
        }

        private Completion defineProperty(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments<Object, IValue, Object>(arguments);
            if (argComp.IsAbrupt()) return argComp;

            var O = arguments[0] as Object;
            var P = arguments[1];
            var Attributes = arguments[2] as Object;

            var keyComp = P.ToPropertyKey();
            if (keyComp.IsAbrupt()) return keyComp;
            var key = keyComp.Other;

            var desc = PropertyDescriptor.FromObject(Attributes);
            if (desc.IsAbrupt()) return desc;

            var comp = O.DefinePropertyOrThrow(key, desc.Other);
            if (comp.IsAbrupt()) return comp;

            return Completion.NormalCompletion(O);
        }

        private Completion getPrototypeOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argCheck = Utils.CheckArguments(arguments, 1);
            if (argCheck.IsAbrupt()) return argCheck;
            var O = arguments[0].ToObject();
            if (O.IsAbrupt()) return O;
            return (O.value as Object).GetPrototypeOf();
        }

        private Completion preventExtensions(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argCheck = Utils.CheckArguments(arguments, 1);
            if (argCheck.IsAbrupt()) return argCheck;
            if (!(arguments[0] is Object O))
                return Completion.NormalCompletion(arguments[0]);
            var status = O.PreventExtensions();
            if (status == false)
                return Completion.ThrowTypeError("Could not prevent extensions");
            return Completion.NormalCompletion(O);
        }

        private Completion ObjectDefineProperties(Object O, IValue Properties)
        {
            var propsComp = Properties.ToObject();
            if (propsComp.IsAbrupt()) return propsComp;
            var props = propsComp.value as Object;
            var keys = props.OwnPropertyKeys();
            var descriptors = new List<(string, PropertyDescriptor)>();
            foreach (var nextKey in keys)
            {
                var propDesc = props.GetOwnProperty(nextKey);
                if (propDesc.IsAbrupt()) return propDesc;
                if (propDesc.Other != null && propDesc.Other.Enumerable.GetValueOrDefault() == true)
                {
                    var descObj = props.Get(nextKey);
                    if (descObj.IsAbrupt()) return descObj;
                    if (!(descObj.value is Object o))
                        return Completion.ThrowTypeError("properties of the property argument must be objects.");
                    var desc = PropertyDescriptor.FromObject(o);
                    if (desc.IsAbrupt()) return desc;
                    descriptors.Add((nextKey, desc.Other));
                }
            }
            foreach (var (P, desc) in descriptors)
            {
                var comp = O.DefinePropertyOrThrow(P, desc);
                if (comp.IsAbrupt()) return comp;
            }
            return Completion.NormalCompletion(O);
        }

    }
}
