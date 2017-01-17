namespace MinChain
{
    public static class Array<T>
    {
        public static readonly T[] Empty = new T[0];
    }

    public static class ArrayExtensions
    {
        public static bool IsNullOrEmpty<T>(this T[] array) =>
            array.IsNull() || array.Length == 0;
    }
}
