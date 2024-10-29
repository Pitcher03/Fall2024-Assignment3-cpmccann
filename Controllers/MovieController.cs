using Fall2024_Assignment3_cpmccann.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Humanizer.Localisation;
using Humanizer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Fall2024_Assignment3_cpmccann.Controllers
{
    public class MovieController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ChatGPT _chatGPT;

        public MovieController(IConfiguration config, ChatGPT chatGPT)
        {
            _configuration = config;
            _chatGPT = chatGPT;
        }

        public IActionResult Index()
        {
            return View("Movies");
        }

        public IActionResult GetMovies()
        {
            MovieModel model = new MovieModel();
            List<Movie> movies = new List<Movie>();

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "SELECT * FROM Movies";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    con.Open();
                    using (SqlDataReader reader = sql.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Movie movie = new Movie
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Year = reader.GetInt32(2),
                                Genre = reader.GetString(3),
                                Description = reader.GetString(4),
                                ImdbLink = reader.GetString(5),
                                CoverImageLink = reader.GetString(6),
                                DateAdded = reader.GetDateTime(7),
                                Reviews = reader.GetString(8)
                            };
                            movies.Add(movie);
                        }
                    }
                };
            }

            model.MovieList = movies;

            return View("MovieResults", model);
        }

        public async Task<IActionResult> AddMovie(IFormCollection formInput) 
        {
            Movie m = new Movie
            {
                Name = formInput["name"].ToString().Trim(),
                Year = Convert.ToInt32(formInput["year"].ToString()),
                DateAdded = new DateTime(),
                Genre = formInput["genre"].ToString().Trim(),
                Description = formInput["description"].ToString().Trim(),
                ImdbLink = formInput["imdblink"].ToString().Trim(),
                CoverImageLink = formInput["coverimagelink"].ToString().Trim()
            };

            m.Reviews = await GenerateMovieReviews(m);

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "INSERT INTO Movies (Name, Year, Genre, Description, ImdbLink, CoverImageLink, DateAdded, Reviews) ";
                query += "VALUES (@Name, @Year, @Genre, @Description, @ImdbLink, @CoverImageLink, GETDATE(), @Reviews)";
                if (m.Reviews == null) m.Reviews = "No reviews yet!";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Name", m.Name);
                    sql.Parameters.AddWithValue("@Year", m.Year);
                    sql.Parameters.AddWithValue("@Genre", m.Genre);
                    sql.Parameters.AddWithValue("@Description", m.Description);
                    sql.Parameters.AddWithValue("@ImdbLink", m.ImdbLink);
                    sql.Parameters.AddWithValue("@CoverImageLink", m.CoverImageLink);
                    sql.Parameters.AddWithValue("@Reviews", m.Reviews);
                    con.Open();
                    sql.ExecuteNonQuery();
                }
            };

            // TODO: insert actors to movie actor table
            List<string> actorNames = new List<string>(formInput["actors"]);
            // get actor ids instead of names through the form

            // for each actor
            // add row to actormovies
            // actor=actorIds[i], movie=this movie

            return Redirect("/Home/Movies");
        }

        public IActionResult EditMovie(IFormCollection formInput)
        {
            int movieId = Convert.ToInt16(formInput["id"].ToString());

            string name = formInput["name"].ToString().Trim();
            int year = Convert.ToInt16(formInput["year"].ToString());
            string genre = formInput["genre"].ToString().Trim();
            string description = formInput["description"].ToString().Trim();
            string imdblink = formInput["imdblink"].ToString().Trim();
            string coverimagelink = formInput["coverimagelink"].ToString().Trim();
            List<string> actors = formInput["actors"].ToString() != "" 
                ? new List<string>(formInput["actors"].ToList()) 
                : new List<string>();
            // fix that ^

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "UPDATE Movies SET Name = @Name, Year = @Year, Genre = @Genre, " +
                    "Description = @Description, ImdbLink = @ImdbLink, CoverImageLink = " +
                    "@CoverImageLink WHERE Id = @Id";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Id", movieId);
                    sql.Parameters.AddWithValue("@Name", name);
                    sql.Parameters.AddWithValue("@Year", year);
                    sql.Parameters.AddWithValue("@Genre", genre);
                    sql.Parameters.AddWithValue("@Description", description);
                    sql.Parameters.AddWithValue("@ImdbLink", imdblink);
                    sql.Parameters.AddWithValue("@CoverImageLink", coverimagelink);

                    con.Open();
                    sql.ExecuteNonQuery();
                }
            };

            return Redirect("/Home/Movies");
        }

        public IActionResult DeleteMovie(int id) 
        {
            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "DELETE FROM Movies WHERE Id = @Id";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Id", id);
                    con.Open();
                    sql.ExecuteNonQuery();
                }
            };

            return Redirect("/Home/Movies");
        }

        public async Task<string> GenerateMovieReviews(Movie m)
        {
            int numReviews = new Random().Next(5, 10);
            try
            {
                List<Review> reviews = await _chatGPT.GenerateReviews(m.Name, m.Genre, m.Year, numReviews);
                string reviewHTML = "<table style='width: 100%; border-collapse: collapse;'>";
                foreach (Review review in reviews)
                {
                    reviewHTML += $"<tr><td style='vertical-align: top; width: 150px;'>" +
                                    $"<div style='text-wrap: nowrap;'><b>{review.Author}</b></div>" +
                                    $"<div><b>{review.Rating} </b><i class='fa fa-star'></i></div>" +
                                    $"<div><b>{review.Date}<b/></div></td>" +
                                    $"<td style='vertical-align: top;'>" +
                                    review.Content + "</td></tr>";
                }
                return reviewHTML + "</table>";
            }
            catch (Exception ex)
            {
                return "No reviews yet! <a href='/Movie/AddMovieReviews?movie=" + m.Name + "&year=" + m.Year + "&genre=" + m.Genre + ">Generate Reviews</a>";
            }
        }

        public async Task<IActionResult> AddMovieReviews(string movie, int year, string genre)
        {
            string reviewHTML = "";

            int numReviews = new Random().Next(5, 10);
            try
            {
                List<Review> reviews = await _chatGPT.GenerateReviews(movie, genre, year, numReviews);
                reviewHTML = "<table style='width: 100%; border-collapse: collapse;'>";
                foreach (Review review in reviews)
                {
                    reviewHTML += $"<tr><td style='vertical-align: top; width: 150px;'>" +
                                    $"<div style='text-wrap: nowrap;'><b>{review.Author}</b></div>" +
                                    $"<div><b>{review.Rating} </b><i class='fa fa-star'></i></div>" +
                                    $"<div><b>{review.Date}<b/></div></td>" +
                                    $"<td style='vertical-align: top;'>" +
                                    review.Content + "</td></tr>";
                }
                reviewHTML += "</table>";
            }
            catch (Exception ex)
            {
                reviewHTML = "No reviews yet! <a href='/Movie/AddMovieReviews?movie=" + movie + "&year=" + year + "&genre=" + 
                    genre + ">Generate Reviews</a>";
            }

            string? dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (dbConnectionString == null) return View("Error");

            using (SqlConnection con = new SqlConnection(dbConnectionString))
            {
                string query = "UPDATE Movies SET Reviews = @Reviews WHERE Name = @Name AND Year = @Year AND Genre = @Genre";

                using (SqlCommand sql = new SqlCommand(query, con))
                {
                    sql.Parameters.AddWithValue("@Reviews", reviewHTML);
                    sql.Parameters.AddWithValue("@Name", movie);
                    sql.Parameters.AddWithValue("@Year", year);
                    sql.Parameters.AddWithValue("@Genre", genre);

                    con.Open();
                    sql.ExecuteNonQuery();
                }
            }

            return Redirect("/Home/Movies");
        }
    }
}
