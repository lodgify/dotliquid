using System;

namespace DotLiquid.Exceptions
{
    public class FilterNotFoundException : LiquidException
    {
        public FilterNotFoundException(string message, FilterNotFoundException innerException)
            : base(message, innerException)
        {
        }

        public FilterNotFoundException(string message, params string[] args)
            : base(string.Format(message, args))
        {
        }

        public FilterNotFoundException(string message)
            : base(message)
        {
        }
    }
}
