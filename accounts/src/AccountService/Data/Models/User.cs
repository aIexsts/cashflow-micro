using System;
using System.ComponentModel.DataAnnotations;
using Cashflow.Common.Data.Enums;
using Cashflow.Common.Data.Models;

namespace AccountService.Data.Models
{
    public class User : BaseEntity
    {
        [Required] public string Email { get; set; }
        [Required] public string UserName { get; set; }
        [Required] public string PublicId { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Password { get; set; }
        public bool IsActive { get; set; }
        public string RefreshToken { get; set; }
        public Genders Gender { get; set; }
        public int RoleId { get; set; }
        public bool IsBanned { get; set; } = false;
    }
}
