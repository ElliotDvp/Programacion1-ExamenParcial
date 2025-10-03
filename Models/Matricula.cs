using System;
using System.ComponentModel.DataAnnotations;

namespace ExParcial.Models
{
    public enum EstadoMatricula { Pendiente, Confirmada, Cancelada }

    public class Matricula
    {
        public int Id { get; set; }

        [Required]
        public int CursoId { get; set; }
        public Curso? Curso { get; set; }

        [Required]
        public string? UsuarioId { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        public EstadoMatricula Estado { get; set; } = EstadoMatricula.Pendiente;
    }
}
