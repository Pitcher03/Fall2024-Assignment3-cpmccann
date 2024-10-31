namespace Fall2024_Assignment3_cpmccann.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Year { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }
        public string? ImdbLink { get; set; }
        public string? CoverImageLink { get; set; }
        public DateTime DateAdded { get; set; }
        public string? Actors { get; set; }
        public string? Reviews { get; set; }
    }
}
