using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class RegExpConstructor : Constructor
    {
        public RegExpConstructor(FunctionPrototype prototype, Realm realm)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("@@species", new PropertyDescriptor(Utils.CreateBuiltinFunction((a, b) => Completion.NormalCompletion(this), realm), null, false, false));
        }

        public void InitPrototypeProperty(RegExpPrototype prototype)
        {
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, false, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            return Construct(arguments, this);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            var pattern = arguments.At(0, UndefinedValue.Instance);
            var flags = arguments.At(1, UndefinedValue.Instance);

            var patternIsRegExpComp = IsRegExp(pattern);
            if (patternIsRegExpComp.IsAbrupt()) return patternIsRegExpComp;
            var patternIsRegExp = patternIsRegExpComp.Other;
            if (newTarget == null)
            {
                newTarget = this;
                if (patternIsRegExp && flags == UndefinedValue.Instance)
                {
                    var patternObject = (pattern as Object)!;
                    var patternConstructor = patternObject.Get("constructor");
                    if (patternConstructor.IsAbrupt()) return patternConstructor;
                    if (patternObject == newTarget)
                        return Completion.NormalCompletion(pattern);
                }
            }
            IValue P, F;

            if (pattern is RegExpObject r)
            {
                P = new StringValue(r.OriginalSource);
                if (flags == UndefinedValue.Instance)
                    F = new StringValue(r.OriginalFlags);
                else
                    F = flags;
            }
            else if (patternIsRegExp)
            {
                var patternObject = (pattern as Object)!;
                var PComp = patternObject.Get("source");
                if (PComp.IsAbrupt()) return PComp;
                P = PComp.value!;
                if (flags == UndefinedValue.Instance)
                {
                    var FComp = patternObject.Get("flags");
                    if (FComp.IsAbrupt()) return FComp;
                    F = FComp.value!;
                }
                else
                {
                    F = flags;
                }
            }
            else
            {
                P = pattern;
                F = flags;
            }
            return RegExpObject.RegExpAllocAndInitialize(P, F);
        }

        public static BooleanCompletion IsRegExp(IValue argument)
        {
            if (!(argument is Object o))
                return false;
            var matcher = o.Get("@@match");
            if (matcher.IsAbrupt()) return matcher.WithEmptyBool();
            if (matcher.value != UndefinedValue.Instance)
                return matcher.value!.ToBoolean().boolean;
            if (argument is RegExpObject) return true;
            return false;
        }
    }
}
