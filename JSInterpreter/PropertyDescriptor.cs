using System;

namespace JSInterpreter
{
    public class PropertyDescriptor
    {
        public IValue Value;
        public bool? Writable;
        public bool? Enumerable;
        public bool? Configurable;

        public Callable Get;
        public Callable Set;

        public PropertyDescriptor(IValue value, bool? writable, bool? enumerable, bool? configurable)
        {
            Value = value;
            Writable = writable;
            Enumerable = enumerable;
            Configurable = configurable;
        }

        public PropertyDescriptor(Callable getter, Callable setter, bool? enumerable, bool? configurable)
        {
            Get = getter;
            Set = setter;
            Enumerable = enumerable;
            Configurable = configurable;
        }

        public PropertyDescriptor()
        {
        }

        public bool IsDataDescriptor()
        {
            return Value != null || Writable != null;
        }

        public bool IsAccessorDescriptor()
        {
            return Get != null || Set != null;
        }

        public bool IsGenericDescriptor() => !IsDataDescriptor() && !IsAccessorDescriptor();

        public Object ToObject()
        {
            var obj = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ObjectPrototype);
            if (Value != null)
                Utils.CreateDataProperty(obj, "value", Value);
            if (Writable.HasValue)
                Utils.CreateDataProperty(obj, "writable", Writable.Value ? BooleanValue.True : BooleanValue.False);
            if (Get != null)
                Utils.CreateDataProperty(obj, "get", Get);
            if (Set != null)
                Utils.CreateDataProperty(obj, "set", Set);
            if (Enumerable.HasValue)
                Utils.CreateDataProperty(obj, "enumerable", Enumerable.Value ? BooleanValue.True : BooleanValue.False);
            if (Configurable.HasValue)
                Utils.CreateDataProperty(obj, "configurable", Configurable.Value ? BooleanValue.True : BooleanValue.False);
            return obj;
        }

        public PropertyDescriptor Copy()
        {
            return (PropertyDescriptor)MemberwiseClone();
        }

        public static CompletionOr<PropertyDescriptor> FromObject(Object Obj)
        {
            var desc = new PropertyDescriptor();

            var hasEnumerable = Obj.HasProperty("enumerable");
            if (hasEnumerable.IsAbrupt()) return hasEnumerable.WithEmpty<PropertyDescriptor>();
            if (hasEnumerable.Other)
            {
                var enumerable = Obj.Get("enumerable");
                if (enumerable.IsAbrupt()) return enumerable.WithEmpty<PropertyDescriptor>();
                desc.Enumerable = enumerable.value.ToBoolean().boolean;
            }

            var hasConfigurable = Obj.HasProperty("configurable");
            if (hasConfigurable.IsAbrupt()) return hasConfigurable.WithEmpty<PropertyDescriptor>();
            if (hasConfigurable.Other)
            {
                var configurable = Obj.Get("configurable");
                if (configurable.IsAbrupt()) return configurable.WithEmpty<PropertyDescriptor>();
                desc.Configurable = configurable.value.ToBoolean().boolean;
            }

            var hasValue = Obj.HasProperty("value");
            if (hasValue.IsAbrupt()) return hasValue.WithEmpty<PropertyDescriptor>();
            if (hasValue.Other)
            {
                var value = Obj.Get("value");
                if (value.IsAbrupt()) return value.WithEmpty<PropertyDescriptor>();
                desc.Value = value.value;
            }

            var hasWritable = Obj.HasProperty("writable");
            if (hasWritable.IsAbrupt()) return hasWritable.WithEmpty<PropertyDescriptor>();
            if (hasWritable.Other)
            {
                var writable = Obj.Get("writable");
                if (writable.IsAbrupt()) return writable.WithEmpty<PropertyDescriptor>();
                desc.Writable = writable.value.ToBoolean().boolean;
            }

            var hasGet = Obj.HasProperty("get");
            if (hasGet.IsAbrupt()) return hasGet.WithEmpty<PropertyDescriptor>();
            if (hasGet.Other)
            {
                var get = Obj.Get("get");
                if (get.IsAbrupt()) return get.WithEmpty<PropertyDescriptor>();
                if (get.value != UndefinedValue.Instance)
                {
                    if (!(get.value is Callable callable))
                        return Completion.ThrowTypeError("get property is not callable").WithEmpty<PropertyDescriptor>();
                    desc.Get = callable;
                }
            }

            var hasSet = Obj.HasProperty("set");
            if (hasSet.IsAbrupt()) return hasSet.WithEmpty<PropertyDescriptor>();
            if (hasSet.Other)
            {
                var set = Obj.Get("set");
                if (set.IsAbrupt()) return set.WithEmpty<PropertyDescriptor>();
                if (set.value != UndefinedValue.Instance)
                {
                    if (!(set.value is Callable callable))
                        return Completion.ThrowTypeError("set property is not callable").WithEmpty<PropertyDescriptor>();
                    desc.Set = callable;
                }
            }

            if ((desc.Get != null || desc.Set != null) && (desc.Value != null || desc.Writable.HasValue))
                return Completion.ThrowTypeError("Invalid property descriptor. Cannot both specify accessors and a value or writable attribute").WithEmpty<PropertyDescriptor>();

            return Completion.NormalWith(desc);
        }
    }
}
