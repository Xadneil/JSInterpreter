using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    class Arguments
    {
        public readonly IReadOnlyList<IArgumentItem> argumentItems;

        public Arguments(IReadOnlyList<IArgumentItem> argumentItems)
        {
            this.argumentItems = argumentItems;
        }

        public (Completion, List<IValue>) ArgumentListEvaluation()
        {
            var argListEnumerable = argumentItems.Select(a => Utils.EvaluateArgument(Interpreter.Instance(), a));
            var argList = new List<IValue>();
            foreach (var (completion, args) in argListEnumerable)
            {
                if (completion.IsAbrupt()) return (completion, null);
                argList.AddRange(args);
            }
            return (Completion.NormalCompletion(), argList);
        }
    }

    interface IArgumentItem
    {
    }
}