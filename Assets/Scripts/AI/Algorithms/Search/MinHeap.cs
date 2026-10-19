using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Search
{
    // Binary min-heap used as the best-first frontier (Lecture 05: "a priority queue first pops the node
    // with the minimum cost according to some evaluation function f"). O(log n) push and pop.
    // Equal priorities pop in insertion order, so searches are deterministic.
    public sealed class MinHeap<T>
    {
        readonly List<(double priority, long order, T item)> items = new List<(double, long, T)>();
        long counter;

        public int Count => items.Count;

        public void Push(T item, double priority)
        {
            items.Add((priority, counter++, item));
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(i, parent)) break;
                Swap(i, parent); i = parent;
            }
        }

        public T Pop(out double priority)
        {
            if (items.Count == 0) throw new InvalidOperationException("Heap is empty");
            var top = items[0];
            int last = items.Count - 1;
            items[0] = items[last]; items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, smallest = i;
                if (l < items.Count && Less(l, smallest)) smallest = l;
                if (r < items.Count && Less(r, smallest)) smallest = r;
                if (smallest == i) break;
                Swap(i, smallest); i = smallest;
            }
            priority = top.priority;
            return top.item;
        }

        bool Less(int a, int b) => items[a].priority < items[b].priority || (items[a].priority == items[b].priority && items[a].order < items[b].order);
        void Swap(int a, int b) { var t = items[a]; items[a] = items[b]; items[b] = t; }
    }
}
