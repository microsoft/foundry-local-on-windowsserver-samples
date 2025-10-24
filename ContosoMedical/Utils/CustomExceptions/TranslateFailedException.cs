using System;
using System.Diagnostics.CodeAnalysis;

namespace PatientSummaryTool.Utils.CustomExceptions
{
    [ExcludeFromCodeCoverage]
    [Serializable]
    public class TranslateFailedException : Exception
    {
        // Default constructor
        public TranslateFailedException()
            : base("Translation task failed.")
        {
        }

        // Constructor with a custom message
        public TranslateFailedException(string message)
            : base(message)
        {
        }

        // Constructor with inner exception
        public TranslateFailedException(Exception innerException)
            : base("Translation task failed.", innerException)
        {
        }

        // Constructor with message and inner exception
        public TranslateFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
