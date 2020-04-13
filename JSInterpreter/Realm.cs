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

        }

        private void AddRestrictedFunctionProperties(FunctionObject F)
        {
#warning implement getters and setters and resume here.
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
