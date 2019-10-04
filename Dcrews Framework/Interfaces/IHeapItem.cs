using System;

namespace Dcrew.Framework
{
    public interface IHeapItem<T> : IComparable<T>
    {
        int Index { get; set; }
    }
}