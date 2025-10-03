using System;
using System.ComponentModel.DataAnnotations;

namespace ExParcial.Models
{
    public class CursoFilters
    {
        public string? Nombre { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Créditos mínimos no puede ser negativo")]
        public int? CreditosMin { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Créditos máximos no puede ser negativo")]
        public int? CreditosMax { get; set; }

        // Filtrar por horario (buscar cursos que empiecen/despues o que contengan rango)
        public TimeSpan? HorarioInicio { get; set; }
        public TimeSpan? HorarioFin { get; set; }
    }
}
