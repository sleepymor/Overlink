namespace Overlink.Core
{
    /// <summary>
    /// Decides how a card name fits its plate: one line at full size, one line shrunk, or two lines.
    /// Kept free of engine types so it can be unit tested. Widths are in any one consistent unit.
    /// </summary>
    public static class CardNameFit
    {
        public const float MinScale = 0.6f;

        public readonly struct Result
        {
            public readonly float Scale;
            public readonly bool Stacked;
            public readonly bool Fits;

            public Result(float scale, bool stacked, bool fits)
            {
                Scale = scale;
                Stacked = stacked;
                Fits = fits;
            }
        }

        /// <param name="oneLineWidth">Width of the name on one line at full size.</param>
        /// <param name="twoLineWidth">Width of the widest line when the name is split at its space, at full size.</param>
        /// <param name="plateWidth">Usable width of the name plate.</param>
        public static Result Plan(float oneLineWidth, float twoLineWidth, float plateWidth)
        {
            if (plateWidth <= 0f) return new Result(1f, false, false);

            if (oneLineWidth <= plateWidth) return new Result(1f, false, true);

            float oneLineScale = plateWidth / oneLineWidth;
            if (oneLineScale >= MinScale) return new Result(oneLineScale, false, true);

            if (twoLineWidth <= plateWidth) return new Result(1f, true, true);

            return new Result(MinScale, true, false);
        }
    }
}
