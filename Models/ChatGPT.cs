using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Humanizer.Localisation;
using Microsoft.Extensions.Configuration;
using VaderSharp2;

namespace Fall2024_Assignment3_cpmccann.Models
{
    public class ChatGPT
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string? _endpoint;
        private readonly SentimentIntensityAnalyzer _vadersharp;

        public ChatGPT(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["ChatGPT:ApiKey"];
            _endpoint = configuration["ChatGPT:Endpoint"];
            _vadersharp = new SentimentIntensityAnalyzer();
        }

        public async Task<List<Review>> GenerateReviews(string movieName, string genre, int year, int numReviews)
        {
            string persona = "Assume the role of a movie critic. When I provide a prompt, respond " +
                "in the format {fname lname} | {review} $ {fname2 lname2} | {review2}, " +
                "where you pick a different name each time and give the review after. " +
                "Then repeat this for n reviewers based on how many reviews I ask for, and delimit each one by '$'. " +
                "Feel free to make some short reviews, some long ones, and to use exotic names. Do not throw in other tokens since " +
                "your response is being parsed, use the provided format above. Assume reviewers have no knowledge of each other.";
            string prompt = "Write " + numReviews + " reviews for the movie " + movieName + ", a " + genre + 
                " film made in " + year + ". All reviews should be independent and do not reference each other.";

            string reviewsRaw = await AskChatGPT(persona, prompt);
            List<string> reviewsAsText = new List<string>(reviewsRaw.Split("$"));
            List<Review> reviews = new List<Review>();

            foreach (string txt in reviewsAsText)
            {
                Review r = new Review
                {
                    Author = txt.Split("|")[0].Replace("{","").Replace("}","").Trim(),
                    Content = txt.Split("|")[1].Replace("{", "").Replace("}", "").Trim(),
                    Date = RandomDate(year),
                };
                var polarity = _vadersharp.PolarityScores(r.Content);
                r.Rating = 0.5*Math.Round(5*(polarity.Compound+1)); // 2.668 -> 5.26 -> 5 -> 2.5
                reviews.Add(r);
            }

            return reviews;
        }

        public async Task<List<Tweet>> GenerateTweets(string actorName, int age, int numTweets)
        {
            string persona = "Assume the role of an unemployed twitter user. You consider yourself a movie critic, despite " +
                "not being very educated. Your job is to write tweets about actors. You have strong opinions about their " +
                "acting skills, appearance, and public character. When you are asked to write n tweets about someone, respond " +
                "in the format {username1}: {tweet1} $ {username2}: {tweet2}, where you pick a different username for each of " +
                "your burner accounts and separate these tweets with the dollar sign. Follow this format exactly as it will be " +
                "used for parsing.";
            string prompt = "Write " + numTweets + " tweets about " + actorName + ", age " + age + ". Be sure to come up with " +
                "funny and original usernames for each tweet. Do not forget the fucking dollar sign between the reviews!";

            string tweetsRaw = await AskChatGPT(persona, prompt);
            List<string> tweetsAsText = new List<string>(tweetsRaw.Split("$"));
            List<Tweet> tweets = new List<Tweet>();

            foreach (string txt in tweetsAsText)
            {
                Tweet t = new Tweet
                {
                    Author = txt.Split(": ")[0].Replace("{", "").Replace("}", "").Trim(),
                    Content = txt.Split(": ")[1].Replace("{", "").Replace("}", "").Trim()
                };

                var polarity = _vadersharp.PolarityScores(t.Content);
                t.Rating = 0.5 * Math.Round(10 * (polarity.Compound + 1)); // 0.5 -> 1.5 -> 15 -> 7.5
                tweets.Add(t);
            }

            return tweets;
        }

        public async Task<string> AskChatGPT(string persona, string prompt)
        {
            var requestBody = new
            {
                model = "gpt-35-turbo",
                messages = new[] {
                    new { role = "system", content = persona },
                    new { role = "user", content = prompt }
                },
                max_tokens = 500,
                temperature = 0.7
            };

            string requestContent = JsonSerializer.Serialize(requestBody);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(requestContent, Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Add("api-key", _apiKey);

            var response = await _httpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using JsonDocument json = JsonDocument.Parse(responseContent);
                return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").ToString().Trim();
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
        }

        public string RandomDate(int year)
        {
            DateTime start = new DateTime(year, 1, 1);
            int range = (DateTime.Today - start).Days;
            Random r = new Random();
            int randomDays = r.Next(0, range);
            DateTime date = start.AddDays(randomDays);
            return date.ToString("MMM yyyy");
        }
    }
}
