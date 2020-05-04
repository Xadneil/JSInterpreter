using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public class FunctionDeclaration : HoistableDeclaration, ILabelledItem, IScopedDeclaration
    {
        public readonly bool isAnonymous;
        public readonly Identifier identifier;
        public readonly FormalParameters formalParameters;
        public readonly FunctionStatementList functionBody;

        public FunctionDeclaration(FormalParameters formalParameters, FunctionStatementList functionBody)
        {
            isAnonymous = true;
            this.formalParameters = formalParameters;
            this.functionBody = functionBody;
        }

        public FunctionDeclaration(Identifier identifier, FormalParameters formalParameters, FunctionStatementList functionBody)
        {
            isAnonymous = false;
            this.identifier = identifier;
            this.formalParameters = formalParameters;
            this.functionBody = functionBody;
        }

        public override IReadOnlyList<string> BoundNames()
        {
            if (isAnonymous)
            {
                return new List<string>(1) { "*default*" };
            }
            else
            {
                return new List<string>(1) { identifier.name };
            }
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return Completion.NormalCompletion();
        }

        public override bool IsConstantDeclaration()
        {
            return false;
        }

        public Completion LabelledEvaluate(Interpreter interpreter, List<string> labelSet)
        {
            return Evaluate(interpreter);
        }

        public override IReadOnlyList<IDeclarationPart> LexicallyScopedDeclarations()
        {
            return new[] { this }.ToList();
        }

        internal FunctionObject InstantiateFunctionObject(LexicalEnvironment scope)
        {
            //TODO: scan body code for strict directive
            bool strict = false;
            var F = FunctionObject.FunctionCreate(FunctionObject.FunctionCreateKind.Normal, formalParameters, functionBody, scope, strict);
            F.MakeConstructor();
            F.SetFunctionName(identifier.name);
            return F;
        }

    }

    public class FunctionExpression : IPrimaryExpression
    {
        public readonly bool isAnonymous;
        public readonly Identifier identifier;
        public readonly FormalParameters formalParameters;
        public readonly FunctionStatementList functionBody;

        public FunctionExpression(FormalParameters formalParameters, FunctionStatementList functionBody)
        {
            isAnonymous = true;
            this.formalParameters = formalParameters;
            this.functionBody = functionBody;
        }

        public FunctionExpression(Identifier identifier, FormalParameters formalParameters, FunctionStatementList functionBody)
        {
            isAnonymous = false;
            this.identifier = identifier;
            this.formalParameters = formalParameters;
            this.functionBody = functionBody;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (isAnonymous)
            {
                //TODO: detect strict mode
                var scope = interpreter.RunningExecutionContext().LexicalEnvironment;
                var closure = FunctionObject.FunctionCreate(FunctionObject.FunctionCreateKind.Normal, formalParameters, functionBody, scope, strict: false);
                closure.MakeConstructor();
                //TODO set SourceText
                return Completion.NormalCompletion(closure);
            }
            else
            {

                //TODO: detect strict mode
                var scope = interpreter.RunningExecutionContext().LexicalEnvironment;
                var funcEnv = scope.NewDeclarativeEnvironment();
                var envRec = funcEnv.EnvironmentRecord;
                envRec.CreateImmutableBinding(identifier.name, false);
                var closure = FunctionObject.FunctionCreate(FunctionObject.FunctionCreateKind.Normal, formalParameters, functionBody, funcEnv, strict: false);
                closure.MakeConstructor();
                closure.SetFunctionName(identifier.name);
                //TODO set SourceText
                envRec.InitializeBinding(identifier.name, closure);
                return Completion.NormalCompletion(closure);
            }
        }

        public Completion NamedEvaluate(Interpreter interpreter, string name)
        {
            var closure = Evaluate(interpreter).value as FunctionObject;
            closure.SetFunctionName(name);
            return Completion.NormalCompletion(closure);
        }
    }

    public class FormalParameters
    {
        public readonly IReadOnlyList<FormalParameter> formalParameters;
        public bool hasRestParameter;
        public readonly Identifier restParameterIdentifier;

        public FormalParameters()
        {
            hasRestParameter = false;
            formalParameters = Utils.EmptyList<FormalParameter>();
        }

        public FormalParameters(IReadOnlyList<FormalParameter> formalParameters)
        {
            hasRestParameter = false;
            this.formalParameters = formalParameters;
        }

        public FormalParameters(IReadOnlyList<FormalParameter> formalParameters, Identifier restParameterIdentifier)
        {
            hasRestParameter = true;
            this.formalParameters = formalParameters;
            this.restParameterIdentifier = restParameterIdentifier;
        }

        public IReadOnlyList<string> BoundNames()
        {
            var names = formalParameters.Select(p => p.identifier.name).ToList();
            if (hasRestParameter)
            {
                names.Add(restParameterIdentifier.name);
            }
            return names;
        }

        public bool IsSimpleParameterList()
        {
            return !hasRestParameter && formalParameters.All(p => !p.hasInitializer);
        }

        public int ExpectedArgumentCount()
        {
            return formalParameters.Count(p => !p.hasInitializer);
        }

        public Completion IteratorBindingInitialization(LexicalEnvironment env, ArgumentIterator arguments)
        {
            foreach (var param in formalParameters)
            {
                Completion comp = param.IteratorBindingInitialization(env, arguments);
                if (comp.IsAbrupt()) return comp;
            }
            if (hasRestParameter)
                return Utils.IteratorBindingInitializationBindingRestIdentifier(restParameterIdentifier, env, arguments);
            return Completion.NormalCompletion();
        }
    }

    public class FormalParameter
    {
        public readonly Identifier identifier;
        public readonly bool hasInitializer;
        public readonly IAssignmentExpression initializerAssignmentExpression;

        public FormalParameter(Identifier identifier)
        {
            hasInitializer = false;
            this.identifier = identifier;
        }

        public FormalParameter(Identifier identifier, IAssignmentExpression initializerAssignmentExpression)
        {
            hasInitializer = true;
            this.identifier = identifier;
            this.initializerAssignmentExpression = initializerAssignmentExpression;
        }

        public Completion IteratorBindingInitialization(LexicalEnvironment env, ArgumentIterator arguments)
        {
            if (!hasInitializer)
                return Utils.IteratorBindingInitializationSingleNameBinding(identifier, initializerAssignmentExpression, env, arguments);
            var currentContext = Interpreter.Instance().RunningExecutionContext();
            var originalEnv = currentContext.VariableEnvironment;
            if (currentContext.VariableEnvironment != currentContext.LexicalEnvironment)
                throw new InvalidOperationException("FormalParameter IteratorBindingInitialization assert step 4");
            if (originalEnv != env)
                throw new InvalidOperationException("FormalParameter IteratorBindingInitialization assert step 5");
            var paramVarEnv = originalEnv.NewDeclarativeEnvironment();
            currentContext.VariableEnvironment = paramVarEnv;
            currentContext.LexicalEnvironment = paramVarEnv;
            var result = Utils.IteratorBindingInitializationSingleNameBinding(identifier, initializerAssignmentExpression, env, arguments);
            currentContext.VariableEnvironment = originalEnv;
            currentContext.LexicalEnvironment = originalEnv;
            return result;
        }
    }
}
