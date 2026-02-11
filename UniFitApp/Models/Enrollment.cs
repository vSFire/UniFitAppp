using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFitApp.Models
{
    public class Enrollment
    {
        public int Id { get; set; }

        // Какая тренировка?
        public int WorkoutId { get; set; }
        [ForeignKey("WorkoutId")]
        public Workout Workout { get; set; }

        // Какой студент?
        public string StudentId { get; set; }
        [ForeignKey("StudentId")]
        public AppUser Student { get; set; }

        // Когда записался?
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public bool IsPresent { get; set; } = false;
    }
}