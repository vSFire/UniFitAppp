using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Required(ErrorMessage = "Место проведения обязательно")]
        public string Location { get; set; }

        [Range(1, 100, ErrorMessage = "Вместимость от 1 до 100")]
        public int Capacity { get; set; }

        // === ПОЛЕ ДЛЯ ВИДЕОГИДА (Добавлено по новому дизайну) ===
        public string? VideoUrl { get; set; }

        public string CoachId { get; set; }
        [ForeignKey("CoachId")]
        public AppUser Coach { get; set; }

        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}