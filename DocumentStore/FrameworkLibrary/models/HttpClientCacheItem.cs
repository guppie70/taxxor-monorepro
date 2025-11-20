using System;
using System.Net.Http;

namespace FrameworkLibrary.models
{

    // Class to track client usage and creation time
    public class HttpClientCacheItem
    {
        public HttpClient Client { get; }
        public DateTime LastUsed { get; private set; }
        public DateTime Created { get; }

        public HttpClientCacheItem(HttpClient client)
        {
            Client = client;
            LastUsed = DateTime.UtcNow;
            Created = DateTime.UtcNow;
        }

        public void UpdateLastUsed()
        {
            LastUsed = DateTime.UtcNow;
        }
    }
}