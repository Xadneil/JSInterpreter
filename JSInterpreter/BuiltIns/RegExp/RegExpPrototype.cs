using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JSInterpreter
{
    public class RegExpPrototype : Object
    {
        public RegExpPrototype(RegExpConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
            prototype = realm.Intrinsics.ObjectPrototype;

            DefinePropertyOrThrow("exec", new PropertyDescriptor(Utils.CreateBuiltinFunction(exec, realm), true, false, true));
            DefinePropertyOrThrow("source", new PropertyDescriptor(Utils.CreateBuiltinFunction(source, realm), null, false, true));
        }

        private static Completion exec(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var @string = arguments.At(0, UndefinedValue.Instance).value!;
            if (!(thisValue is RegExpObject R))
                return Completion.ThrowTypeError("this value must be a RegExp object");
            var S = @string.ToJsString();
            if (S.IsAbrupt()) return S;
            return RegExpBuiltinExec(R, (S.value as StringValue)!.@string);
        }

        private static Completion source(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            if (!(thisValue is Object))
                return Completion.ThrowTypeError("Not valid RegExp object");
            if (!(thisValue is RegExpObject o))
            {
                if (thisValue == Interpreter.Instance().CurrentRealm().Intrinsics.RegExpPrototype)
                    return Completion.NormalCompletion(new StringValue("(?:)"));
                return Completion.ThrowTypeError("Not valid RegExp object");
            }
            return Completion.NormalCompletion(new StringValue(EscapeRegExpPattern(o.OriginalSource)));
        }

        private static string EscapeRegExpPattern(string P)
        {
            return P.Replace("/", "\\/", StringComparison.Ordinal);
        }

        private static Completion RegExpBuiltinExec(RegExpObject R, string S)
        {
            var length = S.Length;
            int lastIndex;
            {
                var lastIndexComp = R.Get("lastIndex");
                if (lastIndexComp.IsAbrupt()) return lastIndexComp;
                var lastIndexLength = lastIndexComp.value!.ToLength();
                if (lastIndexLength.IsAbrupt()) return lastIndexLength;
                lastIndex = (int)lastIndexLength.Other;
            }
            var flags = R.OriginalFlags;
            var global = flags.Contains('g', StringComparison.Ordinal);
            var sticky = flags.Contains('y', StringComparison.Ordinal);
            if (!global && !sticky)
                lastIndex = 0;
            var matcher = R.RegExpMatcher;
            var fullUnicode = flags.Contains('u', StringComparison.Ordinal);
            var matchSucceeded = false;
            Match? r = null;
            while (!matchSucceeded)
            {
                if (lastIndex > length)
                {
                    if (global || sticky)
                    {
                        var set = R.Set("lastIndex", NumberValue.PositiveZero, true);
                        if (set.IsAbrupt()) return set;
                    }
                    return Completion.NormalCompletion(NullValue.Instance);
                }
                r = matcher.Match(S, lastIndex);
                if (!r.Success)
                {
                    if (sticky)
                    {
                        var set = R.Set("lastIndex", NumberValue.PositiveZero, true);
                        if (set.IsAbrupt()) return set;
                        return Completion.NormalCompletion(NullValue.Instance);
                    }
                    lastIndex = AdvanceStringIndex(S, lastIndex, fullUnicode);
                }
                else
                    matchSucceeded = true;
            }

            var e = r!.Index + r.Length;
            if (fullUnicode)
            {
                // e is an index into the Input character list, derived from S, matched by matcher.
                // Let eUTF be the smallest index into S that corresponds to the character at element e of Input.
                // If e is greater than or equal to the number of elements in Input, then eUTF is the number of code units in S.
                // Set e to eUTF.
                var indexes = StringInfo.ParseCombiningCharacters(S);
                if (r.Index < indexes.Length)
                {
                    var sub = StringInfo.GetNextTextElement(S, r.Index);
                    e += sub.Length - 1;
                }
            }
            if (global || sticky)
            {
                var set = R.Set("lastIndex", new NumberValue(e), true);
                if (set.IsAbrupt()) return set;
            }
            var n = r.Groups.Count;
            var A = ArrayObject.ArrayCreate(n + 1);
            Utils.CreateDataProperty(A, "index", new NumberValue(lastIndex));
            Utils.CreateDataProperty(A, "input", new StringValue(S));
            Utils.CreateDataProperty(A, "0", new StringValue(r.Value));
            IValue groups;
            if (R.RegExpMatcher.GetGroupNames().Any())
                groups = Utils.ObjectCreate(null);
            else
                groups = UndefinedValue.Instance;
            Utils.CreateDataProperty(A, "groups", groups);
            for (int i = 0; i <= n; i++)
            {
                var captureI = r.Groups[i];
                IValue capturedValue;
                if (captureI.Success)
                {
                    if (fullUnicode)
                        capturedValue = new StringValue(StringInfo.GetNextTextElement(S, captureI.Index));
                    else
                        capturedValue = new StringValue(captureI.Value);
                }
                else
                    capturedValue = UndefinedValue.Instance;
                Utils.CreateDataProperty(A, i.ToString(CultureInfo.InvariantCulture), capturedValue);
                if (!string.IsNullOrEmpty(captureI.Name))
                    Utils.CreateDataProperty(groups, captureI.Name, capturedValue);
            }
            return Completion.NormalCompletion(A);
        }

        private static int AdvanceStringIndex(string s, int index, bool unicode)
        {
            if (!unicode || index + 1 >= s.Length)
            {
                return index + 1;
            }

            var first = s[index];
            if (first < 0xD800 || first > 0xDBFF)
            {
                return index + 1;
            }

            var second = s[index + 1];
            if (second < 0xDC00 || second > 0xDFFF)
            {
                return index + 1;
            }

            return index + 2;
        }
    }
}
