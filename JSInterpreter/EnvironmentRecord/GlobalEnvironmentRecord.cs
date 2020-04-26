using System;
using System.Collections.Generic;

namespace JSInterpreter
{

    public class GlobalEnvironmentRecord : EnvironmentRecord
    {
        public readonly ObjectEnvironmentRecord ObjectRecord;
        public readonly Object GlobalThisValue;
        public readonly DeclarativeEnvironmentRecord DeclarativeRecord;
        public List<string> VarNames = new List<string>();

        public GlobalEnvironmentRecord(ObjectEnvironmentRecord objectRecord, Object globalThisValue, DeclarativeEnvironmentRecord declarativeRecord)
        {
            ObjectRecord = objectRecord;
            GlobalThisValue = globalThisValue;
            DeclarativeRecord = declarativeRecord;
        }

        public override Completion CreateImmutableBinding(string name, bool strict)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return Completion.ThrowTypeError($"binding name {name} is already bound");
            return DeclarativeRecord.CreateImmutableBinding(name, strict);
        }

        public override Completion CreateMutableBinding(string name, bool deletable)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return Completion.ThrowTypeError($"binding name {name} is already bound");
            return DeclarativeRecord.CreateMutableBinding(name, deletable);
        }

        public override BooleanCompletion DeleteBinding(string name)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return DeclarativeRecord.DeleteBinding(name);
            var globalObject = ObjectRecord.BindingObject;
            var existingPropComp = globalObject.HasOwnProperty(name);
            if (existingPropComp.IsAbrupt()) return existingPropComp;
            var existingProp = existingPropComp.Other;
            if (existingProp)
            {
                var statusComp = ObjectRecord.DeleteBinding(name);
                if (statusComp.IsAbrupt()) return statusComp.WithEmptyBool();
                var status = statusComp.Other;
                if (status)
                {
                    if (VarNames.Contains(name))
                        VarNames.Remove(name);
                }
                return statusComp;
            }
            return true;
        }

        public override Completion GetBindingValue(string name, bool strict)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return DeclarativeRecord.GetBindingValue(name, strict);
            return ObjectRecord.GetBindingValue(name, strict);
        }

        public override BooleanCompletion HasBinding(string name)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return true;
            return ObjectRecord.HasBinding(name);
        }

        public bool HasLexicalDeclaration(string name)
        {
            return DeclarativeRecord.HasBinding(name).Other;
        }

        public BooleanCompletion HasRestrictedGlobalProperty(string name)
        {
            var existingPropComp = ObjectRecord.BindingObject.GetOwnProperty(name);
            if (existingPropComp.IsAbrupt()) return existingPropComp.WithEmptyBool();
            var existingProp = existingPropComp.Other;
            if (existingProp == null)
                return false;
            if (existingProp.Configurable.GetValueOrDefault())
                return false;
            return true;
        }

        public bool HasVarDeclaration(string name)
        {
            return VarNames.Contains(name);
        }

        public override bool HasSuperBinding()
        {
            return false;
        }

        public override bool HasThisBinding()
        {
            return true;
        }

        public Object GetThisBinding()
        {
            return GlobalThisValue;
        }

        public override Completion InitializeBinding(string name, IValue value)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return DeclarativeRecord.InitializeBinding(name, value);
            if (ObjectRecord.HasBinding(name).Other == false)
                throw new InvalidOperationException("Spec 8.1.1.4.4 step 4");
            return ObjectRecord.InitializeBinding(name, value);
        }

        public override Completion SetMutableBinding(string name, IValue value, bool strict)
        {
            if (DeclarativeRecord.HasBinding(name).Other == true)
                return DeclarativeRecord.SetMutableBinding(name, value, strict);
            return ObjectRecord.SetMutableBinding(name, value, strict);
        }

        public BooleanCompletion CanDeclareGlobalVar(string n)
        {
            var hasProperty = ObjectRecord.BindingObject.HasOwnProperty(n);
            if (hasProperty.IsAbrupt()) return hasProperty;
            if (hasProperty.Other == true) return true;
            return ObjectRecord.BindingObject.IsExtensible;
        }

        public BooleanCompletion CanDeclareGlobalFunction(string n)
        {
            var existingPropComp = ObjectRecord.BindingObject.GetOwnProperty(n);
            if (existingPropComp.IsAbrupt()) return existingPropComp.WithEmptyBool();
            var existingProp = existingPropComp.Other;
            if (existingProp == null)
                return ObjectRecord.BindingObject.IsExtensible;
            if (existingProp.Configurable.GetValueOrDefault() == true)
                return true;
            if (existingProp.IsDataDescriptor() && existingProp.Writable.GetValueOrDefault() && existingProp.Enumerable.GetValueOrDefault())
                return true;
            return false;
        }

        public Completion CreateGlobalVarBinding(string n, bool d)
        {
            var hasProperty = ObjectRecord.BindingObject.HasOwnProperty(n);
            if (hasProperty.IsAbrupt()) return hasProperty;
            var extensible = ObjectRecord.BindingObject.IsExtensible;
            if (hasProperty.Other == false && extensible == true)
            {
                var comp = ObjectRecord.CreateMutableBinding(n, d);
                if (comp.IsAbrupt()) return comp;
                comp = ObjectRecord.InitializeBinding(n, UndefinedValue.Instance);
                if (comp.IsAbrupt()) return comp;
            }
            if (!VarNames.Contains(n))
                VarNames.Add(n);
            return Completion.NormalCompletion();
        }

        public Completion CreateGlobalFunctionBinding(string n, IValue v, bool d)
        {
            var existingPropComp = ObjectRecord.BindingObject.GetOwnProperty(n);
            if (existingPropComp.IsAbrupt()) return existingPropComp.WithEmptyBool();
            var existingProp = existingPropComp.Other;
            PropertyDescriptor desc;
            if (existingProp == null || existingProp.Configurable.GetValueOrDefault())
                desc = new PropertyDescriptor(v, true, true, d);
            else
                desc = new PropertyDescriptor() { Value = v };
            Completion comp = ObjectRecord.BindingObject.DefinePropertyOrThrow(n, desc);
            if (comp.IsAbrupt()) return comp;
            //TODO mark n as initialized in ObjectRecord
            comp = ObjectRecord.BindingObject.Set(n, v, false);
            if (comp.IsAbrupt()) return comp;
            if (!VarNames.Contains(n))
                VarNames.Add(n);
            return Completion.NormalCompletion();
        }
    }
}
