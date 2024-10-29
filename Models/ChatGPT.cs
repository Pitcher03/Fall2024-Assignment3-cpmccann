using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
                List<Review> reviews = new List<Review>(); 
                var responseContent = await response.Content.ReadAsStringAsync();
                using JsonDocument json = JsonDocument.Parse(responseContent);
                string responseBody = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").ToString().Trim();
                List<string> reviewsAsText = new List<string>(responseBody.Split("$"));
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
            else
            {
                throw new Exception($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task<List<Tweet>> GenerateTweets(string actorName, int age, int numTweets)
        {
            return new List<Tweet>();
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
