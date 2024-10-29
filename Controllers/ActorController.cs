using Fall2024_Assignment3_cpmccann.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Humanizer.Localisation;
using Humanizer;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Identity.Client;

namespace Fall2024_Assignment3_cpmccann.Controllers
{
    public class ActorController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ChatGPT _chatGPT;

        public ActorController(IConfiguration config, ChatGPT chatGPT)
        {
            _configuration = config;
            _chatGPT = chatGPT;
        }

        public IActionResult Index()
        {
            return View("Actors");
        }

        public IActionResult GetActors()
        {
            ActorModel model = new ActorModel();
            List<Actor> actors = new List<Actor>();

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "SELECT * FROM Actors";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = sql.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Actor actor = new Actor
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Gender = reader.GetString(2),
                                Age = reader.GetInt32(3),
                                ImdbLink = reader.GetString(4),
                                CoverImageLink = reader.GetString(5),
                                Tweets = reader.GetString(6)
                            };
                        }
                    }
                }
            }

            model.ActorList = actors;

            return View("ActorResults", model);
        }

        public async Task<IActionResult> AddActor(IFormCollection formInput)
        {
            Actor a = new Actor
            {
                Name = formInput["name"].ToString().Trim(),
                Age = Convert.ToInt32(formInput["year"].ToString()),
                Gender = formInput["genre"].ToString().Trim(),
                ImdbLink = formInput["imdblink"].ToString().Trim(),
                CoverImageLink = formInput["coverimagelink"].ToString().Trim()
            };

            a.Tweets = await GenerateActorTweets(a);

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "INSERT INTO Actors (Name, Gender, Age, ImdbLink, CoverImageLink, Tweets) ";
                query += "VALUES (@Name, @Gender, @Age, @ImdbLink, @CoverImageLink, @Tweets)";
                if (a.Tweets == null) a.Tweets = "No tweets yet!";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Name", a.Name);
                    sql.Parameters.AddWithValue("@Age", a.Age);
                    sql.Parameters.AddWithValue("@Gender", a.Gender);
                    sql.Parameters.AddWithValue("@ImdbLink", a.ImdbLink);
                    sql.Parameters.AddWithValue("@CoverImageLink", a.CoverImageLink);
                    sql.Parameters.AddWithValue("@Tweets", a.Tweets);
                    con.Open();
                    sql.ExecuteNonQuery();
                }
            }

            List<string> movies = new List<string>(formInput["movies"]);

            return Redirect("/Home/Actors");
        }

        public IActionResult EditActor(IFormCollection formInput)
        {
            int actorId = Convert.ToInt16(formInput["id"].ToString());

            string name = formInput["name"].ToString().Trim();
            int age = Convert.ToInt16(formInput["age"].ToString());
            string gender = formInput["gender"].ToString().Trim();
            string imdblink = formInput["imdblink"].ToString().Trim();
            string coverimagelink = formInput["coverimagelink"].ToString().Trim();
            List<string> movies = formInput["movies"].ToString() != ""
                ? new List<string>(formInput["movies"].ToList())
                : new List<string>();
            // fix that ^

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "UPDATE Actors SET Name = @Name, Age = @Age, Gender = @Gender, " +
                    "ImdbLink = @ImdbLink, CoverImageLink = @CoverImageLink WHERE Id = @Id";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Id", actorId);
                    sql.Parameters.AddWithValue("@Name", name);
                    sql.Parameters.AddWithValue("@Age", age);
                    sql.Parameters.AddWithValue("@Gender", gender);
                    sql.Parameters.AddWithValue("@ImdbLink", imdblink);
                    sql.Parameters.AddWithValue("@CoverImageLink", coverimagelink);

                    con.Open();
                    sql.ExecuteNonQuery();
                }
            };

            return Redirect("/Home/Actors");
        }

        public IActionResult DeleteActor(int id) 
        {
            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "DELETE FROM Actors WHERE Id = @Id";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Id", id);
                    con.Open();
                    sql.ExecuteNonQuery();
                }
            };

            return Redirect("/Home/Actors");
        }

        public async Task<string> GenerateActorTweets(Actor a)
        {
            int numTweets = new Random().Next(10, 20);
            try
            {
                List<Tweet> tweets = await _chatGPT.GenerateTweets(a.Name, a.Age, numTweets);
                string tweetHTML = "<table style='width: 100%; border-collapse: collapse;'>";
                foreach (Tweet tweet in tweets)
                {
                    tweetHTML += $"<tr><td style='vertical-align: top; width: 150px;'>" +
                                    $"<div style='text-wrap: nowrap;'><b>{tweet.Author}</b></div>" +
                                    $"<div><b>{tweet.Rating} </b><i class='fa fa-star'></i></div>" +
                                    $"<div><b>{tweet.Date}<b/></div></td>" +
                                    $"<td style='vertical-align: top;'>" +
                                    tweet.Contents + "</td></tr>";
                }
                return tweetHTML + "</table>";
            }
            catch (Exception ex)
            {
                return "No tweets yet! <a href='/Actor/AddActorTweets?actor=" + a.Name + "&age=" + a.Age + ">Generate Tweets</a>";
            }
        }

        public async Task<IActionResult> AddActorTweets(string actor, int age)
        {
            string tweetHTML = "";

            int numTweets = new Random().Next(10, 20);
            try
            {
                List<Tweet> tweets = await _chatGPT.GenerateTweets(actor, age, numTweets);
                tweetHTML = "<table style='width: 100%; border-collapse: collapse;'>";
                foreach (Tweet tweet in tweets)
                {
                    tweetHTML += $"<tr><td style='vertical-align: top; width: 150px;'>" +
                                    $"<div style='text-wrap: nowrap;'><b>{tweet.Author}</b></div>" +
                                    $"<div><b>{tweet.Rating} </b><i class='fa fa-star'></i></div>" +
                                    $"<div><b>{tweet.Date}<b/></div></td>" +
                                    $"<td style='vertical-align: top;'>" +
                                    tweet.Contents + "</td></tr>";
                }
                tweetHTML += "</table>";
            }
            catch (Exception ex)
            {
                tweetHTML = "No tweets yet! <a href='/Actor/AddActorTweets?actor=" + actor + "&age=" + age + ">Generate Tweets</a>";
            }

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "UPDATE Actors SET Tweets = @Tweets WHERE Name = @Name AND Age = @Age";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Tweets", tweetHTML);
                    sql.Parameters.AddWithValue("@Name", actor);
                    sql.Parameters.AddWithValue("@Age", age);

                    con.Open();
                    sql.ExecuteNonQuery();
                }
            }

            return Redirect("/Home/Actors");
        }
    }
}
