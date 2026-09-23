using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public struct VisualMetricResult
    {
        public int PixelCount;
        public double Mae;
        public double Rmse;
        public double Psnr;
        public double Ssim;

        public bool IsExact => Mae <= 0.0 && Rmse <= 0.0;
    }

    public static class VisualMetricCore
    {
        private const double C1 = 0.0001;   // (0.01)^2 for normalized luminance
        private const double C2 = 0.0009;   // (0.03)^2

        public static VisualMetricResult CompareRgb24(byte[] reference, byte[] candidate)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (reference.Length != candidate.Length || reference.Length == 0 || reference.Length % 3 != 0)
                throw new ArgumentException("RGB buffers must be non-empty and have identical 24-bit lengths.");

            int pixels = reference.Length / 3;
            double absSum = 0.0;
            double squaredSum = 0.0;
            double meanReference = 0.0;
            double meanCandidate = 0.0;

            for (int i = 0; i < reference.Length; i += 3)
            {
                double rr = reference[i] / 255.0;
                double rg = reference[i + 1] / 255.0;
                double rb = reference[i + 2] / 255.0;
                double cr = candidate[i] / 255.0;
                double cg = candidate[i + 1] / 255.0;
                double cb = candidate[i + 2] / 255.0;

                double dr = rr - cr;
                double dg = rg - cg;
                double db = rb - cb;

                absSum += Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);
                squaredSum += dr * dr + dg * dg + db * db;

                meanReference += Luma(rr, rg, rb);
                meanCandidate += Luma(cr, cg, cb);
            }

            meanReference /= pixels;
            meanCandidate /= pixels;

            double varianceReference = 0.0;
            double varianceCandidate = 0.0;
            double covariance = 0.0;

            for (int i = 0; i < reference.Length; i += 3)
            {
                double lr = Luma(
                    reference[i] / 255.0,
                    reference[i + 1] / 255.0,
                    reference[i + 2] / 255.0);
                double lc = Luma(
                    candidate[i] / 255.0,
                    candidate[i + 1] / 255.0,
                    candidate[i + 2] / 255.0);

                double ar = lr - meanReference;
                double ac = lc - meanCandidate;
                varianceReference += ar * ar;
                varianceCandidate += ac * ac;
                covariance += ar * ac;
            }

            varianceReference /= pixels;
            varianceCandidate /= pixels;
            covariance /= pixels;

            double mae = absSum / (pixels * 3.0);
            double rmse = Math.Sqrt(squaredSum / (pixels * 3.0));
            double psnr = rmse <= 0.0 ? double.PositiveInfinity : 20.0 * Math.Log10(1.0 / rmse);
            double ssimNumerator =
                (2.0 * meanReference * meanCandidate + C1) *
                (2.0 * covariance + C2);
            double ssimDenominator =
                (meanReference * meanReference + meanCandidate * meanCandidate + C1) *
                (varianceReference + varianceCandidate + C2);
            double ssim = Math.Abs(ssimDenominator) <= double.Epsilon ? 1.0 : ssimNumerator / ssimDenominator;

            return new VisualMetricResult
            {
                PixelCount = pixels,
                Mae = mae,
                Rmse = rmse,
                Psnr = psnr,
                Ssim = ssim
            };
        }

        private static double Luma(double r, double g, double b)
        {
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }
    }
}
