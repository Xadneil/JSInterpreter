using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class DateInstance : Object
    {
        // Maximum allowed value to prevent DateTime overflow
        private static readonly double Max = (DateTime.MaxValue - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        // Minimum allowed value to prevent DateTime overflow
        private static readonly double Min = -(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) - DateTime.MinValue).TotalMilliseconds;

        public double PrimitiveValue { get; set; }

        internal bool DateTimeRangeValid => !double.IsNaN(PrimitiveValue) && PrimitiveValue <= Max && PrimitiveValue >= Min;

        public DateInstance()
        {
            PrimitiveValue = double.NaN;
        }

        public CompletionOr<DateTime> ToDateTime()
        {
            return DateTimeRangeValid
                ? Completion.NormalWithStruct(DateConstructor.Epoch.AddMilliseconds(PrimitiveValue))
                : Completion.ThrowRangeError("Date is out of range").WithEmpty<DateTime>();
        }
    }
}
