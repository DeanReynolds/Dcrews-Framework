using System;

namespace Dcrew.Framework
{
    public class Heap<T> where T : IHeapItem<T>
    {
        public int Count { get; private set; }

        readonly T[] _items;

        public Heap(int maxHeapSize) { _items = new T[maxHeapSize]; }

        public void Enqueue(T item)
        {
            item.Index = Count;
            _items[Count++] = item;
            SortUp(item);
        }
        public T Dequeue()
        {
            T firstItem = _items[0];
            _items[0] = _items[--Count];
            _items[0].Index = 0;
            SortDown(_items[0]);
            return firstItem;
        }

        public void UpdateItem(T item) => SortUp(item);

        public bool Contains(T item) => Equals(_items[item.Index], item);

        public void Clear() => Array.Clear(_items, Count = 0, _items.Length);

        void SortUp(T item)
        {
            var parentIndex = (item.Index - 1) / 2;
            while (true)
            {
                T parentItem = _items[parentIndex];
                if (item.CompareTo(parentItem) > 0)
                    Swap(item, parentItem);
                else
                    break;
                parentIndex = (item.Index - 1) / 2;
            }
        }
        void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = (item.Index * 2 + 1),
                    childIndexRight = (item.Index * 2 + 2),
                    swapIndex = 0;
                if (childIndexLeft < Count)
                {
                    swapIndex = childIndexLeft;
                    if (childIndexRight < Count && _items[childIndexLeft].CompareTo(_items[childIndexRight]) < 0)
                        swapIndex = childIndexRight;
                    if (item.CompareTo(_items[swapIndex]) < 0)
                        Swap(item, _items[swapIndex]);
                    else
                        return;
                }
                else
                    return;
            }
        }

        void Swap(T itemA, T itemB)
        {
            _items[itemA.Index] = itemB;
            _items[itemB.Index] = itemA;
            var itemAIndex = itemA.Index;
            itemA.Index = itemB.Index;
            itemB.Index = itemAIndex;
        }
    }
}