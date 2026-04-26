using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace UniFitApp.Models
{
    public class Workout
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название обязательно")]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public string Type { get; set; } = "General";

        [Required(ErrorMessage = "Дата обязательна")]
        public DateTime StartTime { get; set; }

        // === ПОЛЯ ДЛЯ КАРТИНКИ ===
        public string? ImageUrl { get; set; }

        [NotMapped] // Это поле не будет создаваться в базе данных
        public IFormFile? ImageFile { get; set; }

        [Range(1, 100, ErrorMessage = "Вместимость от 1 до 100")]
        public int Capacity { get; set; }

        // === ПОЛЕ ДЛЯ ВИДЕОГИДА ===
        public string? VideoUrl { get; set; }

        public string CoachId { get; set; }
        [ForeignKey("CoachId")]
        public AppUser Coach { get; set; }

        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}