using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public class Object : IValue
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

        public Completion ToJsString()
        {
            var prim = ((IValue)this).ToPrimitive(IValue.PrimitiveHint.String);
            if (prim.IsAbrupt()) return prim;
            return prim.value.ToJsString();
        }

        internal void AddCustomInternalSlots(IEnumerable<string> names)
        {
            foreach (var name in names)
                customInternalSlots.Add(name, null);
        }

        public virtual Completion ToNumber()
        {
            var prim = ((IValue)this).ToPrimitive(IValue.PrimitiveHint.Number);
            if (prim.IsAbrupt()) return prim;
            return prim.value.ToNumber();
        }

        internal bool HasInternalSlot(string name)
        {
            return customInternalSlots.ContainsKey(name);
        }

        internal object GetCustomInternalSlot(string name)
        {
            return customInternalSlots[name];
        }

        internal void SetCustomInternalSlot(string name, object value)
        {
            customInternalSlots[name] = value;
        }

        public BooleanCompletion HasOwnProperty(string name)
        {
            var descComp = GetOwnProperty(name);
            if (descComp.IsAbrupt()) return descComp.WithEmptyBool();
            var desc = descComp.Other;
            if (desc == null)
                return false;
            return true;
        }

        public bool IsExtensible { get; set; } = true;

        public bool PreventExtensions()
        {
            IsExtensible = false;
            return true;
        }

        public Completion GetPrototypeOf() => Completion.NormalCompletion((IValue)prototype ?? NullValue.Instance);

        public virtual BooleanCompletion SetPrototypeOf(IValue value)
        {
            return OrdinarySetPrototypeOf(value);
        }

        public bool OrdinarySetPrototypeOf(IValue value)
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
                if (p == null || p == NullValue.Instance)
                    done = true;
                else if (p == this)
                    return false;
                else
                    p = ((Object)p).prototype;
            }
            prototype = value == NullValue.Instance ? null : (value as Object);
            return true;
        }

        public virtual CompletionOr<PropertyDescriptor> GetOwnProperty(string P)
        {
            return Completion.NormalWith(OrdinaryGetOwnProperty(P));
        }

        public PropertyDescriptor OrdinaryGetOwnProperty(string name)
        {
            if (!propertyNames.TryGetValue(name, out int propertyIndex))
                return null;
            properties.TryGetValue(propertyIndex, out PropertyDescriptor X);
            var D = new PropertyDescriptor();
            if (X.IsDataDescriptor())
            {
                D.Value = X.Value;
                D.Writable = X.Writable;
            }
            else if (X.IsAccessorDescriptor())
            {
                D.Get = X.Get;
                D.Set = X.Set;
            }

            D.Enumerable = X.Enumerable;
            D.Configurable = X.Configurable;

            return D;
        }

        public virtual BooleanCompletion DefineOwnProperty(string name, PropertyDescriptor property)
        {
            return OrdinaryDefineOwnProperty(name, property);
        }

        public BooleanCompletion OrdinaryDefineOwnProperty(string P, PropertyDescriptor Desc)
        {
            var currentComp = GetOwnProperty(P);
            if (currentComp.IsAbrupt()) return currentComp.WithEmptyBool();
            var current = currentComp.Other;
            return ValidateAndApplyPropertyDescriptor(P, IsExtensible, Desc, current);
        }

        private bool ValidateAndApplyPropertyDescriptor(string name, bool extensible, PropertyDescriptor Desc, PropertyDescriptor current)
        {
            if (current == null)
            {
                if (!extensible)
                    return false;

                lastAddedIndex++;
                propertyNames[name] = lastAddedIndex;
                properties[lastAddedIndex] = Desc;

                #region Set default property descriptor values if not specified
                if (Desc.IsGenericDescriptor() || Desc.IsDataDescriptor())
                {
                    if (!Desc.Writable.HasValue)
                        Desc.Writable = false;
                }

                if (!Desc.Enumerable.HasValue)
                    Desc.Enumerable = false;
                if (!Desc.Configurable.HasValue)
                    Desc.Configurable = false;
                #endregion

                return true;
            }

            if (!Desc.Configurable.HasValue &&
                !Desc.Enumerable.HasValue &&
                Desc.Get == null &&
                Desc.Set == null &&
                Desc.Value == null &&
                !Desc.Writable.HasValue)
                return true;

            if (!current.Configurable.GetValueOrDefault())
            {
                if (Desc.Configurable.HasValue && Desc.Configurable.Value)
                    return false;
                if (Desc.Enumerable.HasValue && (Desc.Enumerable.Value != current.Enumerable.GetValueOrDefault()))
                    return false;
            }

            var index = propertyNames[name];

            if (Desc.IsGenericDescriptor())
            {
                return true;
            }
            else if (current.IsDataDescriptor() != Desc.IsDataDescriptor())
            {
                if (!current.Configurable.GetValueOrDefault())
                    return false;

                // "convert" from data to accessor or accessor to data by resetting all descriptor properties.
                // new properties will be added below.
                properties[index] = new PropertyDescriptor()
                {
                    Configurable = current.Configurable,
                    Enumerable = current.Enumerable
                };
                //Spec 9.1.6.3 step 2ci says to use default values, and default for writable is false
                if (Desc.IsDataDescriptor())
                {
                    properties[index].Writable = false;
                }
            }
            else if (current.IsDataDescriptor() && Desc.IsDataDescriptor())
            {
                if (!current.Configurable.GetValueOrDefault() && !current.Writable.GetValueOrDefault())
                {
                    if (Desc.Writable.HasValue && Desc.Writable.Value)
                        return false;
                    if (Desc.Value != null && Desc.Value != current.Value)
                        return false;
                    return true;
                }
            }
            else if (current.IsAccessorDescriptor() && Desc.IsAccessorDescriptor())
            {
                if (!current.Configurable.GetValueOrDefault())
                {
                    if (Desc.Set != null && Desc.Set != current.Set) return false;
                    if (Desc.Get != null && Desc.Get != current.Get) return false;
                    return true;
                }
            }

            if (Desc.Configurable.HasValue)
                properties[index].Configurable = Desc.Configurable;
            if (Desc.Enumerable.HasValue)
                properties[index].Enumerable = Desc.Enumerable;
            if (Desc.Get != null)
                properties[index].Get = Desc.Get;
            if (Desc.Set != null)
                properties[index].Set = Desc.Set;
            if (Desc.Value != null)
                properties[index].Value = Desc.Value;
            if (Desc.Writable.HasValue)
                properties[index].Writable = Desc.Writable;

            return true;
        }

        public BooleanCompletion DefinePropertyOrThrow(string name, PropertyDescriptor property)
        {
            var success = DefineOwnProperty(name, property);
            if (success.IsAbrupt()) return success;
            if (success.Other == false) return Completion.ThrowTypeError("DefinePropertyOrThrow failed").WithEmptyBool();
            return success;
        }

        public BooleanCompletion HasProperty(string name)
        {
            var hasOwnComp = GetOwnProperty(name);
            if (hasOwnComp.IsAbrupt()) return hasOwnComp.WithEmptyBool();
            var hasOwn = hasOwnComp.Other;
            if (hasOwn != null)
                return true;
            var parentComp = GetPrototypeOf();
            if (parentComp.IsAbrupt()) return parentComp.WithEmptyBool();
            var parent = parentComp.value;
            if (parent != NullValue.Instance && parent is Object o)
                return o.HasProperty(name);
            return false;
        }

        public Completion Get(string name)
        {
            return InternalGet(name, this);
        }

        public virtual Completion InternalGet(string name, IValue receiver)
        {
            return OrdinaryGet(name, receiver);
        }

        public Completion OrdinaryGet(string name, IValue receiver)
        {
            var desc = GetOwnProperty(name);
            if (desc.IsAbrupt()) return desc;
            var propertyDescriptor = desc.Other;
            if (propertyDescriptor == null)
            {
                var parentComp = GetPrototypeOf();
                if (parentComp.IsAbrupt()) return parentComp;
                var parent = parentComp.value;
                if (parent == NullValue.Instance)
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                return ((Object)parent).InternalGet(name, receiver);
            }
            if (propertyDescriptor.IsDataDescriptor())
            {
                return Completion.NormalCompletion(propertyDescriptor.Value);
            }
            if (!propertyDescriptor.IsAccessorDescriptor())
                throw new InvalidOperationException("Spec 9.1.8.1 Step 5");
            if (propertyDescriptor.Get == null)
                return Completion.NormalCompletion(UndefinedValue.Instance);
            return propertyDescriptor.Get.Call(receiver);
        }

        public BooleanCompletion Set(string P, IValue V, bool Throw)
        {
            var success = InternalSet(P, V, this);
            if (success.IsAbrupt()) return success;
            if (success.Other == false && Throw) return Completion.ThrowTypeError($"Set {P} failed").WithEmptyBool();
            return success;
        }

        public virtual BooleanCompletion InternalSet(string name, IValue value, IValue receiver)
        {
            return OrdinarySet(name, value, receiver);
        }

        public BooleanCompletion OrdinarySet(string name, IValue value, IValue receiver)
        {
            var ownDesc = GetOwnProperty(name);
            if (ownDesc.IsAbrupt()) return ownDesc.WithEmptyBool();
            return OrdinarySetWithOwnDescriptor(name, value, receiver, ownDesc.Other);
        }

        private BooleanCompletion OrdinarySetWithOwnDescriptor(string name, IValue value, IValue receiver, PropertyDescriptor ownDesc)
        {
            if (ownDesc == null)
            {
                var parentComp = GetPrototypeOf();
                if (parentComp.IsAbrupt()) return parentComp.WithEmptyBool();
                if (parentComp.value is Object parent)
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
                if (!ownDesc.Writable.Value)
                    return false;
                if (!(receiver is Object @object))
                    return false;
                var existingDescriptorComp = @object.GetOwnProperty(name);
                if (existingDescriptorComp.IsAbrupt()) return existingDescriptorComp.WithEmptyBool();
                var existingDescriptor = existingDescriptorComp.Other;
                if (existingDescriptor != null)
                {
                    if (!existingDescriptor.IsDataDescriptor())
                        return false;
                    if (!existingDescriptor.Writable.Value)
                        return false;
                    return @object.DefineOwnProperty(name, new PropertyDescriptor(value, null, null, null));
                }
                else
                {
                    return Utils.CreateDataProperty(receiver, name, value);
                }
            }

            if (!ownDesc.IsAccessorDescriptor())
                throw new InvalidOperationException("Spec 9.1.9.2 Step 4");
            if (ownDesc.Set == null)
                return false;
            var setComp = ownDesc.Set.Call(receiver, new[] { value });
            if (setComp.IsAbrupt()) return setComp.WithEmptyBool();

            return true;
        }

        public virtual BooleanCompletion InternalDelete(string name)
        {
            return OrdinaryDelete(name);
        }

        public BooleanCompletion OrdinaryDelete(string name)
        {
            var descComp = GetOwnProperty(name);
            if (descComp.IsAbrupt()) return descComp.WithEmptyBool();
            var desc = descComp.Other;
            if (desc == null)
                return true;
            if (desc.Configurable.GetValueOrDefault())
            {
                propertyNames.Remove(name, out int propertyIndex);
                properties.Remove(propertyIndex);
                return true;
            }
            return false;
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
                if (prop.Value.Enumerable.Value)
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

        public CompletionOr<IteratorRecord> EnumerateObjectProperties()
        {
            return Completion.NormalWith(IteratorRecord.FromEnumerable(AllPropertyKeys()));
        }

        public Completion GetMethod(string name)
        {
            var funcComp = Get(name);
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value;
            if (func == UndefinedValue.Instance || func == NullValue.Instance)
                return Completion.NormalCompletion(UndefinedValue.Instance);
            if (!(func is Callable))
                return Completion.ThrowTypeError($"GetMethod({name}) failed, it is not callable");
            return Completion.NormalCompletion(func);
        }

        public CompletionOr<IteratorRecord> GetIterator()
        {
            // assuming hint is sync
            var methodComp = GetMethod("@@iterator");
            if (methodComp.IsAbrupt()) return methodComp.WithEmpty<IteratorRecord>();
            var method = methodComp.value as Callable;
            var iteratorComp = method.Call(this);
            if (iteratorComp.IsAbrupt()) return iteratorComp.WithEmpty<IteratorRecord>();
            var iterator = iteratorComp.value;
            if (!(iterator is Object o))
                return Completion.ThrowTypeError("@@iterator method did not return an object").WithEmpty<IteratorRecord>();
            var nextMethodComp = o.Get("next");
            if (nextMethodComp.IsAbrupt()) return nextMethodComp.WithEmpty<IteratorRecord>();
            if (!(nextMethodComp.value is Callable c))
                return Completion.ThrowTypeError("iterator next is not callable").WithEmpty<IteratorRecord>();
            return Completion.NormalWith<IteratorRecord>(new IteratorRecord(o, c, false));
        }
    }
}
