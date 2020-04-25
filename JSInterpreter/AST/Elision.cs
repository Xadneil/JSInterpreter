namespace JSInterpreter.AST
{
    public class Elision : IArrayLiteralItem
    {
        public readonly int width;

        public Elision(int width = 1)
        {
            this.width = width;
        }
    }

}
