using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    public class LecturaCorazon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string DispositivoId { get; set; } = "ESP32_MAX30102";

        public long IR { get; set; }

        [Column(TypeName = "decimal(5,1)")]
        public decimal BPM { get; set; }

        public int BPMPromedio { get; set; }

        public int GsrRaw { get; set; }

        [Column(TypeName = "decimal(5,3)")]
        public decimal GsrVoltaje { get; set; }

        [MaxLength(50)]
        public string? Aura { get; set; }

        public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    }
}
