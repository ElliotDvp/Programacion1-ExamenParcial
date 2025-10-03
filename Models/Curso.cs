using System;
using System.ComponentModel.DataAnnotations;

namespace ExParcial.Models
{
    public class Curso
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string? Codigo { get; set; }

        [Required, MaxLength(200)]
        public string? Nombre { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Creditos deben ser mayor que 0")]
        public int Creditos { get; set; }

        [Range(1, int.MaxValue)]
        public int CupoMaximo { get; set; }

        [Required]
        public TimeSpan HorarioInicio { get; set; }

        [Required]
        public TimeSpan HorarioFin { get; set; }

        public bool Activo { get; set; } = true;

        [Timestamp]
        public byte[]? RowVersion { get; set; }

         public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HorarioFin <= HorarioInicio)
            yield return new ValidationResult("HorarioFin debe ser mayor que HorarioInicio", new[] { nameof(HorarioFin), nameof(HorarioInicio) });
        yield break;
    }

    }
}
