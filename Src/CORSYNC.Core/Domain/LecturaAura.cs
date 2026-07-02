using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    public class LecturaAura
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string DispositivoId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,1)")]
        public decimal BpmPromedio { get; set; }

        [Column(TypeName = "decimal(5,1)")]
        public decimal BpmMaximo { get; set; }

        [Column(TypeName = "decimal(5,1)")]
        public decimal BpmMinimo { get; set; }

        public int GsrRawPromedio { get; set; }

        [Column(TypeName = "decimal(5,3)")]
        public decimal GsrVoltajePromedio { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal NivelEstres { get; set; }  // 0.00 - 100.00

        [Required]
        [MaxLength(50)]
        public string AuraDominante { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Notas { get; set; }

        public int DuracionSegundos { get; set; }

        public DateTime FechaInicio { get; set; }
        
        public DateTime FechaFin { get; set; }
    }
}
