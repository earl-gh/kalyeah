using System;
using System.Collections.Generic;

namespace Kalyeah.Net
{
    /// <summary>
    /// Godot's `Array.sort_custom` (core/templates/sort_array.h: introsort, a heap sort past the depth limit, then an insertion sort).
    /// It is not stable for more than 16 items, so the port sorts with the same algorithm to list rooms in the very same order.
    /// `less(a, b)` is the GDScript comparator ("a goes before b").
    /// </summary>
    public static class GodotSort
    {
        const int IntrosortThreshold = 16;

        public static void SortCustom<T>(List<T> list, Func<T, T, bool> less)
        {
            T[] a = list.ToArray();
            int n = a.Length;
            if (n > 0)
            {
                Introsort(a, 0, n, Bitlog(n) * 2, less);
                FinalInsertionSort(a, 0, n, less);
            }
            list.Clear();
            list.AddRange(a);
        }

        static int Bitlog(int n)
        {
            int k;
            for (k = 0; n != 1; n >>= 1) k++;
            return k;
        }

        static T MedianOf3<T>(T a, T b, T c, Func<T, T, bool> less)
        {
            if (less(a, b))
            {
                if (less(b, c)) return b;
                if (less(a, c)) return c;
                return a;
            }
            if (less(a, c)) return a;
            if (less(b, c)) return c;
            return b;
        }

        static void PushHeap<T>(T[] a, int first, int hole, int top, T value, Func<T, T, bool> less)
        {
            int parent = (hole - 1) / 2;
            while (hole > top && less(a[first + parent], value))
            {
                a[first + hole] = a[first + parent];
                hole = parent;
                parent = (hole - 1) / 2;
            }
            a[first + hole] = value;
        }

        static void AdjustHeap<T>(T[] a, int first, int hole, int len, T value, Func<T, T, bool> less)
        {
            int top = hole;
            int second = 2 * hole + 2;
            while (second < len)
            {
                if (less(a[first + second], a[first + (second - 1)])) second--;
                a[first + hole] = a[first + second];
                hole = second;
                second = 2 * (second + 1);
            }
            if (second == len)
            {
                a[first + hole] = a[first + (second - 1)];
                hole = second - 1;
            }
            PushHeap(a, first, hole, top, value, less);
        }

        static void PopHeap<T>(T[] a, int first, int last, int result, T value, Func<T, T, bool> less)
        {
            a[result] = a[first];
            AdjustHeap(a, first, 0, last - first, value, less);
        }

        static void MakeHeap<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            if (last - first < 2) return;
            int len = last - first;
            int parent = (len - 2) / 2;
            while (true)
            {
                AdjustHeap(a, first, parent, len, a[first + parent], less);
                if (parent == 0) return;
                parent--;
            }
        }

        static void SortHeap<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            while (last - first > 1)
            {
                PopHeap(a, first, last - 1, last - 1, a[last - 1], less);
                last--;
            }
        }

        static void PartialSort<T>(T[] a, int first, int last, int middle, Func<T, T, bool> less)
        {
            MakeHeap(a, first, middle, less);
            for (int i = middle; i < last; i++)
                if (less(a[i], a[first])) PopHeap(a, first, middle, i, a[i], less);
            SortHeap(a, first, middle, less);
        }

        static int Partitioner<T>(T[] a, int first, int last, T pivot, Func<T, T, bool> less)
        {
            while (true)
            {
                while (less(a[first], pivot)) first++;
                last--;
                while (less(pivot, a[last])) last--;
                if (!(first < last)) return first;
                (a[first], a[last]) = (a[last], a[first]);
                first++;
            }
        }

        static void Introsort<T>(T[] a, int first, int last, int maxDepth, Func<T, T, bool> less)
        {
            while (last - first > IntrosortThreshold)
            {
                if (maxDepth == 0)
                {
                    PartialSort(a, first, last, last, less);
                    return;
                }
                maxDepth--;
                int cut = Partitioner(a, first, last, MedianOf3(a[first], a[first + (last - first) / 2], a[last - 1], less), less);
                Introsort(a, cut, last, maxDepth, less);
                last = cut;
            }
        }

        static void UnguardedLinearInsert<T>(T[] a, int last, T value, Func<T, T, bool> less)
        {
            int next = last - 1;
            while (less(value, a[next]))
            {
                a[last] = a[next];
                last = next;
                next--;
            }
            a[last] = value;
        }

        static void LinearInsert<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            T value = a[last];
            if (less(value, a[first]))
            {
                for (int i = last; i > first; i--) a[i] = a[i - 1];
                a[first] = value;
            }
            else
            {
                UnguardedLinearInsert(a, last, value, less);
            }
        }

        static void InsertionSort<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            if (first == last) return;
            for (int i = first + 1; i != last; i++) LinearInsert(a, first, i, less);
        }

        static void UnguardedInsertionSort<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            for (int i = first; i != last; i++) UnguardedLinearInsert(a, i, a[i], less);
        }

        static void FinalInsertionSort<T>(T[] a, int first, int last, Func<T, T, bool> less)
        {
            if (last - first > IntrosortThreshold)
            {
                InsertionSort(a, first, first + IntrosortThreshold, less);
                UnguardedInsertionSort(a, first + IntrosortThreshold, last, less);
            }
            else
            {
                InsertionSort(a, first, last, less);
            }
        }
    }
}
