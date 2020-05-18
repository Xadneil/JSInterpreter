using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public class Interpreter : IDisposable
    {
        private static Script? staScript;
        public static bool staCached { get; private set; } = false;

        [ThreadStatic]
        private static Interpreter? interpreter;
        public static Interpreter Instance()
        {
            if (interpreter == null)
                interpreter = new Interpreter();
            return interpreter;
        }

        private readonly Stack<ExecutionContext> executionContextStack = new Stack<ExecutionContext>();

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        private Interpreter()
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
        }

        public Completion ResolveBinding(string name, bool strict, LexicalEnvironment? env = null)
        {
            if (env == null)
                env = RunningExecutionContext().LexicalEnvironment;
            return env.GetIdentifierReference(name, strict);
        }

        public Completion ResolveThisBinding()
        {
            var env = GetThisEnvironment();
            if (env is FunctionEnvironmentRecord functionEnvironmentRecord)
                return functionEnvironmentRecord.GetThisBinding();
            if (env is GlobalEnvironmentRecord globalEnvironmentRecord)
                return Completion.NormalCompletion(globalEnvironmentRecord.GetThisBinding());

            throw new InvalidOperationException("env has no This Binding");
        }

        public int ExecutionContextStackSize()
        {
            return executionContextStack.Count;
        }

        public ExecutionContext SecondExecutionContext()
        {
            return executionContextStack.ElementAt(1);
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

        private void PopExecutionStack()
        {
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
            return RunningExecutionContext().Realm;
        }

        public void ExecuteWithCachedSta(string staSource, string testSource)
        {
            InitializeHostDefinedRealm();

            EnqueueJob("ScriptJobs", StaScriptEvaluationJob, staSource);

            var oldRealm = CurrentRealm();
            PopExecutionStack();
            if (executionContextStack.Count != 0)
                throw new InvalidOperationException("execution stack should be empty.");
            var newContext = new ExecutionContext(oldRealm);
            //TODO store realm in job queue
            PushExecutionStack(newContext);
            queue();

            EnqueueJob("ScriptJobs", ScriptEvaluationJob, testSource);

            PopExecutionStack();
            if (executionContextStack.Count != 0)
                throw new InvalidOperationException("execution stack should be empty.");
            newContext = new ExecutionContext(oldRealm);
            //TODO store realm in job queue
            PushExecutionStack(newContext);
            queue();
        }

        public void Execute(string source)
        {
            InitializeHostDefinedRealm();

            EnqueueJob("ScriptJobs", ScriptEvaluationJob, source);

            var oldRealm = CurrentRealm();
            PopExecutionStack();
            if (executionContextStack.Count != 0)
                throw new InvalidOperationException("execution stack should be empty.");
            var newContext = new ExecutionContext(oldRealm);
            //TODO store realm in job queue
            PushExecutionStack(newContext);
            queue();
        }

        private Completion InitializeHostDefinedRealm()
        {
            var realm = JSInterpreter.Realm.CreateRealm();
            var newContext = new ExecutionContext(realm);
            PushExecutionStack(newContext);
            realm.SetRealmGlobalObject(null, null);
            Completion globalObj = realm.SetDefaultGlobalBindings();
            if (globalObj.IsAbrupt()) return globalObj;
            // implementation-defined global object properties
            return Completion.NormalCompletion();
        }

        private Completion ScriptEvaluationJob(string source)
        {
            var completion = new Parser.Parser(source).ParseScript().ScriptEvaluate(this);
            if (completion.completionType == CompletionType.Throw)
                throw new JavascriptException(completion);
            return completion;
        }

        private Completion StaScriptEvaluationJob(string source)
        {
            if (staScript == null)
                staScript = new Parser.Parser(source).ParseScript();
            var completion = staScript.ScriptEvaluate(this);
            if (completion.completionType == CompletionType.Throw)
                throw new JavascriptException(completion);
            staCached = true;
            return completion;
        }

        //TODO remove when we have a queue
        private Action queue;

        public TimeZoneInfo LocalTimeZone { get; set; }
        public IFormatProvider Culture { get; set; }

        private void EnqueueJob(string queueName, Func<string, Completion> job, string source)
        {
            queue = () => job(source);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize")]
        public void Dispose()
        {
            interpreter = null;
        }
    }

    public class ExecutionContext
    {
        public LexicalEnvironment LexicalEnvironment { get; set; }
        public LexicalEnvironment VariableEnvironment { get; set; }
        public readonly Realm Realm;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ExecutionContext(Realm realm)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            Realm = realm;
        }
    }

    public class LexicalEnvironment
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public EnvironmentRecord EnvironmentRecord { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public LexicalEnvironment? Outer { get; private set; }

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
