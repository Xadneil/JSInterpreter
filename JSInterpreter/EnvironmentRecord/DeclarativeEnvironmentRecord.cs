using System;
using System.Collections.Generic;

namespace JSInterpreter
{
    public class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private readonly Dictionary<string, BindingRecord> bindings = new Dictionary<string, BindingRecord>();

        public override Completion CreateImmutableBinding(string name, bool strict)
        {
            if (bindings.ContainsKey(name))
                throw new InvalidOperationException("Spec 8.1.1.1.3 step 2");
            bindings.Add(name, BindingRecord.Immutable(strict: strict));
            return Completion.NormalCompletion();
        }

        public override Completion CreateMutableBinding(string name, bool deletable)
        {
            if (bindings.ContainsKey(name))
                throw new InvalidOperationException("Spec 8.1.1.1.2 step 2");
            bindings.Add(name, BindingRecord.Mutable(canDelete: deletable));
            return Completion.NormalCompletion();
        }

        public override BooleanCompletion DeleteBinding(string name)
        {
            if (!bindings.ContainsKey(name))
                throw new InvalidOperationException("Spec 8.1.1.1.7 step 2");
            if (!bindings[name].canDelete.GetValueOrDefault(false))
                return false;
            bindings.Remove(name);
            return true;
        }

        public override Completion GetBindingValue(string name, bool strict)
        {
            if (!bindings.ContainsKey(name))
                throw new InvalidOperationException("Spec 8.1.1.1.6 step 2");
            if (bindings[name].Value == null) return Completion.ThrowReferenceError();
            return Completion.NormalCompletion(bindings[name].Value);
        }

        public override BooleanCompletion HasBinding(string name)
        {
            return bindings.ContainsKey(name);
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
            if (!bindings.ContainsKey(name) || bindings[name].Value != null)
                throw new InvalidOperationException("Spec 8.1.1.1.4 step 2");
            bindings[name].Value = value;
            return Completion.NormalCompletion();
        }

        public override Completion SetMutableBinding(string name, IValue value, bool strict)
        {
            if (!bindings.ContainsKey(name))
            {
                if (strict)
                    return Completion.ThrowReferenceError();
                CreateMutableBinding(name, true);
                InitializeBinding(name, value);
                return Completion.NormalCompletion();
            }
            var binding = bindings[name];
            if (binding.strict.GetValueOrDefault(false)) strict = true;
            if (binding.Value == null) return Completion.ThrowReferenceError();
            else if (binding.mutable) binding.Value = value;
            else
            {
                // attempt to change value of immutable binding
                if (strict)
                    return Completion.ThrowTypeError();
            }
            return Completion.NormalCompletion();
        }
    }
}
