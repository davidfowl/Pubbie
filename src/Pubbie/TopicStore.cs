using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Pubbie
{
    public class TopicStore : IEnumerable<KeyValuePair<string, Topic>>
    {
        private readonly ConcurrentDictionary<string, Topic> _topics = new ConcurrentDictionary<string, Topic>();

        public bool TryGetValue(string topicName, out Topic topic)
        {
            return _topics.TryGetValue(topicName, out topic);
        }

        public Topic GetOrAdd(string topicName)
        {
            return _topics.GetOrAdd(topicName, _ => new Topic());
        }

        public IEnumerator<KeyValuePair<string, Topic>> GetEnumerator()
        {
            return _topics.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
