﻿using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace kafka_stream_core.Mock.Kafka
{
    internal class MockProducer : IProducer<byte[], byte[]>
    {
        public MockProducer(string name)
        {
            Name = name;
        }

        #region IProducer Impl

        public Handle Handle => null;

        public string Name { get; }

        public void AbortTransaction(TimeSpan timeout)
        {
            // TODO : 
        }

        public int AddBrokers(string brokers) => 0;

        public void BeginTransaction()
        {
            // TODO
        }

        public void CommitTransaction(TimeSpan timeout)
        {
            // TODO
        }

        public void Dispose()
        {
            // TODO
        }

        public int Flush(TimeSpan timeout)
        {
            // TODO
            return 0;
        }

        public void Flush(CancellationToken cancellationToken = default)
        {
            // TODO : 
        }

        public void InitTransactions(TimeSpan timeout)
        {
            // TODO : 
        }

        public int Poll(TimeSpan timeout) => 0;


        public void Produce(string topic, Message<byte[], byte[]> message, Action<DeliveryReport<byte[], byte[]>> deliveryHandler = null)
        {
            var result = MockCluster.Instance.Produce(topic, message);
            deliveryHandler?.Invoke(result);
        }

        public void Produce(TopicPartition topicPartition, Message<byte[], byte[]> message, Action<DeliveryReport<byte[], byte[]>> deliveryHandler = null)
        {
            var result = MockCluster.Instance.Produce(topicPartition, message);
            deliveryHandler?.Invoke(result);
        }

        public Task<DeliveryResult<byte[], byte[]>> ProduceAsync(string topic, Message<byte[], byte[]> message, CancellationToken cancellationToken = default)
        {
            var r = MockCluster.Instance.Produce(topic, message) as DeliveryResult<byte[], byte[]>;
            return Task.FromResult(r);
        }

        public Task<DeliveryResult<byte[], byte[]>> ProduceAsync(TopicPartition topicPartition, Message<byte[], byte[]> message, CancellationToken cancellationToken = default)
        {
            var r = MockCluster.Instance.Produce(topicPartition, message) as DeliveryResult<byte[], byte[]>;
            return Task.FromResult(r);
        }

        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
        {
            // TODO
        }

        #endregion
    }
}
