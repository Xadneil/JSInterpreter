using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    class Object : IValue
    {
        protected readonly SortedDictionary<int, PropertyDescriptor> properties;
        protected int lastAddedIndex = -1;
        protected readonly Dictionary<string, int> propertyNames;
        public Object prototype;
        private readonly Dictionary<string, object> customInternalSlots;

        public Object()
        {
            properties = new SortedDictionary<int, PropertyDescriptor>();
            propertyNames = new Dictionary<string, int>();
            customInternalSlots = new Dictionary<string, object>();
        }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToObject() => Completion.NormalCompletion(this);

        internal void AddCustomInternalSlots(IEnumerable<string> names)
        {
            foreach (var name in names)
                customInternalSlots.Add(name, null);
        }

        public virtual NumberValue ToNumber()
        {
            throw new NotImplementedException();
        }

        internal object GetCustomInternalSlot(string name)
        {
            return customInternalSlots[name];
        }

        internal void SetCustomInternalSlot(string name, object value)
        {
            customInternalSlots[name] = value;
        }

        public Completion HasOwnProperty(string name)
        {
            var descComp = GetOwnProperty(name);
            if (descComp.IsAbrupt()) return descComp.completion;
            var desc = descComp.propertyDescriptor;
            if (desc == null)
                return Completion.NormalCompletion(BooleanValue.False);
            return Completion.NormalCompletion(BooleanValue.True);
        }

        public bool IsExtensible { get; set; } = true;

        public bool PreventExtensions()
        {
            IsExtensible = false;
            return true;
        }

        public Completion GetPrototypeOf() => Completion.NormalCompletion((IValue)prototype ?? NullValue.Instance);

        public bool SetPrototypeOf(IValue value)
        {
            if (!(value is NullValue) && !(value is Object))
                throw new InvalidOperationException("Object.SetPrototypeOf: Only an object or null is allowed as a prototype.");
            if (prototype == value)
                return true;
            if (!IsExtensible)
                return false;
            var p = value;
            bool done = false;
            // ensure no prototype loops
            while (!done)
            {
                if (p == NullValue.Instance)
                    done = true;
                else if (p == this)
                    return false;
                else
                    p = ((Object)p).prototype;
            }
            prototype = value == NullValue.Instance ? null : (value as Object);
            return true;
        }

        public virtual PropertyDescriptorCompletion GetOwnProperty(string P)
        {
            return OrdinaryGetOwnProperty(P);
        }

        public PropertyDescriptorCompletion OrdinaryGetOwnProperty(string name)
        {
            if (!propertyNames.TryGetValue(name, out int propertyIndex))
                return PropertyDescriptorCompletion.NormalCompletion(UndefinedValue.Instance);
            properties.TryGetValue(propertyIndex, out PropertyDescriptor X);
            PropertyDescriptor D;
            if (X.IsDataDescriptor())
            {
                D = new PropertyDescriptor(X.Value, X.writeable, X.enumerable, X.configurable);
            }
            else if (X.IsAccessorDescriptor())
            {
                throw new NotImplementedException("Accessor properties not implemented.");
            }
            else
            {
                throw new NotImplementedException("Generic properties not implemented.");
            }

            return PropertyDescriptorCompletion.NormalCompletion(D);
        }

        public virtual Completion DefineOwnProperty(string name, PropertyDescriptor property)
        {
            return OrdinaryDefineOwnProperty(name, property);
        }

        public Completion OrdinaryDefineOwnProperty(string P, PropertyDescriptor Desc)
        {
            var currentComp = GetOwnProperty(P);
            if (currentComp.IsAbrupt()) return currentComp.completion;
            var current = currentComp.propertyDescriptor;
            return Completion.NormalCompletion(ValidateAndApplyPropertyDescriptor(P, IsExtensible, Desc, current) ? BooleanValue.True : BooleanValue.False);
        }

        private bool ValidateAndApplyPropertyDescriptor(string name, bool extensible, PropertyDescriptor property, PropertyDescriptor current)
        {
            if (current == null)
            {
                if (!extensible)
                    return false;
                if (property.IsDataDescriptor())
                {
                    lastAddedIndex++;
                    propertyNames[name] = lastAddedIndex;
                    properties[lastAddedIndex] = PropertyDescriptor.FillDefaultValues(property);
                }
                else
                {
                    throw new NotImplementedException("Object.DefineOwnProperty: AssessorDescriptors not implemented");
                }
                return true;
            }
            if (!current.configurable.Value)
            {
                if (property.configurable.HasValue && property.configurable.Value)
                    return false;
                if (property.enumerable.HasValue && (property.enumerable.Value != current.enumerable.Value))
                    return false;
            }
            else if (current.IsDataDescriptor() != property.IsDataDescriptor())
            {
                if (!current.configurable.Value)
                    return false;
                if (current.IsDataDescriptor())
                {
                    // convert from data to accessor
                    throw new NotImplementedException("Object.DefineOwnProperty: AssessorDescriptors not implemented");
                }
                else
                {
                    // convert from accessor to data
                    throw new NotImplementedException("Object.DefineOwnProperty: AssessorDescriptors not implemented");
                }
            }
            else if (current.IsDataDescriptor() && property.IsDataDescriptor())
            {
                if (!current.configurable.Value && !current.writeable.Value)
                {
                    if (property.writeable.HasValue && property.writeable.Value)
                        return false;
                    if (property.Value != null && property.Value != current.Value)
                        return false;
                    return true;
                }
            }
            else if (current.IsAccessorDescriptor() && property.IsAccessorDescriptor())
            {
                throw new NotImplementedException("Object.DefineOwnProperty: AssessorDescriptors not implemented");
            }

            int propertyIndex = propertyNames[name];
            properties[propertyIndex] = new PropertyDescriptor(properties[propertyIndex], property);

            return true;
        }

        public Completion DefineOwnPropertyOrThrow(string name, PropertyDescriptor property)
        {
            var success = DefineOwnProperty(name, property);
            if (success.IsAbrupt()) return success;
            if (success.value == BooleanValue.False) return Completion.ThrowTypeError();
            return success;
        }

        public Completion HasProperty(string name)
        {
            var hasOwnComp = GetOwnProperty(name);
            if (hasOwnComp.IsAbrupt()) return hasOwnComp.completion;
            var hasOwn = hasOwnComp.completion.value;
            if (hasOwn != UndefinedValue.Instance)
                return Completion.NormalCompletion(BooleanValue.True);
            var parentComp = GetPrototypeOf();
            if (parentComp.IsAbrupt()) return parentComp;
            var parent = parentComp.value;
            if (parent != NullValue.Instance && parent is Object o)
                return o.HasProperty(name);
            return Completion.NormalCompletion(BooleanValue.False);
        }

        public Completion Get(string name)
        {
            return InternalGet(name, this);
        }

        internal Completion InternalGet(string name, IValue receiver)
        {
            var desc = GetOwnProperty(name);
            if (desc.IsAbrupt()) return desc.completion;
            if (desc.propertyDescriptor == null)
            {
                var parentComp = GetPrototypeOf();
                if (parentComp.IsAbrupt()) return parentComp;
                var parent = parentComp.value;
                if (parent == NullValue.Instance)
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                return ((Object)parent).InternalGet(name, receiver);
            }
            if (desc.propertyDescriptor.IsDataDescriptor())
            {
                return Completion.NormalCompletion(desc.propertyDescriptor.Value);
            }
            throw new NotImplementedException("Object.Get: AccessorDescriptors not implemented");
        }

        public Completion Set(string P, IValue V, bool Throw)
        {
            var success = InternalSet(P, V, this);
            if (success.IsAbrupt()) return success;
            if (success.value == BooleanValue.False && Throw) return Completion.ThrowTypeError();
            return success;
        }

        public Completion InternalSet(string name, IValue value, IValue receiver)
        {
            var ownDesc = GetOwnProperty(name);
            if (ownDesc.IsAbrupt()) return ownDesc.completion;
            return OrdinarySetWithOwnDescriptor(name, value, receiver, ownDesc.propertyDescriptor);
        }

        private Completion OrdinarySetWithOwnDescriptor(string name, IValue value, IValue receiver, PropertyDescriptor ownDesc)
        {
            if (ownDesc == null)
            {
                var parentComp = GetPrototypeOf();
                if (parentComp.IsAbrupt()) return parentComp;
                var parent = parentComp.value as Object;
                if (parent != null)
                {
                    return parent.InternalSet(name, value, receiver);
                }
                else
                {
                    ownDesc = new PropertyDescriptor(UndefinedValue.Instance, true, true, true);
                }
            }
            if (ownDesc.IsDataDescriptor())
            {
                if (!ownDesc.writeable.Value)
                    return Completion.NormalCompletion(BooleanValue.False);
                if (!(receiver is Object @object))
                    return Completion.NormalCompletion(BooleanValue.False);
                var existingDescriptorComp = @object.GetOwnProperty(name);
                if (existingDescriptorComp.IsAbrupt()) return existingDescriptorComp.completion;
                var existingDescriptor = existingDescriptorComp.propertyDescriptor;
                if (existingDescriptor != null)
                {
                    if (!existingDescriptor.IsDataDescriptor())
                        return Completion.NormalCompletion(BooleanValue.False);
                    if (!existingDescriptor.writeable.Value)
                        return Completion.NormalCompletion(BooleanValue.False);
                    return @object.DefineOwnProperty(name, new PropertyDescriptor(value, null, null, null));
                }
                else
                {
                    return Utils.CreateDataProperty(receiver, name, value);
                }
            }
            throw new NotImplementedException("Object.Set: AccessorDescriptors not implemented");
        }

        public Completion Delete(string name)
        {
            var descComp = GetOwnProperty(name);
            if (descComp.IsAbrupt()) return descComp.completion;
            var desc = descComp.propertyDescriptor;
            if (desc == null)
                return Completion.NormalCompletion(BooleanValue.True);
            if (desc.configurable.Value)
            {
                propertyNames.Remove(name, out int propertyIndex);
                properties.Remove(propertyIndex);
                return Completion.NormalCompletion(BooleanValue.True);
            }
            return Completion.NormalCompletion(BooleanValue.False);
        }

        public IReadOnlyList<string> OwnPropertyKeys()
        {
            var keys = new Stack<string>();
            var keysWithIsNumeric = propertyNames.Select(kvp => (kvp, isNumeric: int.TryParse(kvp.Key, out int index), index));
            foreach (var (kvp, isNumeric, index) in keysWithIsNumeric.Where(t => t.isNumeric).OrderBy(t => t.index))
            {
                keys.Push(kvp.Key);
            }
            foreach (var (kvp, isNumeric, index) in keysWithIsNumeric.Where(t => !t.isNumeric).OrderBy(t => t.kvp.Value))
            {
                keys.Push(kvp.Key);
            }
            return keys.ToList();
        }

        public IEnumerable<Completion> AllPropertyKeys()
        {
            var visited = new HashSet<string>();
            foreach (var prop in properties)
            {
                var key = propertyNames.FirstOrDefault(p => p.Value == prop.Key).Key;
                visited.Add(key);
                if (prop.Value.enumerable.Value)
                    yield return Completion.NormalCompletion(new StringValue(key));
            }
            var protoComp = GetPrototypeOf();
            if (protoComp.IsAbrupt())
            {
                yield return protoComp;
                yield break;
            }
            var proto = protoComp.value;
            if (proto == NullValue.Instance)
                yield break;
            foreach (var protoKeyComp in (proto as Object).AllPropertyKeys())
            {
                if (protoKeyComp.IsAbrupt())
                {
                    yield return protoKeyComp;
                    yield break;
                }
                var protoKey = protoKeyComp.value as StringValue;
                if (!visited.Contains(protoKey.@string))
                    yield return Completion.NormalCompletion(protoKey);
            }
        }

        public (Completion, IEnumerator<Completion>) EnumerateObjectProperties()
        {
            return (Completion.NormalCompletion(), AllPropertyKeys().GetEnumerator());
        }

        public Completion GetMethod(string name)
        {
            var funcComp = Get(name);
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value;
            if (func == UndefinedValue.Instance || func == NullValue.Instance)
                return Completion.NormalCompletion(UndefinedValue.Instance);
            if (!(func is Callable))
                return Completion.ThrowTypeError();
            return Completion.NormalCompletion(func);
        }

        public (Completion, IEnumerator<Completion>) GetIterator()
        {
            // assuming hint is sync
            var methodComp = GetMethod("@@iterator");
            if (methodComp.IsAbrupt()) return (methodComp, null);
            var method = methodComp.value as Callable;
            var iteratorComp = method.Call(this);
            if (iteratorComp.IsAbrupt()) return (iteratorComp, null);
            var iterator = iteratorComp.value;
            if (!(iterator is Object o))
                return (Completion.ThrowTypeError(), null);
            var nextMethodComp = o.Get("next");
            if (nextMethodComp.IsAbrupt()) return (nextMethodComp, null);
            if (!(nextMethodComp.value is Callable c))
                return (Completion.ThrowTypeError(), null);
            return (Completion.NormalCompletion(), new ObjectIteratorRecord(o, c));
        }
    }
}
