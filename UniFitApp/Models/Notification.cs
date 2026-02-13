using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniFitApp.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // Кому предназначено сообщение
        [ForeignKey("UserId")]
        public AppUser User { get; set; }

        [Required]
        public string Message { get; set; } // Текст сообщения

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Когда отправлено

        public bool IsRead { get; set; } = false; // Прочитано или нет
    }
}