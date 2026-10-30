using System;
using System.Collections.Generic;
using System.Linq;

namespace EsfLibrary
{
    /// <summary>
    /// The order the Assembly Kit's 32-bit CA_STD hash map iterates its keys in, read from Rome
    /// II's and Attila's ToolDataBuilderDll. The map starts with 4 buckets and a maximum load of
    /// 1.0; the insert that takes it past that rehashes it to 2n + 1 buckets. A bucket is a list
    /// that new keys join at the back, a rehash moves keys bucket by bucket from the front, and
    /// iteration runs from bucket 0 up. The same map orders those games' ESF string tables
    /// (<see cref="EsfWriter"/>).
    /// </summary>
    public static class CaHashMapOrder
    {
        private const int   INITIAL_BUCKETS     = 4;
        private const float MAXIMUM_LOAD        = 1.0f;

        public static List<T> Iterate<T>(IEnumerable<T> keysInInsertionOrder, Func<T, uint> hash)
        {
            var buckets = NewBuckets<T>(INITIAL_BUCKETS);
            int count   = 0;

            foreach (var key in keysInInsertionOrder)
            {
                ++count;
                if ((float)count / buckets.Length > MAXIMUM_LOAD)
                {
                    var grown = NewBuckets<T>(buckets.Length * 2 + 1);
                    foreach (var moved in buckets.SelectMany(bucket => bucket))
                    {
                        grown[Bucket(hash(moved), grown)].Add(moved);
                    }

                    buckets = grown;
                }

                buckets[Bucket(hash(key), buckets)].Add(key);
            }

            return buckets.SelectMany(bucket => bucket).ToList();
        }

        private static int Bucket<T>(uint hash, List<T>[] buckets)
        {
            return (int)(hash % (uint)buckets.Length);
        }

        private static List<T>[] NewBuckets<T>(int count)
        {
            var buckets = new List<T>[count];
            for (int i = 0; i < count; ++i)
            {
                buckets[i] = new List<T>();
            }

            return buckets;
        }
    }
}
