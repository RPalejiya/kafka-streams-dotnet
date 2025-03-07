﻿using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Streamiz.Kafka.Net.Errors;
using Streamiz.Kafka.Net.Processors.Internal;
using Streamiz.Kafka.Net.SerDes;

namespace Streamiz.Kafka.Net.Processors
{
    internal interface ISinkProcessor
    {
        void UseRepartitionTopic(string repartitionTopic);
    }
    
    internal class SinkProcessor<K, V> : AbstractProcessor<K, V>, ISinkProcessor
    {
        private ITopicNameExtractor<K, V> topicNameExtractor;

        internal SinkProcessor(string name, ITopicNameExtractor<K, V> topicNameExtractor, ISerDes<K> keySerdes, ISerDes<V> valueSerdes)
            : base(name, keySerdes, valueSerdes)
        {
            this.topicNameExtractor = topicNameExtractor;
        }

        public override void Init(ProcessorContext context)
        {
            if (Key == null)
            {
                Key = context.Configuration.DefaultKeySerDes;
            }

            if (Value == null)
            {
                Value = context.Configuration.DefaultValueSerDes;
            }

            Key?.Initialize(context.SerDesContext);
            Value?.Initialize(context.SerDesContext);

            base.Init(context);
        }

        public override void Process(K key, V value)
        {
            LogProcessingKeyValue(key, value);

            long timestamp = Context.Timestamp;
            if (timestamp < 0)
            {
                throw new StreamsException($"Invalid (negative) timestamp of {timestamp } for output record <{key}:{value}>.");
            }

            if (KeySerDes == null || ValueSerDes == null)
            {
                var s = KeySerDes == null ? "key" : "value";
                var errorMessage =
                    $"{logPrefix}The {s} serdes ({(KeySerDes == null ? Key.GetType() : Value.GetType())}) is not compatible to the actual {s} ({(KeySerDes == null ? typeof(K) : typeof(V))}) for this processor. Change the default {s} serdes in StreamConfig or provide correct Serdes via method parameters(using the DSL)";
                log.LogError(errorMessage);
                throw new StreamsException(errorMessage);
            }

            var topicName = topicNameExtractor.Extract(key, value, Context.RecordContext);
            Context.RecordCollector.Send(topicName, key, value, Context.RecordContext.Headers, timestamp, KeySerDes, ValueSerDes);
        }

        public void UseRepartitionTopic(string repartitionTopic)
            => topicNameExtractor = new StaticTopicNameExtractor<K, V>(repartitionTopic);
    }
}
