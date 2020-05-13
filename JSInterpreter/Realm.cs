using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class Realm
    {
        private Realm() { }

        public static Realm CreateRealm()
        {
            var realmRec = new Realm();
            realmRec.CreateIntrinsics();
            return realmRec;
        }

        public Object GlobalObject { get; private set; }
        public LexicalEnvironment GlobalEnv { get; private set; }
        public Intrinsics Intrinsics;

        private void CreateIntrinsics()
        {
            Intrinsics = new Intrinsics();
            Intrinsics.ObjectConstructor = new ObjectConstructor();
            Intrinsics.ObjectPrototype = Intrinsics.ObjectConstructor.prototype as ObjectPrototype;
            Intrinsics.FunctionConstructor = new FunctionConstructor(Intrinsics.ObjectPrototype);
            Intrinsics.FunctionPrototype = Intrinsics.FunctionConstructor.prototype as FunctionPrototype;

            Intrinsics.ObjectConstructor.DefineDeferredProperties(this);
            Intrinsics.ObjectPrototype.DefineDeferredProperties(this);
            Intrinsics.FunctionPrototype.DefineDeferredProperties(this);

            Intrinsics.Eval = DefineEval();

            Intrinsics.ArrayConstructor = new ArrayConstructor(Intrinsics.FunctionPrototype);
            Intrinsics.ArrayPrototype = new ArrayPrototype(Intrinsics.ArrayConstructor, this);
            Intrinsics.ArrayConstructor.InitPrototypeProperty(Intrinsics.ArrayPrototype);

            Intrinsics.DateConstructor = new DateConstructor(Intrinsics.FunctionPrototype);
            Intrinsics.DatePrototype = new DatePrototype(Intrinsics.DateConstructor, this);

            Intrinsics.ErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "Error");
            Intrinsics.ErrorPrototype = new ErrorPrototype(Intrinsics.ErrorConstructor);
            Intrinsics.RangeErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "RangeError");
            Intrinsics.RangeErrorPrototype = new ErrorPrototype(Intrinsics.RangeErrorConstructor);
            Intrinsics.ReferenceErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "ReferenceError");
            Intrinsics.ReferenceErrorPrototype = new ErrorPrototype(Intrinsics.ReferenceErrorConstructor);
            Intrinsics.SyntaxErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "SyntaxError");
            Intrinsics.SyntaxErrorPrototype = new ErrorPrototype(Intrinsics.SyntaxErrorConstructor);
            Intrinsics.TypeErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "TypeError");
            Intrinsics.TypeErrorPrototype = new ErrorPrototype(Intrinsics.TypeErrorConstructor);
            Intrinsics.URIErrorConstructor = new ErrorConstructor(Intrinsics.FunctionPrototype, "URIError");
            Intrinsics.URIErrorPrototype = new ErrorPrototype(Intrinsics.URIErrorConstructor);

            Intrinsics.Math = new BuiltIns.Math.Math(Intrinsics.ObjectPrototype, this);

            Intrinsics.NumberConstructor = new NumberConstructor(Intrinsics.FunctionPrototype);
            Intrinsics.NumberPrototype = new NumberPrototype(Intrinsics.NumberConstructor, this);

            Intrinsics.StringConstructor = new StringConstructor(Intrinsics.FunctionPrototype, this);
            Intrinsics.StringPrototype = new StringPrototype(Intrinsics.StringConstructor, this);

        }

        public Realm SetRealmGlobalObject(Object globalObj, Object thisValue)
        {
            if (globalObj == null)
                globalObj = Utils.ObjectCreate(Intrinsics.ObjectPrototype);
            if (thisValue == null)
                thisValue = globalObj;
            GlobalObject = globalObj;
            var newGlobalEnv = NewGlobalEnvironment(globalObj, thisValue);
            GlobalEnv = newGlobalEnv;
            return this;
        }

        private static LexicalEnvironment NewGlobalEnvironment(Object G, Object thisValue)
        {
            var env = new LexicalEnvironment();
            var objRec = new ObjectEnvironmentRecord(G, false);
            var dclRec = new DeclarativeEnvironmentRecord();
            var globalRec = new GlobalEnvironmentRecord(objRec, thisValue, dclRec);
            env.EnvironmentRecord = globalRec;
            return env;
        }

        public Callable DefineEval()
        {
            return Utils.CreateBuiltinFunction(GlobalObjectProperties.eval, Utils.EmptyList<string>(), this);
        }

        public Completion SetDefaultGlobalBindings()
        {
            Completion comp;
            comp = GlobalObject.DefinePropertyOrThrow("Infinity", new PropertyDescriptor(new NumberValue(double.PositiveInfinity), false, false, false));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("NaN", new PropertyDescriptor(new NumberValue(double.NaN), false, false, false));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("undefined", new PropertyDescriptor(UndefinedValue.Instance, false, false, false));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("eval", new PropertyDescriptor(Intrinsics.Eval, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("isFinite", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.isFinite, Utils.EmptyList<string>(), this), true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("isNaN", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.isNaN, Utils.EmptyList<string>(), this), true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("parseFloat", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.parseFloat, Utils.EmptyList<string>(), this), true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("parseInt", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.parseInt, Utils.EmptyList<string>(), this), true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Function", new PropertyDescriptor(Intrinsics.FunctionConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Array", new PropertyDescriptor(Intrinsics.ArrayConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Date", new PropertyDescriptor(Intrinsics.DateConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Error", new PropertyDescriptor(Intrinsics.ErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("RangeError", new PropertyDescriptor(Intrinsics.RangeErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("ReferenceError", new PropertyDescriptor(Intrinsics.ReferenceErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("SyntaxError", new PropertyDescriptor(Intrinsics.SyntaxErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("TypeError", new PropertyDescriptor(Intrinsics.TypeErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;
            comp = GlobalObject.DefinePropertyOrThrow("URIError", new PropertyDescriptor(Intrinsics.URIErrorConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Math", new PropertyDescriptor(Intrinsics.Math, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Number", new PropertyDescriptor(Intrinsics.NumberConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("Object", new PropertyDescriptor(Intrinsics.ObjectConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            comp = GlobalObject.DefinePropertyOrThrow("String", new PropertyDescriptor(Intrinsics.StringConstructor, true, false, true));
            if (comp.IsAbrupt()) return comp;

            return Completion.NormalCompletion();
        }
    }

    public class Intrinsics
    {
        public ObjectConstructor ObjectConstructor;
        public ObjectPrototype ObjectPrototype;
        public FunctionConstructor FunctionConstructor;
        public FunctionPrototype FunctionPrototype;

        public Callable Eval;

        public ArrayConstructor ArrayConstructor;
        public ArrayIteratorPrototype ArrayIteratorPrototype;
        public ArrayPrototype ArrayPrototype;
        public DateConstructor DateConstructor;
        public DatePrototype DatePrototype;

        public ErrorConstructor ErrorConstructor;
        public ErrorPrototype ErrorPrototype;
        public ErrorConstructor RangeErrorConstructor;
        public ErrorPrototype RangeErrorPrototype;
        public ErrorConstructor ReferenceErrorConstructor;
        public ErrorPrototype ReferenceErrorPrototype;
        public ErrorConstructor SyntaxErrorConstructor;
        public ErrorPrototype SyntaxErrorPrototype;
        public ErrorConstructor TypeErrorConstructor;
        public ErrorPrototype TypeErrorPrototype;
        public ErrorConstructor URIErrorConstructor;
        public ErrorPrototype URIErrorPrototype;

        public BuiltIns.Math.Math Math;
        public NumberConstructor NumberConstructor;
        public NumberPrototype NumberPrototype;
        public StringConstructor StringConstructor;
        public StringPrototype StringPrototype;
    }
}
