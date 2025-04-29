using System;

namespace GvcRevitPlugins.Shared.Exceptions
{
    public class GvcException : Exception
    {
        public GvcExceptionType Type { get; set; }
        public GvcException(string message, GvcExceptionType type) : base(message)
        {
            Type = type;
        }

        public GvcException(string message) : base(message)
        {
        }

        public GvcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
    public enum GvcExceptionType
    {
        Unknown,
        InvalidElementCategoryName
    }
}
