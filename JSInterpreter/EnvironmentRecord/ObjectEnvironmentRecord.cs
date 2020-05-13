using System;

namespace JSInterpreter
{
    public class ObjectEnvironmentRecord : EnvironmentRecord
    {
        public readonly Object BindingObject;
        public readonly bool withEnvironment;

        public ObjectEnvironmentRecord(Object bindingObject, bool withEnvironment)
        {
            BindingObject = bindingObject;
            this.withEnvironment = withEnvironment;
        }

        public override Completion CreateImmutableBinding(string name, bool strict)
        {
            throw new InvalidOperationException("Spec 8.1.1.2.3");
        }

        public override Completion CreateMutableBinding(string name, bool deletable)
        {
            return BindingObject.DefinePropertyOrThrow(name, new PropertyDescriptor(UndefinedValue.Instance, true, true, deletable));
        }

        public override BooleanCompletion DeleteBinding(string name)
        {
            return BindingObject.InternalDelete(name);
        }

        public override Completion GetBindingValue(string name, bool strict)
        {
            var valueComp = BindingObject.HasProperty(name);
            if (valueComp.IsAbrupt()) return valueComp;
            var value = valueComp.Other;
            if (!value)
            {
                if (!strict)
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                else
                    return Completion.ThrowReferenceError($"No value {name} defined.");
            }
            return BindingObject.Get(name);
        }

        public override BooleanCompletion HasBinding(string name)
        {
            var foundBindingsComp = BindingObject.HasProperty(name);
            if (foundBindingsComp.IsAbrupt()) return foundBindingsComp;
            var foundBindings = foundBindingsComp.Other;
            if (!foundBindings) return false;
            if (!withEnvironment) return true;
            throw new NotImplementedException("With statements are not supported.");
        }

        public override bool HasSuperBinding()
        {
            return false;
        }

        public override bool HasThisBinding()
        {
            return false;
        }

        public override Completion InitializeBinding(string name, IValue value)
        {
            return SetMutableBinding(name, value, false);
        }

        public override Completion SetMutableBinding(string name, IValue value, bool strict)
        {
            return BindingObject.Set(name, value, strict);
        }

        public override IValue WithBaseObject()
        {
            if (withEnvironment)
                return BindingObject;
            return UndefinedValue.Instance;
        }
    }
}
