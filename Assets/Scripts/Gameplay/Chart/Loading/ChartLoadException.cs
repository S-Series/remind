using System;

namespace REmind.Gameplay.Chart.Loading
{
    public sealed class ChartLoadException : Exception
    {
        public ChartLoadException(string message)
            : base(message)
        {
        }

        public ChartLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
