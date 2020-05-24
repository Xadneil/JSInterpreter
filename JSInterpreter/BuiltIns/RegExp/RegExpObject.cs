using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JSInterpreter
{
    public class RegExpObject : Object
    {
        public string OriginalSource { get; private set; }
        public string OriginalFlags { get; private set; }
        public Regex RegExpMatcher { get; private set; }

        public RegExpObject(string originalSource, string originalFlags, Regex regExpMatcher)
        {
            prototype = Interpreter.Instance().CurrentRealm().Intrinsics.RegExpPrototype;
            DefinePropertyOrThrow("lastIndex", new PropertyDescriptor(null, true, false, false));
            OriginalSource = originalSource;
            OriginalFlags = originalFlags;
            RegExpMatcher = regExpMatcher;
        }

        public static Completion RegExpAllocAndInitialize(IValue Pvalue, IValue Fvalue)
        {
            string P;
            if (Pvalue == UndefinedValue.Instance)
                P = "";
            else
            {
                var comp = Pvalue.ToJsString();
                if (comp.IsAbrupt()) return comp;
                P = (comp.value as StringValue)!.@string;
            }

            string F;
            if (Fvalue == UndefinedValue.Instance)
                F = "";
            else
            {
                var comp = Fvalue.ToJsString();
                if (comp.IsAbrupt()) return comp;
                F = (comp.value as StringValue)!.@string;
            }
            return RegExpAllocAndInitialize(P, F);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static Completion RegExpAllocAndInitialize(string P, string F)
        {
            var allowedFlags = "gimsuy";
            List<char> invalidFlags;
            if ((invalidFlags = F.Where(c => !allowedFlags.Contains(c, StringComparison.Ordinal)).ToList()).Any())
            {
                if (invalidFlags.Count == 1)
                    return Completion.ThrowTypeError($"'{invalidFlags[0]}' is not a valid RegExp flag.");
                else
                    return Completion.ThrowTypeError($"These flags not valid RegExp flags: {{ {string.Join(", ", invalidFlags.Select(f => $"'{f}'"))} }}");
            }
            else if (F.Distinct().Count() != F.Length)
            {
                return Completion.ThrowTypeError($"RegExp flags should not contain duplicates.");
            }

            var BMP = !F.Contains('u', StringComparison.Ordinal);

            var options = RegexOptions.ECMAScript;
            if (F.Contains('m', StringComparison.Ordinal))
                options |= RegexOptions.Multiline;
            if (F.Contains('s', StringComparison.Ordinal))
            {
                options |= RegexOptions.Singleline;
                options &= ~RegexOptions.ECMAScript;
            }
            if (F.Contains('i', StringComparison.Ordinal))
                options |= RegexOptions.IgnoreCase;

            if (options.HasFlag(RegexOptions.Multiline))
            {
                int index = 0;
                var newPattern = P;
                while ((index = newPattern.IndexOf("$", index, StringComparison.Ordinal)) != -1)
                {
                    if (index > 0 && newPattern[index - 1] != '\\')
                    {
                        newPattern = newPattern.Substring(0, index) + @"\r?" + newPattern.Substring(index);
                        index += 4;
                    }
                }

                P = newPattern;
            }
            Regex regex;
            try
            {
                regex = new Regex(P, options);
            }
            catch
            {
                return Completion.ThrowSyntaxError("Invalid RegEx");
            }

            var obj = new RegExpObject(P, F, regex);
            var set = obj.Set("lastIndex", new NumberValue(0), true);
            if (set.IsAbrupt()) return set;
            return Completion.NormalCompletion(obj);
        }
    }
}
