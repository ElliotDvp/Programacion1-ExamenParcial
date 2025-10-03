using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using ExParcial.Data;
using ExParcial.Models;

namespace ExParcial.Controllers
{
    [Authorize]
    public class MatriculasController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MatriculasController(ApplicationDbContext context) => _context = context;

        [HttpPost]
        public async Task<IActionResult> Create(int cursoId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            if (await _context.Matriculas.AnyAsync(m => m.CursoId == cursoId && m.UsuarioId == userId))
                return BadRequest("Usuario ya matriculado en este curso");

            await using var tx = await _context.Database.BeginTransactionAsync();
            var curso = await _context.Cursos.FindAsync(cursoId);
            if (curso == null) return NotFound();

            var inscritos = await _context.Matriculas.CountAsync(m => m.CursoId == cursoId && m.Estado == EstadoMatricula.Confirmada);
            if (inscritos >= curso.CupoMaximo)
                return BadRequest("Cupo máximo alcanzado");

            var matricula = new Matricula { CursoId = cursoId, UsuarioId = userId, Estado = EstadoMatricula.Confirmada };
            _context.Matriculas.Add(matricula);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok();
        }
    }
}
