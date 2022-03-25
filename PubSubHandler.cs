using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SharpOSC;
using VSF_Twitch_GUI;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Enums;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Windows.Forms;
using TwitchLib.PubSub.Models.Responses.Messages.Redemption;

namespace VSF_Twitch_GUI
{
    class PubSubHandler
    {

        /// <summary>Settings</summary>
        public static IConfiguration Settings;
        /// <summary>Twitchlib Pubsub</summary>
        private static TwitchPubSub PubSub;

        public static TwitchAPI API;

        private static List<string> scopes = new List<string> { "channel:manage:redemptions", "channel:read:redemptions" };

        public  string AccessString;
        public  string RefreshString;
        public  string UserID;

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">Arguments</param>
        static void Main(string[] args)
        {
            



            Settings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Settings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            //run in a sync
            new PubSubHandler()
                .MainAsync(args)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Async main method
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>the Task</returns>
        private async Task MainAsync(string[] args)
        {

            

            var channelId = Settings.GetSection("twitch").GetValue<string>("channelId");

            //set up twitch lib API
            API = new TwitchAPI();
            API.Settings.ClientId = Config.TwitchClientId;

            // start local web server
            var server = new WebServer(Config.TwitchRedirectUri);

            // print out auth url
            Debug.WriteLine($"Please authorize here:\n{getAuthorizationCodeUrl(Config.TwitchClientId, Config.TwitchRedirectUri, scopes)}");

            // listen for incoming requests
            var auth = await server.Listen();

            // exchange auth code for oauth access/refresh
            var resp = await API.Auth.GetAccessTokenFromCodeAsync(auth.Code, Config.TwitchClientSecret, Config.TwitchRedirectUri);

            // update TwitchLib's API with the recently acquired access token
            API.Settings.AccessToken = resp.AccessToken;

            // get the authorized user
            var user = (await API.Helix.Users.GetUsersAsync()).Users[0];

            // print out all the data we've got
            Debug.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}");

            // refresh token
            var refresh = await API.Auth.RefreshAuthTokenAsync(resp.RefreshToken, Config.TwitchClientSecret);
            API.Settings.AccessToken = refresh.AccessToken;

            // confirm new token works
            user = (await API.Helix.Users.GetUsersAsync()).Users[0];

            // print out all the data we've got
            Debug.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}");
            UserID = user.Id;
            AccessString = resp.AccessToken;
            RefreshString = resp.RefreshToken;
            
            //Set up twitch lib pub sub
            PubSub = new TwitchPubSub();
            PubSub.OnListenResponse += OnListenResponse;
            PubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
            PubSub.OnPubSubServiceClosed += OnPubSubServiceClosed;
            PubSub.OnPubSubServiceError += OnPubSubServiceError;
            

            //Set up listeners
            //ListenToBits(channelId);
            //ListenToChatModeratorActions(channelId, channelId);
            //ListenToFollows(channelId);
            //ListenToLeaderboards(channelId);
            //ListenToPredictions(channelId);
            //ListenToRaid(channelId);
            ListenToRewards(UserID);
            //ListenToSubscriptions(channelId);
            //ListenToVideoPlayback(channelId);
            //ListenToWhispers(channelId);

            //Connect to pubsub
            PubSub.Connect();

            //Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        #region Auth Events
        private static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);

            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   "response_type=code&" +
                   $"scope={scopesStr}";
        }

        private static void validateCreds()
        {
            if (String.IsNullOrEmpty(Config.TwitchClientId))
                throw new Exception("client id cannot be null or empty");
            if (String.IsNullOrEmpty(Config.TwitchClientSecret))
                throw new Exception("client secret cannot be null or empty");
            if (String.IsNullOrEmpty(Config.TwitchRedirectUri))
                throw new Exception("redirect uri cannot be null or empty");
            Debug.WriteLine($"Using client id '{Config.TwitchClientId}', secret '{Config.TwitchClientSecret}' and redirect url '{Config.TwitchRedirectUri}'.");
        }
        #endregion


        #region Whisper Events

        private void ListenToWhispers(string channelId)
        {
            PubSub.OnWhisper += PubSub_OnWhisper;
            PubSub.ListenToWhispers(channelId);
        }

        private void PubSub_OnWhisper(object sender, OnWhisperArgs e)
        {
            Debug.WriteLine($"{e.Whisper.DataObjectWhisperReceived.Recipient.DisplayName} send a whisper {e.Whisper.DataObjectWhisperReceived.Body}");
        }

        #endregion

        #region Video Playback Events

        private void ListenToVideoPlayback(string channelId)
        {
            PubSub.OnStreamUp += PubSub_OnStreamUp;
            PubSub.OnStreamDown += PubSub_OnStreamDown;
            PubSub.OnViewCount += PubSub_OnViewCount;
            PubSub.OnCommercial += PubSub_OnCommercial;
            PubSub.ListenToVideoPlayback(channelId);
        }

        [Obsolete]
        private void PubSub_OnCommercial(object sender, OnCommercialArgs e)
        {
            Debug.WriteLine($"A commercial has started for {e.Length} seconds");
        }

        private void PubSub_OnViewCount(object sender, OnViewCountArgs e)
        {
            Debug.WriteLine($"Current viewers: {e.Viewers}");
        }

        private void PubSub_OnStreamDown(object sender, OnStreamDownArgs e)
        {
            Debug.WriteLine($"The stream is down");
        }

        private void PubSub_OnStreamUp(object sender, OnStreamUpArgs e)
        {
            Debug.WriteLine($"The stream is up");
        }

        #endregion

        #region Subscription Events

        private void ListenToSubscriptions(string channelId)
        {
            PubSub.OnChannelSubscription += PubSub_OnChannelSubscription;
            PubSub.ListenToSubscriptions(channelId);
        }

        private void PubSub_OnChannelSubscription(object sender, OnChannelSubscriptionArgs e)
        {
            var gifted = e.Subscription.IsGift ?? false;
            if (gifted)
            {
                Debug.WriteLine($"{e.Subscription.DisplayName} gifted a subscription to {e.Subscription.RecipientName}");
            }
            else
            {
                var cumulativeMonths = e.Subscription.CumulativeMonths ?? 0;
                if (cumulativeMonths != 0)
                {
                    Debug.WriteLine($"{e.Subscription.DisplayName} just subscribed (total of {cumulativeMonths} months)");
                }
                else
                {
                    Debug.WriteLine($"{e.Subscription.DisplayName} just subscribed");
                }

            }

        }

        #endregion

        #region Reward Events

        private void ListenToRewards(string channelId)
        {

            //PubSub.ListenToRewards(channelId);
            PubSub.ListenToChannelPoints(UserID);
            PubSub.OnChannelPointsRewardRedeemed += PubSub_OnChannelPointsRewardRedeemed;
        }

        private void PubSub_OnChannelPointsRewardRedeemed(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            var redemption = e.RewardRedeemed.Redemption;
            var reward = e.RewardRedeemed.Redemption.Reward;
            var redeemedUser = e.RewardRedeemed.Redemption.User;

            if (redemption.Status == "UNFULFILLED")
            {

                Debug.WriteLine($"{redeemedUser.DisplayName} redeemed: {reward.Title}: ID {reward.Id}, {redemption.UserInput}");
                
                API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(e.ChannelId, reward.Id,
                    new List<string>() { e.RewardRedeemed.Redemption.Id }, new UpdateCustomRewardRedemptionStatusRequest() { Status = CustomRewardRedemptionStatus.CANCELED });
                switch (reward.Id)
                {
                    case "e403918c-e2bd-4ae1-a18e-fb72e305c954":

                        break;

                    case "2b92b005-ab1d-4aee-8503-b8880e633992":
                        SetRewardColor(redemption);
                        break;

                    case "c2bd6220-190b-412c-a62b-5bb4c803c5f5":
                        SetRandomColor(redemption);
                        break;
                }
            }

            if (redemption.Status == "FULFILLED")
            {
                Debug.WriteLine($"Reward from {redeemedUser.DisplayName} ({reward.Title}) (ID {reward.Id})has been marked as complete");
                
            }
        }

        public void SetRandomColor(Redemption reward)
        {
            switch (reward.UserInput.ToLower())
            {
                case string a when a.Contains("emi"):
                    SendOSC("TOsc/rainbow", 0, 0, 0, 2);
                    break;
                case string b when b.Contains("base"):
                    SendOSC("TOsc/rainbow", 0, 0, 0, 1);
                    break;
                case string c when c.Contains("both"):
                    SendOSC("TOsc/rainbow", 0, 0, 0, 0);
                    break;
            }
        }

        public void SetRewardColor(Redemption reward)
        {
            
        } 

        public void SendOSC(string address, int val1, int val2, int  val3, int val4)
        {
            var message = new OscMessage(address, val1, val2, val3, val4);
            var OSCSender = new UDPSender("127.0.0.1", 3334);
            OSCSender.Send(message);
        }

       

        

        #endregion

        #region Outgoing Raid Events

        private void ListenToRaid(string channelId)
        {
            PubSub.OnRaidUpdate += PubSub_OnRaidUpdate;
            PubSub.OnRaidUpdateV2 += PubSub_OnRaidUpdateV2;
            PubSub.OnRaidGo += PubSub_OnRaidGo;
            PubSub.ListenToRaid(channelId);
        }

        private void PubSub_OnRaidGo(object sender, OnRaidGoArgs e)
        {
            Debug.WriteLine($"Execute raid for {e.TargetDisplayName}");
        }

        private void PubSub_OnRaidUpdateV2(object sender, OnRaidUpdateV2Args e)
        {
            Debug.WriteLine($"Started raid to {e.TargetDisplayName} with {e.ViewerCount} viewers");
        }

        private void PubSub_OnRaidUpdate(object sender, OnRaidUpdateArgs e)
        {
            Debug.WriteLine($"Started Raid to {e.TargetChannelId} with {e.ViewerCount} viewers will start in {e.RemainingDurationSeconds} seconds");
        }

        #endregion

        #region Prediction Events

        private void ListenToPredictions(string channelId)
        {
            PubSub.OnPrediction += PubSub_OnPrediction;
            PubSub.ListenToPredictions(channelId);
        }

        private void PubSub_OnPrediction(object sender, OnPredictionArgs e)
        {
            //if (e.Type == PredictionType.EventCreated)
            {
                Debug.WriteLine($"A new prediction has started: {e.Title}");
            }

            //if (e.Type == PredictionType.EventUpdated)
            {
                if (e.Status == PredictionStatus.Active)
                {
                    var winningOutcome = e.Outcomes.First(x => e.WinningOutcomeId.Equals(x.Id));
                    Debug.WriteLine($"Prediction: {e.Status}, {e.Title} => winning: {winningOutcome.Title}({winningOutcome.TotalPoints} points by {winningOutcome.TotalUsers} users)");
                }

                if (e.Status == PredictionStatus.Resolved)
                {
                    var winningOutcome = e.Outcomes.First(x => e.WinningOutcomeId.Equals(x.Id));
                    Debug.WriteLine($"Prediction: {e.Status}, {e.Title} => Won: {winningOutcome.Title}({winningOutcome.TotalPoints} points by {winningOutcome.TotalUsers} users)");
                }
            }
        }

        #endregion

        #region Leaderboard Events

        private void ListenToLeaderboards(string channelId)
        {
            PubSub.OnLeaderboardBits += PubSub_OnLeaderboardBits;
            PubSub.OnLeaderboardSubs += PubSub_OnLeaderboardSubs;
            PubSub.ListenToLeaderboards(channelId);
        }

        private void PubSub_OnLeaderboardSubs(object sender, OnLeaderboardEventArgs e)
        {
            Debug.WriteLine($"Gifted Subs leader board");
            foreach (LeaderBoard leaderBoard in e.TopList)
            {
                Debug.WriteLine($"{leaderBoard.Place}) {leaderBoard.UserId} ({leaderBoard.Score})");
            }
        }

        private void PubSub_OnLeaderboardBits(object sender, OnLeaderboardEventArgs e)
        {
            Debug.WriteLine($"Bits leader board");
            foreach (LeaderBoard leaderBoard in e.TopList)
            {
                Debug.WriteLine($"{leaderBoard.Place}) {leaderBoard.UserId} ({leaderBoard.Score})");
            }
        }

        #endregion

        #region Follow Events

        private void ListenToFollows(string channelId)
        {
            PubSub.OnFollow += PubSub_OnFollow;
            PubSub.ListenToFollows(channelId);
        }

        private void PubSub_OnFollow(object sender, OnFollowArgs e)
        {
            Debug.WriteLine($"{e.Username} is now following");
        }

        #endregion

        #region Moderator Events

        private void ListenToChatModeratorActions(string myTwitchId, string channelId)
        {
            PubSub.OnTimeout += PubSub_OnTimeout;
            PubSub.OnBan += PubSub_OnBan;
            PubSub.OnMessageDeleted += PubSub_OnMessageDeleted;
            PubSub.OnUnban += PubSub_OnUnban;
            PubSub.OnUntimeout += PubSub_OnUntimeout;
            PubSub.OnHost += PubSub_OnHost;
            PubSub.OnSubscribersOnly += PubSub_OnSubscribersOnly;
            PubSub.OnSubscribersOnlyOff += PubSub_OnSubscribersOnlyOff;
            PubSub.OnClear += PubSub_OnClear;
            PubSub.OnEmoteOnly += PubSub_OnEmoteOnly;
            PubSub.OnEmoteOnlyOff += PubSub_OnEmoteOnlyOff;
            PubSub.OnR9kBeta += PubSub_OnR9kBeta;
            PubSub.OnR9kBetaOff += PubSub_OnR9kBetaOff;
            PubSub.ListenToChatModeratorActions(myTwitchId, channelId);
        }

        private void PubSub_OnR9kBetaOff(object sender, OnR9kBetaOffArgs e)
        {
            Debug.WriteLine($"{e.Moderator} disabled R9K mode");
        }

        private void PubSub_OnR9kBeta(object sender, OnR9kBetaArgs e)
        {
            Debug.WriteLine($"{e.Moderator} enabled R9K mode");
        }

        private void PubSub_OnEmoteOnlyOff(object sender, OnEmoteOnlyOffArgs e)
        {
            Debug.WriteLine($"{e.Moderator} disabled emote only mode");
        }

        private void PubSub_OnEmoteOnly(object sender, OnEmoteOnlyArgs e)
        {
            Debug.WriteLine($"{e.Moderator} enabled emote only mode");
        }

        private void PubSub_OnClear(object sender, OnClearArgs e)
        {
            Debug.WriteLine($"{e.Moderator} cleared the chat");
        }

        private void PubSub_OnSubscribersOnlyOff(object sender, OnSubscribersOnlyOffArgs e)
        {
            Debug.WriteLine($"{e.Moderator} disabled subscriber only mode");
        }

        private void PubSub_OnSubscribersOnly(object sender, OnSubscribersOnlyArgs e)
        {
            Debug.WriteLine($"{e.Moderator} enabled subscriber only mode");
        }

        private void PubSub_OnHost(object sender, OnHostArgs e)
        {
            Debug.WriteLine($"{e.Moderator} started host to {e.HostedChannel}");
        }

        private void PubSub_OnUntimeout(object sender, OnUntimeoutArgs e)
        {
            Debug.WriteLine($"{e.UntimeoutedUser} undid the timeout of {e.UntimeoutedUser}");
        }

        private void PubSub_OnUnban(object sender, OnUnbanArgs e)
        {
            Debug.WriteLine($"{e.UnbannedBy} unbanned {e.UnbannedUser}");
        }

        private void PubSub_OnMessageDeleted(object sender, OnMessageDeletedArgs e)
        {
            Debug.WriteLine($"{e.DeletedBy} deleted the message \"{e.Message}\" from {e.TargetUser}");
        }

        private void PubSub_OnBan(object sender, OnBanArgs e)
        {
            Debug.WriteLine($"{e.BannedBy} banned {e.BannedUser} ({e.BanReason})");
        }

        private void PubSub_OnTimeout(object sender, OnTimeoutArgs e)
        {
            Debug.WriteLine($"{e.TimedoutBy} timed out {e.TimedoutUser} ({e.TimeoutReason}) for {e.TimeoutDuration.Seconds} seconds");
        }

        #endregion

        #region Bits Events

        private void ListenToBits(string channelId)
        {
            PubSub.OnBitsReceived += PubSub_OnBitsReceived;
            PubSub.ListenToBitsEvents(channelId);
        }

        private void PubSub_OnBitsReceived(object sender, OnBitsReceivedArgs e)
        {
            Debug.WriteLine($"{e.Username} trowed {e.TotalBitsUsed} bits");
        }

        #endregion

        #region Pubsub events

        private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            Debug.WriteLine($"{e.Exception.Message}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e)
        {
            Debug.WriteLine($"Connection closed to pubsub server");
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e)
        {
            Debug.WriteLine($"Connected to pubsub server");
            var oauth = Settings.GetSection("twitch.pubsub").GetValue<string>("oauth");
            PubSub.SendTopics(AccessString);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        private void OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                Debug.WriteLine($"Failed to listen! Response{e.Response.Error.ToString()}");
            }
        }

        #endregion

    }
}
