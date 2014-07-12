using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Media.SpeechRecognition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace XboxMusicCortana
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        HttpClient client = new HttpClient();
        string service = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
        string clientId = "XboxMusicCortana";
        string clientSecret = ClientSecret.clientSecret;
        string scope = "http://music.xboxlive.com";
        string grantType = "client_credentials";
        string clientInstanceId = "f496a444-d9a6-40b4-8400-d70b7b39c07b";
        bool isPlaying = false;
        DateTime tokenTime;

        Dictionary<string, string> requestData = new Dictionary<string, string>();
        string token = "";

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            requestData.Add("client_id", clientId);
            requestData.Add("client_secret", clientSecret);
            requestData.Add("scope", scope);
            requestData.Add("grant_type", grantType);
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Get access token
            var response = await client.PostAsync(new Uri(service), new HttpFormUrlEncodedContent(requestData));
            string responseString = await response.Content.ReadAsStringAsync();
            token = Regex.Match(responseString, ".*\"access_token\":\"(.*?)\".*", RegexOptions.IgnoreCase).Groups[1].Value;
            tokenTime = DateTime.Now;
            searchButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private async void searchButton_Click(object sender, RoutedEventArgs e)
        {
            // Check to see if we are currently playing anything
            // so that we can pause it so we don't play during
            // speech recoginzation
            if (isPlaying)
            {
                mediaElement.Pause();
            }

            // Ask the user what they want to listen to and wait for the response
            SpeechRecognizer speechRec = new SpeechRecognizer();
            speechRec.UIOptions.AudiblePrompt = "What do you want to listen to?";
            speechRec.UIOptions.ExampleText = "Anamanaguchi";
            await speechRec.CompileConstraintsAsync();
            SpeechRecognitionResult result = await speechRec.RecognizeWithUIAsync();

            // If they say something look it up
            if (result.Text != "")
            {
                if (DateTime.Now > tokenTime.AddMinutes(10))
                {
                    // Should get new token if we passed 10 minutes. Doesn't work.
                    //requestData.Clear();
                    //requestData.Add("grant_type", "refresh_token");
                    //requestData.Add("client_id", clientId);
                    //requestData.Add("client_secret", clientSecret);
                    //requestData.Add("refresh_token", token);
                    //requestData.Add("scope", "https://api.datamarket.azure.com/");

                    //var tokenResponse = await client.PostAsync(new Uri(service), new HttpFormUrlEncodedContent(requestData));
                    //string tokenResponseString = await tokenResponse.Content.ReadAsStringAsync();
                    //token = Regex.Match(tokenResponseString, ".*\"access_token\":\"(.*?)\".*", RegexOptions.IgnoreCase).Groups[1].Value;
                    //tokenTime = DateTime.Now;
                }

                searchButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                searchRing.IsActive = true;

                // Search for what they said
                string searchService = "https://music.xboxlive.com/1/content/music/search?q=" + result.Text + "&accessToken=Bearer+";
                var response = await client.GetAsync(new Uri(searchService + WebUtility.UrlEncode(token)));
                string responseString = await response.Content.ReadAsStringAsync();
                JObject responseObject = JObject.Parse(responseString);

                // Make sure there are no errors
                if ((string)responseObject["Error"]["ErrorCode"] == null)
                {
                    // Make sure we have at least one track
                    if ((int)responseObject["Tracks"]["TotalItemCount"] > 0)
                    {
                        // Get track preview
                        string previewService = "https://music.xboxlive.com/1/content/" +
                            (string)responseObject["Tracks"]["Items"][0]["Id"] +
                            "/preview?clientInstanceId=" + clientInstanceId +
                            "&accessToken=Bearer+";
                        var previewResponse = await client.GetAsync(new Uri(previewService + WebUtility.UrlEncode(token)));
                        string previewResponseString = await previewResponse.Content.ReadAsStringAsync();
                        JObject previewResponseObject = JObject.Parse(previewResponseString);

                        // Play track preview MP3 and display song name and album name
                        mediaElement.Source = new Uri((string)previewResponseObject["Url"]);
                        trackInfoTextBlock.Text = (string)responseObject["Tracks"]["Items"][0]["Name"] +
                            " - " +
                            (string)responseObject["Tracks"]["Items"][0]["Album"]["Name"];
                        mediaElement.Play();
                        isPlaying = true;
                    }
                }
                searchRing.IsActive = false;
                searchButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                // Resume playback if they didn't say anything and we had a track paused
                if (isPlaying)
                {
                    mediaElement.Play();
                }
            }
        }

        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Once track is done reset
            trackInfoTextBlock.Text = "";
            isPlaying = false;
        }
    }
}
