using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class IteratorRecord
    {
        public readonly Object Iterator;
        public readonly Callable NextMethod;
        public bool Done;

        public IteratorRecord(Object iterator, Callable nextMethod, bool done)
        {
            Iterator = iterator;
            NextMethod = nextMethod;
            Done = done;
        }

        public Completion IteratorNext(IValue value = null)
        {
            Completion result;
            if (value == null)
                result = NextMethod.Call(Iterator);
            else
                result = NextMethod.Call(Iterator, new List<IValue>() { value });
            if (result.IsAbrupt()) return result;
            if (!(result.value is Object))
                return Completion.ThrowTypeError("iterator next must return an object.");
            return result;
        }

        public static Completion IteratorComplete(IValue iterResult)
        {
            if (!(iterResult is Object o))
                throw new InvalidOperationException("Spec 7.4.3 step 1");
            var comp = o.Get("done");
            if (comp.IsAbrupt()) return comp;
            return Completion.NormalCompletion(comp.value.ToBoolean());
        }

        public static Completion IteratorValue(IValue iterResult)
        {
            if (!(iterResult is Object o))
                throw new InvalidOperationException("Spec 7.4.4 step 1");
            return o.Get("value");
        }

        public Completion IteratorStep()
        {
            var result = IteratorNext();
            if (result.IsAbrupt()) return result;
            var done = IteratorComplete(result.value);
            if (done.IsAbrupt()) return done;
            if (done.value == BooleanValue.True)
                return Completion.NormalCompletion(BooleanValue.False);
            return result;
        }

        public Completion IteratorClose(Completion completion)
        {
            var @return = Iterator.GetMethod("return");
            if (@return.IsAbrupt()) return @return;
            if (@return.value == UndefinedValue.Instance)
                return completion;
            var innerResult = (@return.value as Callable).Call(Iterator);
            if (completion.completionType == CompletionType.Throw)
                return completion;
            if (innerResult.completionType == CompletionType.Throw)
                return innerResult;
            if (!(innerResult.value is Object))
                return Completion.ThrowTypeError("iterator return did not return an object.");
            return completion;
        }

        public static IteratorRecord FromEnumerable(IEnumerable<Completion> values)
        {
            var iterator = new EnumerableIteratorObject(values);
            return new IteratorRecord(iterator, iterator.GetMethod("next").value as Callable, false);
        }

        private class EnumerableIteratorObject : Object
        {
            private readonly IEnumerator<Completion> values;

            public EnumerableIteratorObject(IEnumerable<Completion> values)
            {
                this.values = values.GetEnumerator();
                DefineOwnProperty("next", new PropertyDescriptor(Utils.CreateBuiltinFunction(Next, Utils.EmptyList<string>()), false, false, false));
            }

            private Completion Next(IValue arg1, IReadOnlyList<IValue> arg2)
            {
                var hasNext = values.MoveNext();
                if (!hasNext)
                {
                    var doneRet = Utils.ObjectCreate(null);
                    doneRet.DefineOwnProperty("done", new PropertyDescriptor(BooleanValue.True, false, false, false));
                    return Completion.NormalCompletion(doneRet);
                }
                if (values.Current.IsAbrupt())
                    return values.Current;
                var ret = Utils.ObjectCreate(null);
                ret.DefineOwnProperty("value", new PropertyDescriptor(values.Current.value, false, false, false));
                ret.DefineOwnProperty("done", new PropertyDescriptor(BooleanValue.False, false, false, false));
                return Completion.NormalCompletion(ret);
            }
        }
    }
}
