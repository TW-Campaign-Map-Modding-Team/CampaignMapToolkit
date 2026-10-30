using System;
using System.Collections.Generic;
using System.Linq;

namespace EsfLibrary
{
    /// <summary>
    /// A game builder's CA_STD hash map, whose iteration order is the order of its ESF string tables
    /// (<see cref="EsfWriter"/>) and of dynamic_resources.esf's regions. The map starts with
    /// <see cref="InitialBuckets"/> buckets and a maximum load of 1.0; the insert that takes it past
    /// that rehashes it to 2n + 1 buckets. A bucket is a list that new keys join at the back, a
    /// rehash moves keys bucket by bucket from the front, and iteration runs from bucket 0 up. Read
    /// from Rome II's and Attila's 32-bit ToolDataBuilderDll and Warhammer III's 64-bit one.
    /// </summary>
    public sealed class CaHashMap
    {
        private const float MAXIMUM_LOAD = 1.0f;

        public static readonly CaHashMap RomeII     = new CaHashMap(CaStringHash.RomeII, 4);

        /// <summary>Attila's, which Thrones of Britannia's files are taken to share.</summary>
        public static readonly CaHashMap Attila     = new CaHashMap(CaStringHash.Attila, 4);

        /// <summary>
        /// Warhammer's shipped files: Attila's string hash in the 64-bit map. They predate its
        /// Assembly Kit, whose builder uses <see cref="CaStringHash.Murmur"/>.
        /// </summary>
        public static readonly CaHashMap Warhammer  = new CaHashMap(CaStringHash.Attila, 1);

        /// <summary>The 64-bit builders: Warhammer II and III, Troy, Three Kingdoms and Pharaoh.</summary>
        public static readonly CaHashMap Murmur     = new CaHashMap(CaStringHash.Murmur, 1);

        public Func<string, uint> StringHash { get; }

        public int InitialBuckets { get; }

        private CaHashMap(Func<string, uint> stringHash, int initialBuckets)
        {
            StringHash      = stringHash;
            InitialBuckets  = initialBuckets;
        }

        public List<T> Iterate<T>(IEnumerable<T> keysInInsertionOrder, Func<T, uint> hash)
        {
            var buckets = NewBuckets<T>(InitialBuckets);
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
