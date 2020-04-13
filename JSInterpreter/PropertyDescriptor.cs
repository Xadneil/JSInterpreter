using System;

namespace JSInterpreter
{
    class PropertyDescriptor
    {
        public IValue Value;
        public bool? writeable;
        public readonly bool? enumerable;
        public readonly bool? configurable;

        public PropertyDescriptor(IValue Value, bool? writeable, bool? enumerable, bool? configurable)
        {
            this.Value = Value;
            this.writeable = writeable;
            this.enumerable = enumerable;
            this.configurable = configurable;
        }

        public static PropertyDescriptor FillDefaultValues(PropertyDescriptor property)
        {
            return new PropertyDescriptor(
                property.Value,
                property.writeable.GetValueOrDefault(false),
                property.enumerable.GetValueOrDefault(false),
                property.configurable.GetValueOrDefault(false)
                );
        }

        public bool IsDataDescriptor()
        {
            return true;
        }

        public bool IsAccessorDescriptor()
        {
            return false;
        }

        public bool IsGenericDescriptor() => !IsDataDescriptor() && !IsAccessorDescriptor();

        public PropertyDescriptor(PropertyDescriptor old, PropertyDescriptor newDesc)
        {
            Value = newDesc.Value ?? old.Value;
            writeable = newDesc.writeable ?? old.writeable.Value;
            enumerable = newDesc.enumerable ?? old.enumerable.Value;
            configurable = newDesc.configurable ?? old.configurable.Value;
        }
    }

    class PropertyDescriptorCompletion
    {
        public readonly Completion completion;
        public readonly PropertyDescriptor propertyDescriptor;

        public static PropertyDescriptorCompletion NormalCompletion(PropertyDescriptor propertyDescriptor)
        {
            return new PropertyDescriptorCompletion(Completion.NormalCompletion(), propertyDescriptor);
        }

        public static PropertyDescriptorCompletion NormalCompletion(IValue value)
        {
            return new PropertyDescriptorCompletion(Completion.NormalCompletion(value), null);
        }

        private PropertyDescriptorCompletion(Completion completion, PropertyDescriptor propertyDescriptor)
        {
            this.completion = completion;
            this.propertyDescriptor = propertyDescriptor;
        }

        public bool IsAbrupt()
        {
            return completion.IsAbrupt();
        }
    }
}
