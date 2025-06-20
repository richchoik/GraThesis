using Newtonsoft.Json;

namespace WebApplication1.Models
{
    public class User
    {
        public int Id { get; set; }
        [JsonProperty(PropertyName = "body")]
        public string Username { get; set; }
        public string Password { get; set; }  // In production, use hashing
    }
}