using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class Realm
    {
        public static Realm CreateRealm()
        {
            var realmRec = new Realm();
            realmRec.CreateIntrinsics();
            return realmRec;
        }

        private void CreateIntrinsics()
        {
            Intrinsics = new Intrinsics();
            Intrinsics.ObjectPrototype = Utils.ObjectCreate(null);
            Intrinsics.ThrowTypeError = Utils.CreateBuiltinFunction(() => Completion.ThrowTypeError(), Utils.EmptyList<string>(), this, null);
            Intrinsics.FunctionPrototype = Utils.CreateBuiltinFunction(() => Completion.NormalCompletion(), Utils.EmptyList<string>(), this, Intrinsics.ObjectPrototype);
            Intrinsics.ThrowTypeError.SetPrototypeOf(Intrinsics.FunctionPrototype);
            AddRestrictedFunctionProperties(Intrinsics.FunctionPrototype);
#warning add the rest of the global properties
        }

        private void AddRestrictedFunctionProperties(FunctionObject F)
        {
            F.DefinePropertyOrThrow("caller", new PropertyDescriptor() { Get = Intrinsics.ThrowTypeError, Set = Intrinsics.ThrowTypeError, Enumerable = false, Configurable = true });
            F.DefinePropertyOrThrow("arguments", new PropertyDescriptor() { Get = Intrinsics.ThrowTypeError, Set = Intrinsics.ThrowTypeError, Enumerable = false, Configurable = true });
        }

        public Object GlobalObject { get; private set; }
        public LexicalEnvironment GlobalEnv { get; private set; }
        public Intrinsics Intrinsics;
    }

    class Intrinsics
    {
        public Object ObjectPrototype;
        public FunctionObject FunctionPrototype;
        public FunctionObject ThrowTypeError;
    }
}
