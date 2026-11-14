public static class NumericExtensions
{
    public static float ToRadians(this float val)
    {
        return val * (MathF.PI / 180f);
    }

    public static double ToRadians(this double val)
    {
        return val * (Math.PI / 180.0);
    }
}