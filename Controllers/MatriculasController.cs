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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int cursoId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge(); // no autenticado

            var curso = await _context.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cursoId && c.Activo);
            if (curso == null)
            {
                TempData["Error"] = "Curso no encontrado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Verificar duplicado (ya matriculado en cualquier estado)
            if (await _context.Matriculas.AnyAsync(m => m.CursoId == cursoId && m.UsuarioId == userId))
            {
                TempData["Error"] = "Ya se encuentra una matrícula para este curso.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Transacción para consistencia (cupo y evitar race)
            await using var tx = await _context.Database.BeginTransactionAsync();

            // Re-obtener curso con seguimiento para posible RowVersion (opcional)
            var cursoForUpdate = await _context.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId);
            if (cursoForUpdate == null)
            {
                TempData["Error"] = "Curso no encontrado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Contar matriculas confirmadas (o pendientes según la política)
            var inscritos = await _context.Matriculas.CountAsync(m => m.CursoId == cursoId && m.Estado == EstadoMatricula.Confirmada);
            if (inscritos >= cursoForUpdate.CupoMaximo)
            {
                TempData["Error"] = "Cupo máximo alcanzado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Verificar solapamiento horario con cursos ya matriculados (considerar aquellos Confirmada o Pendiente segun regla)
            var userMatriculas = await _context.Matriculas
                .Include(m => m.Curso)
                .Where(m => m.UsuarioId == userId && m.Estado != EstadoMatricula.Cancelada)
                .ToListAsync();

            bool solapa = userMatriculas.Any(m =>
                !(m.Curso.HorarioFin <= cursoForUpdate.HorarioInicio || m.Curso.HorarioInicio >= cursoForUpdate.HorarioFin)
            );

            if (solapa)
            {
                TempData["Error"] = "La inscripción se solapa con otro curso ya matriculado en el mismo horario.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Crear matrícula en estado Pendiente
            var matricula = new Matricula
            {
                CursoId = cursoId,
                UsuarioId = userId,
                Estado = EstadoMatricula.Pendiente,
                FechaRegistro = DateTime.UtcNow
            };

            _context.Matriculas.Add(matricula);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = "Inscripción creada en estado Pendiente.";
            return RedirectToAction("Details", "Cursos", new { id = cursoId });
        }
    }
}