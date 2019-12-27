using Bedrock.Framework.Protocols;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Pubbie
{
    public class Topic : IEnumerable<KeyValuePair<string, ProtocolWriter>>
    {
        private readonly ConcurrentDictionary<string, ProtocolWriter> _clients = new ConcurrentDictionary<string, ProtocolWriter>();
        private long _subscriberCount;
        private long _publishCount;

        public long SubscriberCount => _subscriberCount;

        public long PublishCount => _publishCount;

        public IEnumerator<KeyValuePair<string, ProtocolWriter>> GetEnumerator()
        {
            return _clients.GetEnumerator();
        }

        internal void IncrementPublish()
        {
            Interlocked.Increment(ref _publishCount);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Remove(string key)
        {
            if (_clients.TryRemove(key, out _))
            {
                Interlocked.Decrement(ref _subscriberCount);
            }
        }

        public void Add(string key, ProtocolWriter writer)
        {
            if (_clients.TryAdd(key, writer))
            {
                Interlocked.Increment(ref _subscriberCount);
            }
        }
    }
}
