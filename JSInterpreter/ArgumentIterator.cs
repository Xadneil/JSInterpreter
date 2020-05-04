using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public class ArgumentIterator
    {
        private readonly IEnumerator<IValue> enumerator;
        public bool Done { get; private set; }

        public ArgumentIterator(IEnumerable<IValue> values)
        {
            enumerator = values.GetEnumerator();
            if (!values.Any())
                Done = true;
        }

        public IValue Next()
        {
            Done = !enumerator.MoveNext();
            return enumerator.Current;
        }

    }
}
