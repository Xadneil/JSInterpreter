namespace JSInterpreter
{
    public class GlobalObject : Object
    {
        public GlobalObject()
        {
            DefinePropertyOrThrow("undefined", new PropertyDescriptor(UndefinedValue.Instance, false, false, false));
        }
    }
}
