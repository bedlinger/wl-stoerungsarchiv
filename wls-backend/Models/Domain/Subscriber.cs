using System.ComponentModel.DataAnnotations;

namespace wls_backend.Models.Domain
{
    public class Subscriber
    {
        [Key]
        required public string Token { get; set; }
        required public ICollection<Line> Lines { get; set; }
    }
}
