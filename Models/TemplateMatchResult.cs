using System.Drawing;

namespace RobloxMacroStudio.Models
{
    public class TemplateMatchResult
    {
        public bool IsFound { get; set; }
        public double Confidence { get; set; }
        public double Scale { get; set; } = 1.0;
        public Rectangle BoundingBox { get; set; }

        public int CenterX => BoundingBox.X + BoundingBox.Width / 2;
        public int CenterY => BoundingBox.Y + BoundingBox.Height / 2;

        public static TemplateMatchResult NotFound => new TemplateMatchResult
        {
            IsFound = false,
            Confidence = 0.0,
            BoundingBox = Rectangle.Empty
        };

        public override string ToString()
        {
            return IsFound
                ? $"Found at ({CenterX}, {CenterY}) [Score: {Confidence:P1}, Scale: {Scale:F2}x]"
                : "Not Found";
        }
    }
}
