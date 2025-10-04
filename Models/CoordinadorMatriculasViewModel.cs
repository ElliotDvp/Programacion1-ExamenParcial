using System.Collections.Generic;

namespace ExParcial.Models
{
    public class CoordinadorMatriculasViewModel
    {
        public Curso Curso { get; set; } = default!;
        public List<Matricula> Matriculas { get; set; } = new();
        public Dictionary<string, string>? Usernames { get; set; }
    }
}
