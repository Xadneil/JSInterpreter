using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class FunctionConstructor : FunctionObject
    {
        public FunctionConstructor(ObjectPrototype objectPrototype)
        {
            prototype = new FunctionPrototype(objectPrototype);
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

        }

        public override Completion InternalCall(IValue @this, IReadOnlyList<IValue> arguments)
        {
            return CreateDynamicFunction(null, FunctionKind.Normal, arguments);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            return CreateDynamicFunction(newTarget, FunctionKind.Normal, arguments);
        }

        private Completion CreateDynamicFunction(Object? newTarget, FunctionKind kind, IReadOnlyList<IValue> arguments)
        {
            if (Interpreter.Instance().ExecutionContextStackSize() < 2)
                throw new InvalidOperationException("Spec 19.2.1.1.1 step 1");
            var callerContext = Interpreter.Instance().SecondExecutionContext();
            var callerRealm = callerContext.Realm;
            var calleeRealm = Interpreter.Instance().CurrentRealm();
            //TODO HostEnsureCanCompileStrings
            if (newTarget == null)
                newTarget = this;
            Func<Parser.Parser, AST.FunctionStatementList> goal;
            Func<Intrinsics, Object> fallbackProto;
            switch (kind)
            {
                case FunctionKind.Normal:
                    goal = (p) => p.ParseFunctionBody();
                    fallbackProto = i => i.FunctionPrototype;
                    break;
                default:
                    throw new NotImplementedException("Async and/or generators");
            }
            var argCount = arguments.Count;
            var P = "";
            string bodyText;
            if (argCount == 0)
                bodyText = "";
            else if (argCount == 1)
            {
                var bodyComp = arguments[0].ToJsString();
                if (bodyComp.IsAbrupt()) return bodyComp;
                bodyText = (bodyComp.value as StringValue)!.@string;
            }
            else
            {
                var firstArg = arguments[0];
                var pComp = firstArg.ToJsString();
                if (pComp.IsAbrupt()) return pComp;
                P = (pComp.value as StringValue)!.@string;
                int k = 1;
                for (; k < argCount - 1; k++)
                {
                    var nextArg = arguments[k];
                    var nextArgStringComp = nextArg.ToJsString();
                    if (nextArgStringComp.IsAbrupt()) return nextArgStringComp;
                    var nextArgString = (nextArgStringComp.value as StringValue)!.@string;
                    P += "," + nextArgString;
                }
                var bodyComp = arguments[k].ToJsString();
                if (bodyComp.IsAbrupt()) return bodyComp;
                bodyText = (bodyComp.value as StringValue)!.@string;
            }
            AST.FormalParameters parameters = new AST.FormalParameters();
            try
            {
                if (!string.IsNullOrEmpty(P))
                    parameters = new Parser.Parser(P).ParseFormalParameters()!;
            }
            catch (Parser.ParseFailureException e)
            {
                return Completion.ThrowSyntaxError($"Failed to parse parameters \"{P}\".\n{e.Message}");
            }
            AST.FunctionStatementList body;
            try
            {
                body = goal(new Parser.Parser(bodyText));
            }
            catch (Parser.ParseFailureException e)
            {
                return Completion.ThrowSyntaxError($"Failed to parse body \"{bodyText}\".\n{e.Message}");
            }
            //TODO detect strict mode: ContainsUseStrict
            bool strict = false;
            if (!parameters.IsSimpleParameterList() && strict)
                return Completion.ThrowSyntaxError($"parameters must be simple in strict mode. \"{P}\"");
            //TODO implement tree walking for checking if parameters or body contains a SuperCall or SuperProperty
            //TODO generator yield, async await errors
            var protoComp = Utils.GetPrototypeFromConstructor(newTarget, fallbackProto);
            if (protoComp.IsAbrupt()) return protoComp;
            var proto = protoComp.value;
            var F = FunctionObject.FunctionAllocate(proto!, strict, kind);
            var realmF = F.Realm;
            var scope = realmF.GlobalEnv;
            FunctionObject.FunctionInitialize(F, FunctionCreateKind.Normal, parameters, body, scope);
            //TODO generator, async generator
            if (kind == FunctionKind.Normal)
                F.MakeConstructor();
            F.SetFunctionName("anonymous");
            return Completion.NormalCompletion(F);
        }
    }
}
