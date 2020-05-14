using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public class StringObject : Object
    {
        public readonly StringValue value;

        public StringObject(StringValue value)
        {
            this.value = value;
            prototype = Interpreter.Instance().CurrentRealm().Intrinsics.StringPrototype;
            DefineOwnProperty("length", new PropertyDescriptor(new NumberValue(value.@string.Length), false, false, false));
        }

        public override CompletionOr<PropertyDescriptor?> GetOwnProperty(string P)
        {
            var desc = OrdinaryGetOwnProperty(P);
            if (desc != null)
                return Completion.NormalWith(desc);
            return StringGetOwnProperty(P);
        }

        public override BooleanCompletion DefineOwnProperty(string name, PropertyDescriptor property)
        {
            var stringDesc = StringGetOwnProperty(name);
            if (stringDesc.Other != null)
            {
                return IsCompatiblePropertyDescriptor(IsExtensible, property, stringDesc.Other);
            }
            return OrdinaryDefineOwnProperty(name, property);
        }

        private BooleanCompletion IsCompatiblePropertyDescriptor(bool Extensible, PropertyDescriptor Desc, PropertyDescriptor Current)
        {
            return ValidateAndApplyPropertyDescriptor(null, null, Extensible, Desc, Current);
        }

        public override IReadOnlyList<string> OwnPropertyKeys()
        {
            var keys = new List<string>();
            for (int i = 0; i < value.@string.Length; i++)
            {
                keys.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            var propertiesWithIndex = propertyNames.Select(pair => (isIndex: int.TryParse(pair.Key, out int x), index: x, name: pair.Key, chronologicalIndex: pair.Value));

            foreach (var P in propertiesWithIndex.Where(pair => pair.isIndex).OrderBy(pair => pair.index))
            {
                keys.Add(P.name);
            }
            foreach (var P in propertiesWithIndex.Where(pair => !pair.isIndex).OrderBy(pair => pair.chronologicalIndex))
            {
                keys.Add(P.name);
            }
            return keys;
        }

        private CompletionOr<PropertyDescriptor?> StringGetOwnProperty(string P)
        {
            if (!double.TryParse(P, out double index))
                return Completion.NormalCompletion().WithEmpty<PropertyDescriptor?>();
            if (index != (int)index)
                return Completion.NormalCompletion().WithEmpty<PropertyDescriptor?>();
            if (index == 0d && BitConverter.GetBytes(index)[7] == 128) // negative zero
                return Completion.NormalCompletion().WithEmpty<PropertyDescriptor?>();
            if (index < 0 || value.@string.Length <= index)
                return Completion.NormalCompletion().WithEmpty<PropertyDescriptor?>();
            return Completion.NormalWith(new PropertyDescriptor(new StringValue(value.@string[(int)index].ToString(System.Globalization.CultureInfo.InvariantCulture)), false, true, false));
        }
    }
}
