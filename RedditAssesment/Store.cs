using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditAssesment
{
    internal class AuthorCount
    {
        public required int Count;
        public required string Name;
    }
    internal class TimeCount
    {
        public required float Count;
        public required TimeOnly Time;
    }
    internal interface IStore
    {
        void SaveData(string subreddit, Post item);
        IEnumerable<Post> GetMostLikedPosts(string subreddit, int count);
        IEnumerable<AuthorCount> GetUsersWithMostPosts(string subreddit, int count);
        IEnumerable<Post> GetNewestPosts(string subreddit, int count);
        IEnumerable<TimeCount> GetMostPostedTimes(string subreddit, int count);
        int GetPostCount(string subreddit);
    }

    internal class SubredditInfo
    {
        internal ConcurrentDictionary<string, Post> PostInfo;

        public SubredditInfo()
        {
            PostInfo = new ConcurrentDictionary<string, Post>();
        }

        public void SaveData(Post item)
        {
            PostInfo[item.Id] = item;
        }
    }

    internal class MemoryStore: IStore
    {

        internal ConcurrentDictionary<string, SubredditInfo> Data;

        internal MemoryStore()
        {
            Data = new ConcurrentDictionary<string, SubredditInfo>();
        }

        public void SaveData(string subreddit, Post item)
        {
            if (!Data.TryGetValue(subreddit, out SubredditInfo? value))
            {
                value = new SubredditInfo();
                Data[subreddit] = value;
            }

            value.SaveData(item);
        }

        public IEnumerable<Post> GetMostLikedPosts(string subreddit, int count)
        {
            return Data.GetValueOrDefault(subreddit, new SubredditInfo()).PostInfo.OrderByDescending(x => x.Value.Ups).Take(count).Select(x => x.Value);
        }

        public IEnumerable<AuthorCount> GetUsersWithMostPosts(string subreddit, int count)
        {
            return Data.GetValueOrDefault(subreddit, new SubredditInfo()).PostInfo.GroupBy(x => x.Value.Author).OrderByDescending(x => x.Count()).Take(count).Select(x => new AuthorCount
            {
                Name = x.Key,
                Count = x.Count(),
            });
        }

        public IEnumerable<Post> GetNewestPosts(string subreddit, int count)
        {
            return Data.GetValueOrDefault(subreddit, new SubredditInfo()).PostInfo.OrderByDescending(x => x.Value.Created).Take(count).Select(x => x.Value);
        }

        public IEnumerable<TimeCount> GetMostPostedTimes(string subreddit, int count)
        {
            return Data.GetValueOrDefault(subreddit, new SubredditInfo()).PostInfo.GroupBy(x => TimeOnly.FromDateTime(DateTime.UnixEpoch.AddSeconds(x.Value.Created)).Hour).OrderByDescending(x => x.Count()).Select(x => new
            TimeCount
            {
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(x.Key)),
                Count = x.Count()
            }).Take(count);
        }

        public int GetPostCount(string subreddit)
        {
            return Data.GetValueOrDefault(subreddit, new SubredditInfo()).PostInfo.Count;
        }

    }
}
