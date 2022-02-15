using System;
using System.ComponentModel.DataAnnotations;
using Cashflow.Common.Data.Models;
using TaskService.Data.Models;

namespace ModerationService.Data.Models
{
    public class Task : BaseEntity
    {
        [Required] public string Title { get; set; }
        [Required] public string Description { get; set; }
        [Required] public int UserId { get; set; }
        public User User { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
