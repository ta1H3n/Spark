using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProfanityFilter;
using System.Text;

namespace Spark
{

    class Program
    {
        private static DiscordSocketClient Client;
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();
        private static readonly CancellationToken ct = cts.Token;

        static void Main(string[] args)
        {
            using Mutex mutex = new Mutex(true, "Global-SparkBot", out bool createdNew);
            if (!createdNew)
            {
                Console.WriteLine("Instance Already Running!");
                return;
            }

            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private async Task MainAsync()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRetryMode = RetryMode.Retry502,
                ExclusiveBulkDelete = true,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 0,
                LargeThreshold = 50,
                GatewayIntents = GatewayIntents.Guilds |
                    GatewayIntents.GuildMessages
            });

            Client.Ready += Client_Ready;
            Client.MessageReceived += MsgReveiced_RenameChannel;
            Client.MessageReceived += MsgReceived_RenameUser;
            Client.MessageReceived += Client_ReceivedMessageFromBoss;
            Client.Log += Client_Log;

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();

            try
            {
                await Task.Delay(-1, ct);
            }
            catch { }
            cts.Dispose();
            Console.WriteLine("Shutting down...");
            await Client.LogoutAsync();
        }

        private Task Client_Log(LogMessage arg)
        {
            if (arg.Severity == LogSeverity.Warning || arg.Severity == LogSeverity.Error || arg.Severity == LogSeverity.Critical)
            {
                Console.WriteLine(arg.Message);
            }
            if (arg.Exception != null)
            {
                Console.WriteLine(arg.Exception.Message);
                Console.WriteLine(arg.Exception);
            }
            return Task.CompletedTask;
        }

        private async Task Client_ReceivedMessageFromBoss(SocketMessage arg)
        {
            if (arg.Author.Id != Config.OwnerId)
                return;

            if (!arg.MentionedUsers.Any(x => Client.CurrentUser.Id == x.Id))
                return;

            string msg = Remove(arg.ToString(), "<", ">").Replace("<", "").Replace(">", "").Trim();

            if (msg.Equals("Die"))
            {
                await arg.Channel.SendMessageAsync($"Finally, sweet release...");
                cts.Cancel();
                return;
            }

            else if (msg.Equals("Hi"))
            {
                await arg.Channel.SendMessageAsync($"Finally, sweet release...");
                cts.Cancel();
                return;
            }

            else if (msg.Equals("Awoo"))
            {
                if (Config.RenameChannelIds.Contains(arg.Channel.Id))
                {
                    Config.RenameChannelIds.Remove(arg.Channel.Id);
                    Config.Save();
                    await arg.Channel.SendMessageAsync($"Baibai uwu");
                }
                else
                {
                    Config.RenameChannelIds.Add(arg.Channel.Id);
                    Config.Save();
                    await arg.Channel.SendMessageAsync($"On it, boss uwu");
                }
            }
            else if (msg.Equals("Awooo"))
            {
                if (Config.RenameUserChannelIds.Contains(arg.Channel.Id))
                {
                    Config.RenameUserChannelIds.Remove(arg.Channel.Id);
                    Config.Save();
                    await arg.Channel.SendMessageAsync($"Seeyanara uwu");
                }
                else
                {
                    Config.RenameUserChannelIds.Add(arg.Channel.Id);
                    Config.Save();
                    await arg.Channel.SendMessageAsync($"Not again... please");
                }
            }
        }

        private Dictionary<ulong, (DateTime, Task<RestUserMessage>)> Rate = new Dictionary<ulong, (DateTime, Task<RestUserMessage>)>();
        private object RenameLock = new object();
        private async Task MsgReveiced_RenameChannel(SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if (!Config.RenameChannelIds.Contains(arg.Channel.Id))
                return;
            if (arg.MentionedUsers.Any(x => Client.CurrentUser.Id == x.Id))
                return;

            lock (RenameLock)
            {
                if (Rate.TryGetValue(arg.Channel.Id, out var dateTime))
                {
                    if (dateTime.Item1 > DateTime.Now)
                    {
                        return;
                    }
                }

                if (arg.Channel is SocketGuildChannel)
                {
                    string name = Remove(arg.ToString(), "<", ">").Replace("<", "").Replace(">", "").Trim();
                    if (name.ToString().Length > 0)
                    {
                        if (name.ToString().Length > 100)
                        {
                            name = name.Substring(0, 99);
                        }

                        name = Profane(arg, name);

                        var ch = arg.Channel as SocketGuildChannel;
                        string oldName = ch.Name.ToString();
                        _ = ch.ModifyAsync(x => x.Name = name);
                        var msg = arg.Channel.SendMessageAsync(embed: new EmbedBuilder().WithDescription($"`{oldName}` -> `{name}`").WithColor(Color.Gold).Build());

                        if (Rate.TryGetValue(arg.Channel.Id, out var item))
                        {
                            try
                            {
                                _ = item.Item2.Result.DeleteAsync();
                            } catch { }
                            Rate.Remove(arg.Channel.Id);
                        }
                        Rate.Add(arg.Channel.Id, (DateTime.Now.AddMinutes(10), msg));
                    }
                }
            }
        }


        private Dictionary<ulong, (DateTime, Task<RestUserMessage>)> UserRate = new Dictionary<ulong, (DateTime, Task<RestUserMessage>)>();
        private object RenameUserLock = new object();
        private async Task MsgReceived_RenameUser(SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if (!Config.RenameUserChannelIds.Contains(arg.Channel.Id))
                return;
            if (arg.MentionedUsers.Any(x => Client.CurrentUser.Id == x.Id))
                return;

            lock (RenameUserLock)
            {
                if (UserRate.TryGetValue(arg.Channel.Id, out var dateTime))
                {
                    if (dateTime.Item1 > DateTime.Now)
                    {
                        return;
                    }
                }

                if (arg.Channel is SocketGuildChannel)
                {
                    string name = Remove(arg.ToString(), "<", ">").Replace("<", "").Replace(">", "").Trim();
                    if (name.ToString().Length > 0)
                    {
                        if (name.ToString().Length > 100)
                        {
                            name = name.Substring(0, 99);
                        }

                        name = Profane(arg, name);

                        var user = arg.Author as SocketGuildUser;
                        string oldName = user.Nickname?.ToString() ?? user.Username.ToString();
                        _ = user.ModifyAsync(x => x.Nickname = name);

                        var eb = new EmbedBuilder()
                            .WithAuthor(oldName, user.GetAvatarUrl())
                            .WithDescription($"- is now `{name}` =w=")
                            .WithColor(Color.Gold);

                        var msg = arg.Channel.SendMessageAsync(embed: eb.Build());

                        if (UserRate.TryGetValue(arg.Channel.Id, out var item))
                        {
                            try
                            {
                                _ = item.Item2.Result.DeleteAsync();
                            }
                            catch { }
                            UserRate.Remove(arg.Channel.Id);
                        }
                        UserRate.Add(arg.Channel.Id, (DateTime.Now.AddMinutes(10), msg));
                    }
                }
            }
        }





        ProfanityFilter.ProfanityFilter filter = new ProfanityFilter.ProfanityFilter(Config.Profanities);
        private string Profane(SocketMessage arg, string msg)
        {
            string flair = " just tried to be funny xd";
            if (filter.ContainsProfanity(msg.ToLower()))
            {
                return arg.Author.Username + flair;
            }
            else if (msg.ToLower().EndsWith(flair))
            {
                return "You tried.";
            }
            return msg;
        }
        private async Task Client_Ready()
        {
            var ch = Client.GetChannel(Config.LogChannelId) as SocketTextChannel;
            string msg = (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) switch
            {
                "Development" => "<:NekoHi:620711213826834443> Dev mode >_<",
                _ => $"<:NekoHi:620711213826834443> `{DateTime.Now.ToString("HH:mm:ss")}` Reporting uwu"
            };

            await ch.SendMessageAsync(msg);
        }
        private string Remove(string original, string firstTag, string secondTag)
        {
            string pattern = firstTag + "(.*?)" + secondTag;
            Regex regex = new Regex(pattern, RegexOptions.RightToLeft);

            foreach (Match match in regex.Matches(original))
            {
                original = original.Replace(match.Groups[1].Value, string.Empty);
            }

            return original;
        }
    }
}
