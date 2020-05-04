using System;
using System.Linq;
using JSInterpreter.AST;
using JSInterpreter.Parser;
using Xunit;

namespace JSInterpreter.Test
{
    public class EvaluationTests
    {
        [Fact]
        public void ShouldInitializeVariable()
        {
            var source = "var x; x;";
            var program = new Parser.Parser(source).ParseScript();
            var body = program.scriptBody.statements;

            Assert.Equal(2, body.Count);
            Assert.NotNull(body.First().As<VariableStatement>());
            Assert.NotNull(body.ElementAt(1).As<ExpressionStatement>().expression.As<IdentifierReference>());

            using (var i = Interpreter.Instance())
            {
                i.Execute(source);
            }
        }

        [Fact]
        public void ShouldExecuteFunctionWithFewerParameters()
        {
            var source = @"
function test1(x) {
	return x;
}
test1();";
            var program = new Parser.Parser(source).ParseScript();
            var body = program.scriptBody.statements;

            Assert.Equal(2, body.Count);
            Assert.NotNull(body.First().As<FunctionDeclaration>());
            Assert.NotNull(body.ElementAt(1).As<ExpressionStatement>().expression.As<MemberCallExpression>());

            using (var i = Interpreter.Instance())
            {
                i.Execute(source);
            }
        }


        [Fact]
        public void ShouldConstructEmpty()
        {
            var source = @"(new Object()).newProperty !== undefined";

            using (var i = Interpreter.Instance())
            {
                i.Execute(source);
            }
        }

    }
}