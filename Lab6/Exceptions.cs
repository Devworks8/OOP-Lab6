using System;

namespace Psim.Exceptions
{
    class InvalidCellCount : Exception
    {
        public InvalidCellCount() {}
        public InvalidCellCount(string description="") : base(String.Format("Invalid Cell Count {0}", description))
        {

        }
    }
}