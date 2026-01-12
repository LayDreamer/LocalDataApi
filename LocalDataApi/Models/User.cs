using System.ComponentModel.DataAnnotations;

namespace LocalDataApi.Models
{
    public class User
    {
        [Key]       
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public  string? UserName { get; set; }
       
        [MaxLength(100)]
        public  string? Email { get; set; }

        public DateTime CreateDate { get; set; }
    }
}
