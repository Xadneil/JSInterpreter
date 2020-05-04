using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class MappedArguments : Object
    {
        private readonly Object ParameterMap;

        public MappedArguments(Object parameterMap)
        {
            ParameterMap = parameterMap;
            AddCustomInternalSlots(new[] { "ParameterMap" });
        }

        public override CompletionOr<PropertyDescriptor> GetOwnProperty(string P)
        {
            var desc = OrdinaryGetOwnProperty(P);
            if (desc == null)
                return Completion.NormalWith(desc);
            var isMapped = ParameterMap.HasOwnProperty(P).Other;
            if (isMapped)
                desc.Value = ParameterMap.Get(P).value;
            return Completion.NormalWith(desc);
        }

        public override BooleanCompletion DefineOwnProperty(string P, PropertyDescriptor Desc)
        {
            var isMapped = ParameterMap.HasOwnProperty(P).Other;
            var newArgDesc = Desc;
            if (isMapped && Desc.IsDataDescriptor())
            {
                if (Desc.Value == null && Desc.Writable.HasValue && Desc.Writable.Value == false)
                {
                    newArgDesc = Desc.Copy();
                    newArgDesc.Value = ParameterMap.Get(P).value;
                }
            }
            var allowed = OrdinaryDefineOwnProperty(P, newArgDesc);
            if (allowed.IsAbrupt()) return allowed;
            if (!allowed.Other)
                return false;
            if (isMapped)
            {
                if (Desc.IsAccessorDescriptor())
                    ParameterMap.InternalDelete(P);
                else
                {
                    if (Desc.Value != null)
                    {
                        var setStatus = ParameterMap.Set(P, Desc.Value, false);
                        if (!setStatus.Other)
                            throw new InvalidOperationException("formal parameters mapped by argument objscts must always be writable");
                    }
                    if (Desc.Writable.GetValueOrDefault())
                        ParameterMap.InternalDelete(P);
                }
            }
            return true;
        }

        public override Completion InternalGet(string name, IValue receiver)
        {
            bool isMapped = ParameterMap.HasOwnProperty(name).Other;
            if (!isMapped)
                return OrdinaryGet(name, receiver);
            else
                return ParameterMap.Get(name);
        }

        public override BooleanCompletion InternalSet(string name, IValue value, IValue receiver)
        {
            bool isMapped;
            if (receiver != this)
                isMapped = false;
            else
                isMapped = ParameterMap.HasOwnProperty(name).Other;
            if (isMapped)
            {
                var setStatus = ParameterMap.Set(name, value, false);
                if (!setStatus.Other)
                    throw new InvalidOperationException("formal parameters mapped by argument objscts must always be writable");
            }
            return OrdinarySet(name, value, receiver);
        }

        public override BooleanCompletion InternalDelete(string name)
        {
            bool isMapped = ParameterMap.HasOwnProperty(name).Other;
            var result = ParameterMap.OrdinaryDelete(name);
            if (result.IsAbrupt()) return result;
            if (result.Other && isMapped)
                ParameterMap.InternalDelete(name);
            return result;
        }
    }
}
