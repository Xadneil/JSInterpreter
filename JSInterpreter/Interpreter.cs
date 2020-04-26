using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class Interpreter
    {
        private static Interpreter interpreter;
        public static Interpreter Instance()
        {
            if (interpreter == null)
                interpreter = new Interpreter();
            return interpreter;
        }

        private readonly Stack<ExecutionContext> executionContextStack = new Stack<ExecutionContext>();
        private readonly Realm Realm;

        private Interpreter()
        {
            Realm = Realm.CreateRealm();
        }

        public Completion ResolveBinding(string name, LexicalEnvironment env = null)
        {
            if (env == null)
                env = RunningExecutionContext().LexicalEnvironment;
            //TODO: get strict mode
            var strict = true;
            return env.GetIdentifierReference(name, strict);
        }

        public Completion ResolveThisBinding()
        {
            var env = GetThisEnvironment();
            if (!(env is FunctionEnvironmentRecord functionEnvironmentRecord))
                throw new InvalidOperationException("env has no This Binding");
            return functionEnvironmentRecord.GetThisBinding();
        }

        public ExecutionContext RunningExecutionContext() => executionContextStack.Peek();

        public void PopExecutionStack(ExecutionContext removingContext)
        {
            if (removingContext != executionContextStack.Peek())
            {
                Console.WriteLine("Warning: execution stack does not have expected top frame.");
                if (executionContextStack.Contains(removingContext))
                {
                    ExecutionContext current;
                    do
                    {
                        current = executionContextStack.Pop();
                    } while (current != removingContext);
                }
                else
                {
                    throw new InvalidOperationException("Interpreter.PopExecutionStack: calleeContext was removed when it shouldn't have been");
                }
            }
            //actually remove removingContext
            executionContextStack.Pop();
        }

        public void PushExecutionStack(ExecutionContext context)
        {
            executionContextStack.Push(context);
        }

        public IValue GetNewTarget()
        {
            var envRec = GetThisEnvironment();
            if (!(envRec is FunctionEnvironmentRecord functionEnvironmentRecord))
                throw new InvalidOperationException("Interpreter.GetNewTarget: envRec has no NewTarget.");
            return functionEnvironmentRecord.NewTarget;
        }

        public EnvironmentRecord GetThisEnvironment()
        {
            var lex = RunningExecutionContext().LexicalEnvironment;
            while (true)
            {
                var envRec = lex.EnvironmentRecord;
                if (envRec.HasThisBinding())
                    return envRec;
                var outer = lex.Outer;
                if (outer == null)
                    throw new InvalidOperationException("Interpreter.GetThisEnvironment: no this environment could be found.");
                lex = outer;
            }
        }

        public Realm CurrentRealm()
        {
            return Realm;
        }

        public void Execute(string source)
        {
            var completion = new Parser.Parser(source).ParseScript().ScriptEvaluate(this);
            if (completion.completionType == CompletionType.Throw)
                throw new JavascriptException(completion);
        }
    }

    public class ExecutionContext
    {
        public LexicalEnvironment LexicalEnvironment;
        public LexicalEnvironment VariableEnvironment;
        public Realm Realm;
    }

    public class LexicalEnvironment
    {
        public EnvironmentRecord EnvironmentRecord { get; set; }
        public LexicalEnvironment Outer { get; private set; }

        public LexicalEnvironment NewDeclarativeEnvironment()
        {
            var env = new LexicalEnvironment
            {
                EnvironmentRecord = new DeclarativeEnvironmentRecord(),
                Outer = this
            };
            return env;
        }

        public static LexicalEnvironment NewFunctionalEnvironment(FunctionObject F, IValue newTarget)
        {
            if (!(newTarget is UndefinedValue) && !(newTarget is Object))
                throw new InvalidOperationException("Spec 8.1.2.4 step 2");
            var env = new LexicalEnvironment();
            var envRec = new FunctionEnvironmentRecord(F, newTarget);
            env.EnvironmentRecord = envRec;
            env.Outer = F.Environment;
            return env;
        }

        public Completion GetIdentifierReference(string name, bool strict)
        {
            var comp = EnvironmentRecord.HasBinding(name);
            if (comp.IsAbrupt()) return comp;
            if (comp.Other == true)
                return Completion.NormalCompletion(new ReferenceValue(EnvironmentRecord, name, strict));
            else
            {
                if (Outer == null)
                {
                    return Completion.NormalCompletion(new ReferenceValue(UndefinedValue.Instance, name, strict));
                }
                return Outer.GetIdentifierReference(name, strict);
            }
        }
    }
}
