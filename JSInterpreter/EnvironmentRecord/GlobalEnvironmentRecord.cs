using System;
using System.Collections.Generic;

namespace JSInterpreter
{

    class GlobalEnvironmentRecord : EnvironmentRecord
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
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return Completion.ThrowTypeError();
            return DeclarativeRecord.CreateImmutableBinding(name, strict);
        }

        public override Completion CreateMutableBinding(string name, bool deletable)
        {
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return Completion.ThrowTypeError();
            return DeclarativeRecord.CreateMutableBinding(name, deletable);
        }

        public override Completion DeleteBinding(string name)
        {
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return DeclarativeRecord.DeleteBinding(name);
            var globalObject = ObjectRecord.BindingObject;
            var existingPropComp = globalObject.HasOwnProperty(name);
            if (existingPropComp.IsAbrupt()) return existingPropComp;
            var existingProp = (existingPropComp.value as BooleanValue).boolean;
            if (existingProp)
            {
                var statusComp = ObjectRecord.DeleteBinding(name);
                if (statusComp.IsAbrupt()) return statusComp;
                var status = statusComp.value as BooleanValue;
                if (status.boolean)
                {
                    if (VarNames.Contains(name))
                        VarNames.Remove(name);
                }
                return statusComp;
            }
            return Completion.NormalCompletion(BooleanValue.True);
        }

        public override Completion GetBindingValue(string name, bool strict)
        {
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return DeclarativeRecord.GetBindingValue(name, strict);
            return ObjectRecord.GetBindingValue(name, strict);
        }

        public override Completion HasBinding(string name)
        {
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return Completion.NormalCompletion(BooleanValue.True);
            return ObjectRecord.HasBinding(name);
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
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return DeclarativeRecord.InitializeBinding(name, value);
            if (ObjectRecord.HasBinding(name).value != BooleanValue.True)
                throw new InvalidOperationException("Spec 8.1.1.4.4 step 4");
            return ObjectRecord.InitializeBinding(name, value);
        }

        public override Completion SetMutableBinding(string name, IValue value, bool strict)
        {
            if (DeclarativeRecord.HasBinding(name).value == BooleanValue.True)
                return DeclarativeRecord.SetMutableBinding(name, value, strict);
            return ObjectRecord.SetMutableBinding(name, value, strict);
        }
    }
}
