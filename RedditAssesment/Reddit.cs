using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using YamlDotNet.Core.Tokens;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Threading.Channels;
using System.Collections.Specialized;
using System.Threading.RateLimiting;
using System.Net;
using System.Web.Http;

namespace RedditAssesment
{
    public class ListingContainer
    {
        public required string Kind;
        public required Listing Data;
    }

    public class Listing
    {
        public required string After;
        public required PostContainer[] Children;
    }

    public class PostContainer
    {
        public required string Kind;
        public required Post Data;
    }

    public class Post
    {
        public required string Author;
        public required int Ups;
        public required string Id;
        public required string Title;
        public required float Created;
    }

    public interface RedditApiClient
    {
        public ValueTask GetPosts(ChannelWriter<Post> ch, string subreddit, CancellationToken cancel);
    }

    internal class Reddit: RedditApiClient
    {
        private readonly string accessToken;
        private readonly HttpClient client;

        public Reddit(string accessToken) {
            this.accessToken = accessToken;

            client = new HttpClient();
            client.BaseAddress = new Uri("https://oauth.reddit.com/api/v1");
            client.DefaultRequestHeaders.Add("User-Agent", "windows camjobappassessment v0.0.1");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.accessToken);
        }

        public ValueTask GetPosts(ChannelWriter<Post> ch, string subreddit, CancellationToken cancel)
        {
            return GetPostsAfter(ch, subreddit, "", cancel);
        }

        private async ValueTask GetPostsAfter(ChannelWriter<Post> ch, string subreddit, string after, CancellationToken cancel)
        {
            NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            queryString.Add("after", after);
            var res = client.GetAsync($"/r/{subreddit}/new?{queryString.ToString()}", cancel).Result;

            string body = string.Empty;
            using (var reader = new StreamReader(res.Content.ReadAsStream(cancel)))
            {
                body = reader.ReadToEndAsync(cancel).Result;
            }
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ch.Complete();
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            }

            var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
            var ser = deserializer.Deserialize<ListingContainer>(body);

            //var rlused = 0;
            var rlremain = 0;
            var rlreset = 0;

            //These headers are sometimes not sent over with the results
            IEnumerable<string> values;
            //If we need to use the amount of tokens used
            /*if (res.Headers.TryGetValues("x-ratelimit-used", out values))
            {
                rlused = Int32.Parse(values.FirstOrDefault("0"));
            }*/
            if (res.Headers.TryGetValues("x-ratelimit-remaining", out values))
            {
                rlremain = (int)float.Parse(values.FirstOrDefault("0"));
            }
            if (res.Headers.TryGetValues("x-ratelimit-reset", out values))
            {
                rlreset = Int32.Parse(values.FirstOrDefault("0"));
            }

            if (res.StatusCode != HttpStatusCode.OK && rlremain <= 1)
            {
                await Task.Delay((rlreset * 1000) + 100, cancel);
                await GetPostsAfter(ch, subreddit, after, cancel);
                return;
            } else if (res.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }

            foreach (var item in ser.Data.Children)
            {
                if (cancel.IsCancellationRequested)
                {
                    Console.WriteLine("Cancel was cancelled");
                    break;
                }
                await ch.WriteAsync(item.Data, cancel);
            }
            if (ser.Data.After != null && ser.Data.After.Trim() != "" && !cancel.IsCancellationRequested)
            {
                await GetPostsAfter(ch, subreddit, ser.Data.After, cancel);
            } else
            {
                ch.Complete();
            }
        }

    }
}
