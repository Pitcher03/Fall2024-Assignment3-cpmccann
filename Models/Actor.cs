namespace Fall2024_Assignment3_cpmccann.Models
{
    public class Actor
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Gender { get; set; }
        public int Age { get; set; }
        public string? ImdbLink { get; set; }
        public string? CoverImageLink { get; set; }
        public List<Movie>? Movies { get; set; }
        public string? Tweets { get; set; }
    }
}
