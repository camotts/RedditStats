using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Channels;
using System.Web.Http;
using CommandLine;
using Spectre.Console;
using Spectre.Console.Rendering;
using YamlDotNet.Core.Tokens;

namespace RedditAssesment
{
    internal class Program
    {
        private class Args
        {
            [Option('c', "config", Required = false, Default = "./config.yml", HelpText = "Config file to load")]
            public required string Config { get; set; }
        }
        static async Task Main(string[] args)
        {
            var cancel = new CancellationTokenSource();
            var channel = Channel.CreateBounded<Post>(100);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                channel.Writer.Complete();
                cancel.Cancel();
            };

            var str = new MemoryStore();
            await Parser.Default.ParseArguments<Args>(args).WithParsedAsync(a =>
            {
                var cfg = Config.LoadConfig(a.Config);

                var r = new Reddit(cfg.AccessToken);
          
                var tasks = new List<Task>();

                    var t = Task.Run(() =>
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            var tasks = new List<Task>();
                            foreach (var subreddit in cfg.Subreddits)
                            {
                                var t = Task.Run(async () =>
                                {
                                    var ch = Channel.CreateBounded<Post>(100);
                                    var t = r.GetPosts(ch, subreddit, cancel.Token);
                                    await foreach (var post in ch.Reader.ReadAllAsync())
                                    {
                                        if (cancel.IsCancellationRequested)
                                        {
                                            Console.WriteLine("Cancel was cancelled");
                                            break;
                                        }
                                        str.SaveData(subreddit, post);
                                    }
                                    try
                                    {
                                        t.AsTask().Wait();
                                    }
                                    catch (AggregateException ae)
                                    {
                                        foreach (var ex in ae.InnerExceptions)
                                        {
                                            if (ex is HttpResponseException)
                                            {
                                                Console.Error.WriteLine($"Unauthroized: Please use a valid access token: {ae}");
                                                cancel.Cancel();
                                            }
                                            else
                                            {
                                                throw ex;
                                            }
                                        }
                                    }
                                });
                                tasks.Add(t);
                            }
                            Task.WaitAll([.. tasks]);
                        }
                    }, cancel.Token);

                    tasks.Add(t);
                

                var tsk = Task.Run(async () =>
                {
                    while (!cancel.IsCancellationRequested)
                    {
                        AnsiConsole.Console.Clear(true);
                        foreach (var subreddit in cfg.Subreddits)
                        {
                            var liked = str.GetMostLikedPosts(subreddit, cfg.DataCount);
                            var mostAuthors = str.GetUsersWithMostPosts(subreddit, cfg.DataCount);
                            var newestPosts = str.GetNewestPosts(subreddit, cfg.DataCount);
                            var timeFrequency = str.GetMostPostedTimes(subreddit, cfg.DataCount);
                            IRenderable data = new Markup("Waiting for data...");
                            if (liked.Any() && mostAuthors.Any())
                            {

                                var likesPanel = GenPanel(liked.Select(x => new Markup(Markup.Escape($"{x.Title} : {x.Ups}"))).ToList(), "Most liked posts");

                                var mostPanel = GenPanel(mostAuthors.Select(x => new Markup(Markup.Escape($"{x.Name} : {x.Count}"))).ToList(), "Users with the most posts");

                                var newestPanel = GenPanel(newestPosts.Select(x => new Markup(Markup.Escape($"{x.Title} : {DateTime.UnixEpoch.AddSeconds(x.Created).ToLocalTime().ToShortTimeString()}"))).ToList(), "Newest posts");

                                var timesPanel = GenPanel(timeFrequency.Select(x => new Markup(Markup.Escape($"{x.Time} : {x.Count}"))).ToList(), "Most popular hours");

                                data = new Rows(likesPanel, mostPanel, newestPanel, timesPanel);
                            }
                            var panel = new Panel(new Rows(
                            new Markup($"[red]{subreddit}[/]"),
                            data,
                            new Markup($"Total Posts : {str.GetPostCount(subreddit)}")));
                            panel.Border = BoxBorder.Rounded;
                            panel.Expand = true;

                            AnsiConsole.Write(panel);
                        }
                        await Task.Delay(10000, cancel.Token);
                    }
                }, cancel.Token);

                tasks.Add(tsk);

                channel.Writer.Complete();
                Task.WaitAll([.. tasks]);

                Console.WriteLine("exited gracefully");
                return Task.CompletedTask;
            });
        }

        private static Panel GenPanel(List<Markup> data, string title)
        {
            var table = new Table();
            for (int i = 0; i< data.Count; i++)
            {
                table.AddColumn("");
            }
            table.ShowHeaders = false;
            table.AddRow(data);
            table.Expand = true;
            var panel = new Panel(new Rows(
                new Markup(title),
                table
                ));
            panel.Expand = true;
            return panel;
        }
    }
}
