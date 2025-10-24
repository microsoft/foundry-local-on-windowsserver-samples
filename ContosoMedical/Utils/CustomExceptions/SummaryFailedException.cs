using System;
using System.Diagnostics.CodeAnalysis;

namespace PatientSummaryTool.Utils.CustomExceptions
{
    [ExcludeFromCodeCoverage]
    [Serializable]
    public class SummaryFailedException : Exception
    {
        // Default constructor
        public SummaryFailedException()
            : base("Summarization task failed.")
        {
        }

        // Constructor with a custom message
        public SummaryFailedException(string message)
            : base(message)
        {
        }

        // Constructor with inner exception
        public SummaryFailedException(Exception innerException)
            : base("Summarization task failed.", innerException)
        {
        }

        // Constructor with message and inner exception
        public SummaryFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
