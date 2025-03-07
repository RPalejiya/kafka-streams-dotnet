﻿using Streamiz.Kafka.Net.Crosscutting;
using Streamiz.Kafka.Net.State.Supplier;

namespace Streamiz.Kafka.Net.State.RocksDb
{
    /// <summary>
    /// A rocksdb key/value store supplier used to create <see cref="RocksDbKeyValueStore"/>.
    /// </summary>
    public class RocksDbKeyValueBytesStoreSupplier : IKeyValueBytesStoreSupplier
    {
        /// <summary>
        /// Constructor with state store name
        /// </summary>
        /// <param name="name">state store name</param>
        public RocksDbKeyValueBytesStoreSupplier(string name)
        {
            Name = name;
        }

        /// <summary>
        /// State store name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Build the rocksdb state store.
        /// </summary>
        /// <returns><see cref="RocksDbKeyValueStore"/> state store</returns>
        public IKeyValueStore<Bytes, byte[]> Get() =>
            new RocksDbKeyValueStore(Name);
    }
}
