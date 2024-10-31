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

        public string FindDistinctActors()
        {
            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return "Unable to access db.";
            string actors = "";

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "SELECT DISTINCT Id, Name FROM Actors;";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = sql.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            actors += reader.GetInt32(0) + " | " + reader.GetString(1) + " $ ";
                        }
                    }
                }
            }

            return actors;
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

                            List<string> movies = new List<string>();

                            using (SqlConnection con2 = new SqlConnection(dbConnectionString))
                            {
                                con2.Open();
                                string query2 = @"
                                                SELECT m.Id, m.Name 
                                                FROM Movies m
                                                INNER JOIN MovieActors ma ON m.Id = ma.MovieId
                                                WHERE ma.ActorId = @ActorId";

                                using (SqlCommand sql2 = new SqlCommand(query2, con2))
                                {
                                    sql2.Parameters.AddWithValue("@ActorId", actor.Id);
                                    using (SqlDataReader reader2 = sql2.ExecuteReader())
                                    {
                                        if (reader2.HasRows)
                                        {
                                            while (reader2.Read())
                                            {
                                                int movieId = reader2.GetInt32(0);
                                                string movieName = reader2.GetString(1);
                                                movies.Add($"{movieId} | {movieName}");
                                            }
                                        }
                                    }
                                }
                            }
                            actor.Movies = movies.Count > 0 ? string.Join(" $ ", movies) : "No movies found!";
                            actors.Add(actor);
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
                Age = Convert.ToInt32(formInput["age"].ToString()),
                Gender = formInput["gender"].ToString().Trim(),
                ImdbLink = formInput["imdblink"].ToString().Trim(),
                CoverImageLink = formInput["coverimagelink"].ToString().Trim()
            };

            a.Tweets = await GenerateActorTweets(a);

            List<string> movieIds = new List<string>(formInput["movies"]);

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null)
            {
                ViewBag.ErrorMessage = "Database connection string is missing.";
                return View("Error");
            }

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                con.Open();

                string query = "INSERT INTO Actors (Name, Gender, Age, ImdbLink, CoverImageLink, Tweets) ";
                query += "VALUES (@Name, @Gender, @Age, @ImdbLink, @CoverImageLink, @Tweets); SELECT SCOPE_IDENTITY();";
                if (a.Tweets == null) a.Tweets = "No tweets yet!";

                int actorId = 0;
                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Name", a.Name);
                    sql.Parameters.AddWithValue("@Age", a.Age);
                    sql.Parameters.AddWithValue("@Gender", a.Gender);
                    sql.Parameters.AddWithValue("@ImdbLink", a.ImdbLink);
                    sql.Parameters.AddWithValue("@CoverImageLink", a.CoverImageLink);
                    sql.Parameters.AddWithValue("@Tweets", a.Tweets);

                    actorId = Convert.ToInt32(sql.ExecuteScalar());
                }
                
                string query2 = "INSERT INTO MovieActors (MovieId, ActorId) VALUES (@MovieId, @ActorId)";
                foreach (string movieId in movieIds)
                {
                    using (SqlCommand sql2 = new SqlCommand(query2, con))
                    {
                        sql2.Parameters.AddWithValue("@MovieId", movieId);
                        sql2.Parameters.AddWithValue("@ActorId", actorId);
                        sql2.ExecuteNonQuery();
                    }
                }

                con.Close();
            }

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
            List<string> movieIds = new List<string>(formInput["movies"]);

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                con.Open();

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
                    
                    sql.ExecuteNonQuery();
                }

                string query2 = "DELETE FROM MovieActors WHERE ActorId = @ActorId";

                using (SqlCommand sql2 = new SqlCommand(query2, con))
                {
                    sql2.Parameters.AddWithValue("@ActorId", actorId);
                    sql2.ExecuteNonQuery();
                }

                string query3 = "INSERT INTO MovieActors (MovieId, ActorId) VALUES (@MovieId, @ActorId)";

                foreach (string movieId in movieIds)
                {
                    using (SqlCommand sql3 = new SqlCommand(query3, con))
                    {
                        sql3.Parameters.AddWithValue("@MovieId", movieId);
                        sql3.Parameters.AddWithValue("@ActorId", actorId);
                        sql3.ExecuteNonQuery();
                    }
                }

                con.Close();
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
            }

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
                                    $"<div><b>{tweet.Rating} </b><i class='fa fa-star'></i></div></td>" +
                                    $"<td style='vertical-align: top;'>" + tweet.Content + "</td></tr>";
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
                                    $"<div><b>{tweet.Rating} </b><i class='fa fa-star'></i></div></td>" +
                                    $"<td style='vertical-align: top;'>" + tweet.Content + "</td></tr>";
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
