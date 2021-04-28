using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
            Client.MessageReceived += Client_MessageReceived;
            Client.MessageReceived += Client_ReceivedMessageFromBoss;

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
        }

        private Dictionary<ulong, (DateTime, Task<RestUserMessage>)> Rate = new Dictionary<ulong, (DateTime, Task<RestUserMessage>)>();
        private object RenameLock = new object();
        private async Task Client_MessageReceived(SocketMessage arg)
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

                        var ch = arg.Channel as SocketGuildChannel;
                        string oldName = ch.Name;
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
