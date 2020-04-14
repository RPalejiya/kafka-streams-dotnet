﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confluent.Kafka;

namespace kafka_stream_core.Mock.Kafka
{
    internal class MockTopic
    {
        private string topic;
        private int partitionNumber;
        private readonly List<MockPartition> partitions;

        public MockTopic(string topic, int part)
        {
            this.topic = topic;
            this.partitionNumber = part;

            this.partitions = new List<MockPartition>();
            for (int i = 0; i < this.partitionNumber; ++i)
                this.partitions.Add(new MockPartition(i));
        }

        public string Name => topic;
        public int PartitionNumber => partitionNumber;

        public void AddMessage(byte[] key, byte[] value, int partition)
        {
            partitions[partition].AddMessageInLog(key, value);
        }

        public TestRecord<byte[], byte[]> GetMessage(int partition, long consumerOffset)
        {
            if (this.partitions[partition].Size > consumerOffset)
            {
                return this.partitions[partition].GetMessage(consumerOffset);
            }
            else
                return null;
        }

        internal void CreateNewPartitions(Partition partition)
        {
            var diff = partition - (partitionNumber - 1);
            for (int i = 0; i < diff; ++i)
                this.partitions.Add(new MockPartition(partitionNumber - 1 + i));

            partitionNumber = partitionNumber + diff;
        }
    }
}
