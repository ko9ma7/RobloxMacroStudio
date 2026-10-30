using System;
using System.Drawing;
using OpenCvSharp;
using RobloxMacroStudio.Models;

namespace RobloxMacroStudio.Services
{
    public class OpenCvVisionService : IVisionService
    {
        public TemplateMatchResult MatchTemplate(Mat source, Mat template, double threshold = 0.8, Rectangle? roi = null)
        {
            if (source == null || source.Empty() || template == null || template.Empty())
                return TemplateMatchResult.NotFound;

            int offsetX = 0;
            int offsetY = 0;
            Mat searchMat = source;
            bool isRoiAllocated = false;

            if (roi.HasValue && !roi.Value.IsEmpty)
            {
                var r = NormalizeRoi(roi.Value, source.Width, source.Height);
                if (r.Width >= template.Width && r.Height >= template.Height)
                {
                    searchMat = new Mat(source, new OpenCvSharp.Rect(r.X, r.Y, r.Width, r.Height));
                    offsetX = r.X;
                    offsetY = r.Y;
                    isRoiAllocated = true;
                }
            }

            try
            {
                if (searchMat.Width < template.Width || searchMat.Height < template.Height)
                    return TemplateMatchResult.NotFound;

                using var result = new Mat();
                Cv2.MatchTemplate(searchMat, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                if (maxVal >= threshold)
                {
                    return new TemplateMatchResult
                    {
                        IsFound = true,
                        Confidence = maxVal,
                        Scale = 1.0,
                        BoundingBox = new Rectangle(
                            maxLoc.X + offsetX,
                            maxLoc.Y + offsetY,
                            template.Width,
                            template.Height)
                    };
                }

                return new TemplateMatchResult
                {
                    IsFound = false,
                    Confidence = maxVal,
                    Scale = 1.0,
                    BoundingBox = Rectangle.Empty
                };
            }
            finally
            {
                if (isRoiAllocated)
                {
                    searchMat.Dispose();
                }
            }
        }

        public TemplateMatchResult MatchTemplateMultiScale(Mat source, Mat template, double threshold = 0.8, double minScale = 0.8, double maxScale = 1.2, double scaleStep = 0.1, Rectangle? roi = null)
        {
            if (source == null || source.Empty() || template == null || template.Empty())
                return TemplateMatchResult.NotFound;

            double bestScore = -1.0;
            TemplateMatchResult bestResult = TemplateMatchResult.NotFound;

            if (scaleStep <= 0.01) scaleStep = 0.05;
            if (minScale <= 0.1) minScale = 0.5;
            if (maxScale < minScale) maxScale = minScale;

            for (double scale = minScale; scale <= maxScale + 0.001; scale += scaleStep)
            {
                int newW = (int)(template.Width * scale);
                int newH = (int)(template.Height * scale);

                if (newW <= 5 || newH <= 5 || newW > source.Width || newH > source.Height)
                    continue;

                using var resizedTemplate = new Mat();
                Cv2.Resize(template, resizedTemplate, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Linear);

                var match = MatchTemplate(source, resizedTemplate, threshold, roi);
                match.Scale = scale;

                if (match.Confidence > bestScore)
                {
                    bestScore = match.Confidence;
                    bestResult = match;
                }

                // Early exit if near-perfect match
                if (match.IsFound && match.Confidence >= 0.95)
                {
                    return match;
                }
            }

            return bestResult;
        }

        public TemplateMatchResult FindColorBlob(Mat source, int minH, int maxH, int minS, int maxS, int minV, int maxV, int minArea = 50, Rectangle? roi = null)
        {
            if (source == null || source.Empty())
                return TemplateMatchResult.NotFound;

            int offsetX = 0;
            int offsetY = 0;
            Mat searchMat = source;
            bool isRoiAllocated = false;

            if (roi.HasValue && !roi.Value.IsEmpty)
            {
                var r = NormalizeRoi(roi.Value, source.Width, source.Height);
                if (r.Width > 5 && r.Height > 5)
                {
                    searchMat = new Mat(source, new OpenCvSharp.Rect(r.X, r.Y, r.Width, r.Height));
                    offsetX = r.X;
                    offsetY = r.Y;
                    isRoiAllocated = true;
                }
            }

            try
            {
                using var hsv = new Mat();
                Cv2.CvtColor(searchMat, hsv, ColorConversionCodes.BGR2HSV);

                using var mask = new Mat();
                var lower = new Scalar(Math.Clamp(minH, 0, 180), Math.Clamp(minS, 0, 255), Math.Clamp(minV, 0, 255));
                var upper = new Scalar(Math.Clamp(maxH, 0, 180), Math.Clamp(maxS, 0, 255), Math.Clamp(maxV, 0, 255));
                Cv2.InRange(hsv, lower, upper, mask);

                // Morphological filtering to reduce noise
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
                Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

                Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours == null || contours.Length == 0)
                    return TemplateMatchResult.NotFound;

                double maxArea = 0;
                OpenCvSharp.Rect bestRect = default;

                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour);
                    if (area >= minArea && area > maxArea)
                    {
                        maxArea = area;
                        bestRect = Cv2.BoundingRect(contour);
                    }
                }

                if (maxArea >= minArea && bestRect.Width > 0 && bestRect.Height > 0)
                {
                    return new TemplateMatchResult
                    {
                        IsFound = true,
                        Confidence = Math.Min(1.0, maxArea / 1000.0),
                        Scale = 1.0,
                        BoundingBox = new Rectangle(
                            bestRect.X + offsetX,
                            bestRect.Y + offsetY,
                            bestRect.Width,
                            bestRect.Height)
                    };
                }

                return TemplateMatchResult.NotFound;
            }
            finally
            {
                if (isRoiAllocated)
                {
                    searchMat.Dispose();
                }
            }
        }

        public void DrawMatchResult(Mat target, TemplateMatchResult result, Scalar color, string label = "")
        {
            if (target == null || target.Empty() || result == null || !result.IsFound)
                return;

            var r = result.BoundingBox;
            var cvRect = new OpenCvSharp.Rect(r.X, r.Y, r.Width, r.Height);

            // Draw bounding box
            Cv2.Rectangle(target, cvRect, color, 2);

            // Draw center crosshair
            int cx = result.CenterX;
            int cy = result.CenterY;
            Cv2.DrawMarker(target, new OpenCvSharp.Point(cx, cy), color, MarkerTypes.Cross, 16, 2);

            // Text label
            string text = string.IsNullOrWhiteSpace(label)
                ? $"{result.Confidence:P0}"
                : $"{label} ({result.Confidence:P0})";

            int baseline = 0;
            var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, 0.5, 1, out baseline);
            int textY = Math.Max(textSize.Height + 5, r.Y - 5);

            Cv2.Rectangle(target, new OpenCvSharp.Rect(r.X, textY - textSize.Height - 4, textSize.Width + 6, textSize.Height + 6), new Scalar(0, 0, 0), -1);
            Cv2.PutText(target, text, new OpenCvSharp.Point(r.X + 3, textY), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
        }

        private static Rectangle NormalizeRoi(Rectangle roi, int maxW, int maxH)
        {
            int x = Math.Max(0, roi.X);
            int y = Math.Max(0, roi.Y);
            int w = Math.Min(roi.Width, maxW - x);
            int h = Math.Min(roi.Height, maxH - y);

            if (w <= 0 || h <= 0)
                return Rectangle.Empty;

            return new Rectangle(x, y, w, h);
        }
    }
}
