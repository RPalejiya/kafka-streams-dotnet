﻿using Streamiz.Kafka.Net.Crosscutting;
using Streamiz.Kafka.Net.State.RocksDb.Internal;
using Streamiz.Kafka.Net.State.Supplier;
using System;

namespace Streamiz.Kafka.Net.State.RocksDb
{
    /// <summary>
    /// A rocksdb key/value store supplier used to create <see cref="RocksDbWindowStore"/>.
    /// </summary>
    public class RocksDbWindowBytesStoreSupplier : IWindowBytesStoreSupplier
    {
        private readonly TimeSpan retention;
        private readonly long segmentInterval;

        /// <summary>
        /// Constructor with some arguments.
        /// </summary>
        /// <param name="storeName">state store name</param>
        /// <param name="retention">retention of windowing store</param>
        /// <param name="segmentInterval">segment interval</param>
        /// <param name="size">window size</param>
        public RocksDbWindowBytesStoreSupplier(
            string storeName,
            TimeSpan retention,
            long segmentInterval,
            long? size)
        {
            Name = storeName;
            this.retention = retention;
            this.segmentInterval = segmentInterval;
            WindowSize = size;
        }
        
        /// <summary>
        /// Window size of the state store
        /// </summary>
        public long? WindowSize { get; set; }

        /// <summary>
        /// Retention of the state store
        /// </summary>
        public long Retention => (long)retention.TotalMilliseconds;

        /// <summary>
        /// State store name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Build the rocksdb state store.
        /// </summary>
        /// <returns><see cref="RocksDbWindowStore"/> state store</returns>
        public IWindowStore<Bytes, byte[]> Get()
        {
            return new RocksDbWindowStore(
                new RocksDbSegmentedBytesStore(
                    Name,
                    Retention,
                    segmentInterval,
                    new RocksDbWindowKeySchema()),
                WindowSize.HasValue ? WindowSize.Value : (long)TimeSpan.FromMinutes(1).TotalMilliseconds);
        }
    }
}
