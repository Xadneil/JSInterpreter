using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IMethodDefinition : IPropertyDefinition
    {
    }

    public sealed class MethodDefinition : IMethodDefinition
    {
        public readonly string propertyName;
        public readonly FormalParameters formalParameters;
        public readonly FunctionStatementList functionBody;

        public MethodDefinition(string propertyName, FormalParameters formalParameters, FunctionStatementList functionBody)
        {
            this.propertyName = propertyName;
            this.formalParameters = formalParameters;
            this.functionBody = functionBody;
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            var methodDefComp = DefineMethod(@object);
            if (methodDefComp.IsAbrupt()) return methodDefComp;
            var methodDef = methodDefComp.Other;
            methodDef.Closure.SetFunctionName(methodDef.Key);
            var desc = new PropertyDescriptor(methodDef.Closure, true, enumerable, true);
            return @object.DefinePropertyOrThrow(methodDef.Key, desc);
        }

        private CompletionOr<(string Key, FunctionObject Closure)> DefineMethod(Object @object, IValue? functionPrototype = null)
        {
            var strict = functionBody.IsStrictMode;
            var scope = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
            FunctionObject.FunctionCreateKind kind;
            IValue prototype;
            if (functionPrototype != null)
            {
                kind = FunctionObject.FunctionCreateKind.Normal;
                prototype = functionPrototype;
            }
            else
            {
                kind = FunctionObject.FunctionCreateKind.Method;
                prototype = Interpreter.Instance().CurrentRealm().Intrinsics.FunctionPrototype;
            }
            var closure = FunctionObject.FunctionCreate(kind, formalParameters, functionBody, scope, strict, prototype);
            closure.MakeMethod(@object);
            return Completion.NormalWithStruct((propertyName, closure));
        }
    }

    public sealed class Getter : IMethodDefinition
    {
        public readonly string propertyName;
        public readonly FunctionStatementList functionBody;

        public Getter(string propertyName, FunctionStatementList functionBody)
        {
            this.propertyName = propertyName;
            this.functionBody = functionBody;
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            var strict = functionBody.IsStrictMode;
            var scope = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
            var closure = FunctionObject.FunctionCreate(FunctionObject.FunctionCreateKind.Method, new FormalParameters(), functionBody, scope, strict);
            closure.MakeMethod(@object);
            closure.SetFunctionName(propertyName, "get");
            var desc = new PropertyDescriptor(closure, null, enumerable, true);
            return @object.DefinePropertyOrThrow(propertyName, desc);
        }
    }

    public sealed class Setter : IMethodDefinition
    {
        public readonly string propertyName;
        public readonly FormalParameter setParameter;
        public readonly FunctionStatementList functionBody;

        public Setter(string propertyName, FormalParameter setParameter, FunctionStatementList functionBody)
        {
            this.propertyName = propertyName;
            this.setParameter = setParameter;
            this.functionBody = functionBody;
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            var strict = functionBody.IsStrictMode;
            var scope = Interpreter.Instance().RunningExecutionContext().LexicalEnvironment;
            var closure = FunctionObject.FunctionCreate(FunctionObject.FunctionCreateKind.Method, new FormalParameters(new List<FormalParameter>(1) { setParameter }), functionBody, scope, strict);
            closure.MakeMethod(@object);
            closure.SetFunctionName(propertyName, "set");
            var desc = new PropertyDescriptor(closure, null, enumerable, true);
            return @object.DefinePropertyOrThrow(propertyName, desc);
        }
    }
}
