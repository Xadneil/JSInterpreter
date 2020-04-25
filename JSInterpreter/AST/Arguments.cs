using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    public class Arguments
    {
        public readonly IReadOnlyList<IArgumentItem> argumentItems;

        public Arguments(IReadOnlyList<IArgumentItem> argumentItems)
        {
            this.argumentItems = argumentItems;
        }

        public CompletionOr<List<IValue>> ArgumentListEvaluation()
        {
            var argListEnumerable = argumentItems.Select(a => Utils.EvaluateArgument(Interpreter.Instance(), a));
            var argList = new List<IValue>();
            foreach (var args in argListEnumerable)
            {
                if (args.IsAbrupt()) return args;
                argList.AddRange(args.Other);
            }
            return Completion.NormalWith(argList);
        }
    }

    public interface IArgumentItem
    {
    }
}