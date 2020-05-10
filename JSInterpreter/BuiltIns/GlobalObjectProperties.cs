using JSInterpreter.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public static class GlobalObjectProperties
    {
        public static Completion eval(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;

            if (Interpreter.Instance().ExecutionContextStackSize() < 2)
                throw new InvalidOperationException("Spec 18.2.1 step 1");
            var callerContext = Interpreter.Instance().SecondExecutionContext();
            var callerRealm = callerContext.Realm;
            var calleeRealm = Interpreter.Instance().CurrentRealm();
            //TODO HostEnsureCanCompileStrings
            return PerformEval(arguments[0], calleeRealm, strictCaller: false, direct: false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static Completion PerformEval(IValue xValue, Realm evalRealm, bool strictCaller, bool direct)
        {
            if (!(direct || (!direct && !strictCaller)))
                throw new InvalidOperationException("Direct evals cannot have strictCaller. Spec 18.2.1.1 step 1");
            if (!(xValue is StringValue xString))
                return Completion.NormalCompletion(xValue);
            var x = xString.@string;
            var thisEnvRec = Interpreter.Instance().GetThisEnvironment();
            bool inFunction, inMethod, inDerivedConstructor;
            if (thisEnvRec is FunctionEnvironmentRecord functionEnvironmentRecord)
            {
                var F = functionEnvironmentRecord.FunctionObject;
                inFunction = true;
                inMethod = functionEnvironmentRecord.HasSuperBinding();
                inDerivedConstructor = F.ConstructorKind == ConstructorKind.Derived;
            }
            else
            {
                inFunction = inMethod = inDerivedConstructor = false;
            }
            Script script;
            try
            {
                //TODO use the in variables to apply additional early errors
                // Spec 18.2.1.1 step 6
                script = new Parser.Parser(x).ParseScript();
            }
            catch (Exception e)
            {
                return Completion.ThrowSyntaxError(e.Message);
            }
            if (!script.scriptBody.Any())
                return Completion.NormalCompletion(UndefinedValue.Instance);
            //TODO detect strict mode
            var strictEval = false;
            var ctx = Interpreter.Instance().RunningExecutionContext();
            LexicalEnvironment lexEnv;
            LexicalEnvironment varEnv;
            if (direct)
            {
                lexEnv = ctx.LexicalEnvironment.NewDeclarativeEnvironment();
                varEnv = ctx.VariableEnvironment;
            }
            else
            {
                lexEnv = evalRealm.GlobalEnv.NewDeclarativeEnvironment();
                varEnv = evalRealm.GlobalEnv;
            }
            if (strictEval)
                varEnv = lexEnv;
            var evalCtx = new ExecutionContext()
            {
                Realm = evalRealm,
                VariableEnvironment = varEnv,
                LexicalEnvironment = lexEnv
            };
            Interpreter.Instance().PushExecutionStack(evalCtx);
            var result = EvalDeclarationInstantiation(script.scriptBody, varEnv, lexEnv, strictEval);
            if (result.completionType == CompletionType.Normal)
                result = script.scriptBody.Evaluate(Interpreter.Instance());
            if (result.completionType == CompletionType.Normal && result.value == null)
                result = Completion.NormalCompletion(UndefinedValue.Instance);
            Interpreter.Instance().PopExecutionStack(evalCtx);
            return result;
        }

        private static Completion EvalDeclarationInstantiation(ScriptStatementList body, LexicalEnvironment varEnv, LexicalEnvironment lexEnv, bool strict)
        {
            var varNames = body.VarDeclaredNames();
            var varDeclarations = body.VarScopedDeclarations();
            var lexEnvRec = lexEnv.EnvironmentRecord;
            var varEnvRec = varEnv.EnvironmentRecord;
            if (!strict)
            {
                if (varEnvRec is GlobalEnvironmentRecord g)
                {
                    foreach (var name in varNames)
                    {
                        if (g.HasLexicalDeclaration(name))
                            return Completion.ThrowSyntaxError("Spec 18.2.1.3 step 5ai1");
                    }
                }
                var thisLex = lexEnv;
                while (thisLex != varEnv)
                {
                    var thisEnvRec = thisLex.EnvironmentRecord;
                    if (!(thisEnvRec is ObjectEnvironmentRecord))
                    {
                        foreach (var name in varNames)
                        {
                            if (thisEnvRec.HasBinding(name).Other)
                                return Completion.ThrowSyntaxError("Spec 18.2.1.3 step 5dii2ai");
                        }
                    }
                    thisLex = thisLex.Outer;
                    if (thisLex == null)
                    {
                        throw new InvalidOperationException("thisLex and varEnv never matched");
                    }
                }
            }
            var functionsToInitialize = new List<FunctionDeclaration>();
            var declaredFunctionNames = new List<string>();
            foreach (var d in varDeclarations.Reverse())
            {
                if (!(d is VariableDeclaration) && !(d is ForBinding))
                {
                    if (!(d is FunctionDeclaration f))
                        throw new InvalidOperationException("Spec 18.2.1.3 step 8ai");
                    var fn = f.BoundNames()[0];
                    if (!declaredFunctionNames.Contains(fn))
                    {
                        if (varEnvRec is GlobalEnvironmentRecord g)
                        {
                            var fnDefinable = g.CanDeclareGlobalFunction(fn);
                            if (fnDefinable.IsAbrupt()) return fnDefinable;
                            if (!fnDefinable.Other)
                                return Completion.ThrowTypeError($"Function {fn} is not definable in global scope");
                        }
                        declaredFunctionNames.Add(fn);
                        functionsToInitialize.Insert(0, f);
                    }
                }
            }
            var declaredVarNames = new List<string>();
            foreach (var d in varDeclarations)
            {
                IReadOnlyList<string> boundNames = null;
                if (d is VariableDeclaration v)
                    boundNames = v.BoundNames();
                if (d is ForBinding f)
                    boundNames = f.BoundNames();
                if (boundNames != null)
                {
                    foreach (var vn in boundNames)
                    {
                        if (!declaredFunctionNames.Contains(vn))
                        {
                            if (varEnvRec is GlobalEnvironmentRecord g)
                            {
                                var fnDefinable = g.CanDeclareGlobalVar(vn);
                                if (fnDefinable.IsAbrupt()) return fnDefinable;
                                if (!fnDefinable.Other)
                                    return Completion.ThrowTypeError($"Variable {vn} is not definable in global scope");
                            }
                            if (!declaredVarNames.Contains(vn))
                                declaredVarNames.Add(vn);
                        }
                    }
                }
            }
            var lexDeclarations = body.LexicallyScopedDeclarations();
            foreach (var d in lexDeclarations)
            {
                foreach (var dn in d.BoundNames())
                {
                    Completion comp;
                    if (d.IsConstantDeclaration())
                        comp = lexEnvRec.CreateImmutableBinding(dn, true);
                    else
                        comp = lexEnvRec.CreateMutableBinding(dn, false);
                    if (comp.IsAbrupt()) return comp;
                }
            }
            foreach (var f in functionsToInitialize)
            {
                var fn = f.BoundNames()[0];
                var fo = f.InstantiateFunctionObject(lexEnv);
                if (varEnvRec is GlobalEnvironmentRecord g)
                {
                    var comp = g.CreateGlobalFunctionBinding(fn, fo, true);
                    if (comp.IsAbrupt()) return comp;
                }
                else
                {
                    var bindingExists = varEnvRec.HasBinding(fn);
                    if (!bindingExists.Other)
                    {
                        var status = varEnvRec.CreateMutableBinding(fn, true);
                        if (status.IsAbrupt())
                            throw new InvalidOperationException("Spec 18.2.1.3 Step 15dii2");
                        varEnvRec.InitializeBinding(fn, fo);
                    }
                    else
                        varEnvRec.SetMutableBinding(fn, fo, false);
                }
            }
            foreach (var vn in declaredVarNames)
            {
                if (varEnvRec is GlobalEnvironmentRecord g)
                {
                    var comp = g.CreateGlobalVarBinding(vn, true);
                    if (comp.IsAbrupt()) return comp;
                }
                else
                {
                    var bindingExists = varEnvRec.HasBinding(vn);
                    if (!bindingExists.Other)
                    {
                        var status = varEnvRec.CreateMutableBinding(vn, true);
                        if (status.IsAbrupt())
                            throw new InvalidOperationException("Spec 18.2.1.3 Step 16bii2");
                        varEnvRec.InitializeBinding(vn, UndefinedValue.Instance);
                    }
                }
            }
            return Completion.NormalCompletion();
        }

        public static Completion isFinite(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var numComp = arguments[0].ToNumber();
            if (numComp.IsAbrupt()) return numComp;
            var num = (numComp.value as NumberValue).number;
            if (double.IsNaN(num) || double.IsInfinity(num))
                return Completion.NormalCompletion(BooleanValue.False);
            return Completion.NormalCompletion(BooleanValue.True);
        }

        public static Completion isNaN(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var numComp = arguments[0].ToNumber();
            if (numComp.IsAbrupt()) return numComp;
            var num = (numComp.value as NumberValue).number;
            if (double.IsNaN(num))
                return Completion.NormalCompletion(BooleanValue.True);
            return Completion.NormalCompletion(BooleanValue.False);
        }

        public static Completion parseFloat(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var inputStringComp = arguments[0].ToJsString();
            if (inputStringComp.IsAbrupt()) return inputStringComp;
            var inputString = (inputStringComp.value as StringValue).@string;
            var trimmedString = inputString.TrimStart();

            var sign = 1;
            if (trimmedString.Length > 0)
            {
                if (trimmedString[0] == '-')
                {
                    sign = -1;
                    trimmedString = trimmedString.Substring(1);
                }
                else if (trimmedString[0] == '+')
                {
                    trimmedString = trimmedString.Substring(1);
                }
            }

            if (trimmedString.StartsWith("Infinity", StringComparison.InvariantCulture))
            {
                return Completion.NormalCompletion(new NumberValue(sign * double.PositiveInfinity));
            }

            if (trimmedString.StartsWith("NaN", StringComparison.InvariantCulture))
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }

            var separator = (char)0;

            bool isNan = true;
            decimal number = 0;
            var i = 0;
            for (; i < trimmedString.Length; i++)
            {
                var c = trimmedString[i];
                if (c == '.')
                {
                    i++;
                    separator = '.';
                    break;
                }

                if (c == 'e' || c == 'E')
                {
                    i++;
                    separator = 'e';
                    break;
                }

                var digit = c - '0';

                if (digit >= 0 && digit <= 9)
                {
                    isNan = false;
                    number = number * 10 + digit;
                }
                else
                {
                    break;
                }
            }

            decimal pow = 0.1m;

            if (separator == '.')
            {
                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        isNan = false;
                        number += digit * pow;
                        pow *= 0.1m;
                    }
                    else if (c == 'e' || c == 'E')
                    {
                        i++;
                        separator = 'e';
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            var exp = 0;
            var expSign = 1;

            if (separator == 'e')
            {
                if (i < trimmedString.Length)
                {
                    if (trimmedString[i] == '-')
                    {
                        expSign = -1;
                        i++;
                    }
                    else if (trimmedString[i] == '+')
                    {
                        i++;
                    }
                }

                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        exp = exp * 10 + digit;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (isNan)
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }

            for (var k = 1; k <= exp; k++)
            {
                if (expSign > 0)
                {
                    number *= 10;
                }
                else
                {
                    number /= 10;
                }
            }

            return Completion.NormalCompletion(new NumberValue((double)(sign * number)));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static Completion parseInt(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var inputStringComp = arguments[0].ToJsString();
            if (inputStringComp.IsAbrupt()) return inputStringComp;
            var inputString = (inputStringComp.value as StringValue).@string;
            var s = inputString.Trim();

            int R = 0;
            if (arguments.Count > 1)
            {
                var radixComp = arguments[1].ToNumber();
                if (radixComp.IsAbrupt()) return radixComp;
                R = (int)(radixComp.value as NumberValue).number;
            }

            var sign = 1;
            if (!string.IsNullOrEmpty(s))
            {
                if (s[0] == '-')
                {
                    sign = -1;
                }

                if (s[0] == '-' || s[0] == '+')
                {
                    s = s.Substring(1);
                }
            }

            var stripPrefix = true;

            if (R == 0)
            {
                if (s.Length >= 2 && s.StartsWith("0x", StringComparison.InvariantCulture) || s.StartsWith("0X", StringComparison.InvariantCulture))
                {
                    R = 16;
                }
                else
                {
                    R = 10;
                }
            }
            else if (R < 2 || R > 36)
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }
            else if (R != 16)
            {
                stripPrefix = false;
            }

            if (stripPrefix && s.Length >= 2 && s.StartsWith("0x", StringComparison.InvariantCulture) || s.StartsWith("0X", StringComparison.InvariantCulture))
            {
                s = s.Substring(2);
            }

            try
            {
                return Completion.NormalCompletion(new NumberValue(sign * Parse(s, R)));
            }
            catch
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }
        }

        private static double Parse(string number, int radix)
        {
            if (number.Length == 0)
            {
                return double.NaN;
            }

            double result = 0;
            double pow = 1;
            for (int i = number.Length - 1; i >= 0; i--)
            {
                double index = double.NaN;
                char digit = number[i];

                if (digit >= '0' && digit <= '9')
                {
                    index = digit - '0';
                }
                else if (digit >= 'a' && digit <= 'z')
                {
                    index = digit - 'a' + 10;
                }
                else if (digit >= 'A' && digit <= 'Z')
                {
                    index = digit - 'A' + 10;
                }

                if (double.IsNaN(index) || index >= radix)
                {
                    return Parse(number.Substring(0, i), radix);
                }

                result += index * pow;
                pow *= radix;
            }

            return result;
        }
    }
}
