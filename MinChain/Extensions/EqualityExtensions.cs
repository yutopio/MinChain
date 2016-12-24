namespace MinChain
{
    public static class EqualityExtensions
    {
        public static bool IsNull<T>(this T obj) where T : class =>
            ReferenceEquals(obj, null);
    }
}
