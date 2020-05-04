using System;
using System.Linq;
using JSInterpreter.AST;
using JSInterpreter.Parser;
using Xunit;

namespace JSInterpreter.Test
{
    public static class ObjectExtensions
    {
        public static T As<T>(this object o) where T : class
        {
            return o as T;
        }
    }

    public class ParserTests
    {
        [Fact]
        public void ShouldParseThis()
        {
            var program = new Parser.Parser("this").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.Equal(ThisExpression.Instance, body.First().As<ExpressionStatement>().expression.As<ThisExpression>());
        }

        [Fact]
        public void ShouldParseNewNoArguments()
        {
            var program = new Parser.Parser("var strObj=new String;").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.NotNull(body.First().As<VariableStatement>().variableDeclarations[0].assignmentExpression.As<NewExpression>());
        }

        [Fact]
        public void ShouldParseNull()
        {
            var program = new Parser.Parser("null").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.Equal(Literal.NullLiteral, body.First().As<ExpressionStatement>().expression.As<Literal>());
        }

        [Fact]
        public void ShouldParseNumeric()
        {
            var program = new Parser.Parser(
                @"
                42
            ").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.IsType<Literal>(body.First().As<ExpressionStatement>().expression);
            Assert.Equal(42d, body.First().As<ExpressionStatement>().expression.As<Literal>().number);
        }

        [Theory]
        [InlineData(@"<%([\s\S]+?)%>", "g", @"/<%([\s\S]+?)%>/g")]
        [InlineData(@"\\|'|\r|\n|\t|\u2028|\u2029", "g", @"/\\|'|\r|\n|\t|\u2028|\u2029/g")]
        [InlineData(@"[\-{}\[\]+?.,\\\^$|#\s]", "g", @"/[\-{}\[\]+?.,\\\^$|#\s]/g")]
        public void ShouldParseRegex(string expectedBody, string expectedFlags, string source)
        {
            RegularExpressionLiteral literal;

            var program = new Parser.Parser(source).ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.NotNull(literal = body.First().As<ExpressionStatement>().expression.As<RegularExpressionLiteral>());
            Assert.Equal(expectedBody, literal.body);
            Assert.Equal(expectedFlags, literal.flags);
        }

        [Fact]
        public void ShouldParseCompareAndContains()
        {
            var program = new Parser.Parser(@"(function () {
    /**
      * Finds the first date, starting from |start|, where |predicate|
      * holds.
      */
    var findNearestDateBefore = function (start, predicate) {
        var current = start;
        var month = 1000 * 60 * 60 * 24 * 30;
        for (var step = month; step > 0; step = Math.floor(step / 3)) {
            if (!predicate(current)) {
                while (!predicate(current))
                    current = new Date(current.getTime() + step);
                current = new Date(current.getTime() - step);
            }
        }
        while (!predicate(current)) {
            current = new Date(current.getTime() + 1);
        }
        return current;
    };

    var juneDate = new Date(2000, 5, 20, 0, 0, 0, 0);
    var decemberDate = new Date(2000, 11, 20, 0, 0, 0, 0);
    var juneOffset = juneDate.getTimezoneOffset();
    var decemberOffset = decemberDate.getTimezoneOffset();
    var isSouthernHemisphere = (juneOffset > decemberOffset);
    var winterTime = isSouthernHemisphere ? juneDate : decemberDate;
    var summerTime = isSouthernHemisphere ? decemberDate : juneDate;

    var dstStart = findNearestDateBefore(winterTime, function (date) {
        return date.getTimezoneOffset() == summerTime.getTimezoneOffset();
    });
    $DST_start_month = dstStart.getMonth();
    $DST_start_sunday = dstStart.getDate() > 15 ? '""last""' : '""first""';
    $DST_start_hour = dstStart.getHours();
    $DST_start_minutes = dstStart.getMinutes();

    var dstEnd = findNearestDateBefore(summerTime, function (date) {
        return date.getTimezoneOffset() == winterTime.getTimezoneOffset();
    });
    $DST_end_month = dstEnd.getMonth();
    $DST_end_sunday = dstEnd.getDate() > 15 ? '""last""' : '""first""';
    $DST_end_hour = dstEnd.getHours();
    $DST_end_minutes = dstEnd.getMinutes();

    return;
})();").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Equal(1, body.Count);
            Assert.NotNull(body[0].As<ExpressionStatement>().expression.As<MemberCallExpression>());
            //Assert.NotNull(body[1].As<FunctionDeclaration>());
        }

        [Fact]
        public void ShouldParseBinaryExpression()
        {
            MultiplicativeExpression binary;
            ParenthesizedExpression parenthesized;
            AdditiveExpression additive;

            var program = new Parser.Parser("(1 + 2 ) * 3").ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.NotNull(binary = body.First().As<ExpressionStatement>().expression.As<MultiplicativeExpression>());
            Assert.Equal(3d, binary.exponentiationExpression.As<Literal>().number);
            Assert.Equal(MultiplicativeOperator.Multiply, binary.multiplicativeOperator);
            Assert.NotNull(parenthesized = binary.multiplicativeExpression.As<ParenthesizedExpression>());
            Assert.NotNull(additive = parenthesized.expression.As<AdditiveExpression>());
            Assert.Equal(1d, additive.additiveExpression.As<Literal>().number);
            Assert.Equal(2d, additive.multiplicativeExpression.As<Literal>().number);
            Assert.Equal(AdditiveOperator.Add, additive.additiveOperator);
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(42, "42")]
        [InlineData(0.14, "0.14")]
        [InlineData(3.14159, "3.14159")]
        [InlineData(6.02214179e+23, "6.02214179e+23")]
        [InlineData(1.492417830e-10, "1.492417830e-10")]
        [InlineData(0, "0x0")]
        [InlineData(0, "0x0;")]
        [InlineData(0xabc, "0xabc")]
        [InlineData(0xdef, "0xdef")]
        [InlineData(0X1A, "0X1A")]
        [InlineData(0x10, "0x10")]
        [InlineData(0x100, "0x100")]
        [InlineData(0X04, "0X04")]
        [InlineData(02, "02")]
        [InlineData(10, "012")]
        [InlineData(10, "0012")]
        [InlineData(1.189008226412092e+38, "0x5973772948c653ac1971f1576e03c4d4")]
        [InlineData(18446744073709552000d, "0xffffffffffffffff")]
        public void ShouldParseNumericLiterals(object expected, string source)
        {
            Literal literal;

            var program = new Parser.Parser(source).ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.NotNull(literal = body.First().As<ExpressionStatement>().expression.As<Literal>());
            Assert.Equal(Convert.ToDouble(expected), Convert.ToDouble(literal.number));
        }

        [Theory]
        [InlineData("Hello", @"'Hello'")]
        [InlineData("\n\r\t\v\b\f\\\'\"\0", @"'\n\r\t\v\b\f\\\'\""\0'")]
        // not supported yet
        //[InlineData("\u0061", @"'\u0061'")]
        //[InlineData("\x61", @"'\x61'")]
        [InlineData("Hello\nworld", @"'Hello\nworld'")]
        [InlineData("Hello\\\nworld", @"'Hello\\\nworld'")]
        public void ShouldParseStringLiterals(string expected, string source)
        {
            Literal literal;

            var program = new Parser.Parser(source).ParseScript();
            var body = program.scriptBody.statements;

            Assert.Single(body);
            Assert.NotNull(literal = body.First().As<ExpressionStatement>().expression.As<Literal>());
            Assert.Equal(expected, literal.@string);
        }

        [Theory]
        [InlineData(@"{ x
                      ++y }")]
        [InlineData(@"{ x
                      --y }")]
        [InlineData(@"var x /* comment */;
                      { var x = 14, y = 3
                      z; }")]
        [InlineData(@"while (true) { continue
                      there; }")]
        [InlineData(@"while (true) { continue // Comment
                      there; }")]
        [InlineData(@"while (true) { continue /* Multiline
                      Comment */there; }")]
        [InlineData(@"while (true) { break
                      there; }")]
        [InlineData(@"while (true) { break // Comment
                      there; }")]
        [InlineData(@"while (true) { break /* Multiline
                      Comment */there; }")]
        [InlineData(@"(function(){ return
                      x; })")]
        [InlineData(@"(function(){ return // Comment
                      x; })")]
        [InlineData(@"(function(){ return/* Multiline
                      Comment */x; })")]
        [InlineData(@"{ throw error
                      error; }")]
        [InlineData(@"{ throw error// Comment
                      error; }")]
        [InlineData(@"{ throw error/* Multiline
                      Comment */error; }")]
        public void ShouldInsertSemicolons(string source)
        {
            new Parser.Parser(source).ParseScript();
        }

        //        [Fact]
        //        public void ShouldProvideLocationForMultiLinesStringLiterals()
        //        {
        //            var source = @"'\
        //\
        //'
        //";
        //            var program = new Parser2(source, new ParserOptions { Loc = true }).ParseScript();
        //            var expr = program.scriptBody.statements.First().As<ExpressionStatement>().Expression;
        //            Assert.Equal(1, expr.Location.Start.Line);
        //            Assert.Equal(0, expr.Location.Start.Column);
        //            Assert.Equal(3, expr.Location.End.Line);
        //            Assert.Equal(1, expr.Location.End.Column);
        //        }

        //[Fact]
        //public void ShouldThrowErrorForInvalidLeftHandOperation()
        //{
        //    Assert.Throws<JavaScriptException>(() => new Engine().Execute("~ (WE0=1)--- l('1');"));
        //}


        [Theory]
        [InlineData("....")]
        [InlineData("while")]
        [InlineData("var")]
        [InlineData("-.-")]
        public void ShouldThrowParserExceptionForInvalidCode(string code)
        {
            Assert.Throws<ParseFailureException>(() => new Parser.Parser(code).ParseScript());
        }
    }
}