using System.Collections.Generic;
using UnityEngine;

namespace QuickCopyAR.OCR
{
    /// <summary>
    /// Represents a single detected text block with its bounding box.
    /// </summary>
    [System.Serializable]
    public class TextBlock
    {
        public string Text;
        public Rect BoundingBox;
        public float Confidence;
        public List<TextLine> Lines;

        public TextBlock()
        {
            Lines = new List<TextLine>();
        }
    }

    /// <summary>
    /// Represents a single line of text within a block.
    /// </summary>
    [System.Serializable]
    public class TextLine
    {
        public string Text;
        public Rect BoundingBox;
        public float Confidence;
    }

    /// <summary>
    /// Result of OCR processing containing all recognized text and metadata.
    /// </summary>
    public class OCRResult
    {
        public bool IsSuccess { get; set; }
        public string RecognizedText { get; set; }
        public string ErrorMessage { get; set; }
        public List<TextBlock> TextBlocks { get; set; }
        public float ProcessingTimeMs { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        public OCRResult()
        {
            TextBlocks = new List<TextBlock>();
        }

        public static OCRResult Success(string text, List<TextBlock> blocks, float processingTime)
        {
            return new OCRResult
            {
                IsSuccess = true,
                RecognizedText = text,
                TextBlocks = blocks ?? new List<TextBlock>(),
                ProcessingTimeMs = processingTime
            };
        }

        public static OCRResult Error(string message)
        {
            return new OCRResult
            {
                IsSuccess = false,
                ErrorMessage = message
            };
        }

        public static OCRResult Empty()
        {
            return new OCRResult
            {
                IsSuccess = true,
                RecognizedText = "",
                TextBlocks = new List<TextBlock>()
            };
        }
    }
}
