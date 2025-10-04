using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using ExParcial.Data;
using ExParcial.Models;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace ExParcial.Controllers
{
    [Authorize]
    public class MatriculasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConnectionMultiplexer _muxer;
        private readonly IDatabase _db;
        private const int LastCourseTtlSeconds = 120; // 2 minutes
        private const int ActiveCoursesTtlSeconds = 60; // 60 seconds
        private const string LastCoursePrefix = "lastcourse:user:";
        private const string ActiveCoursesKey = "activecourses:list";

        public MatriculasController(ApplicationDbContext context, IConnectionMultiplexer muxer)
        {
            _context = context;
            _muxer = muxer;
            _db = _muxer.GetDatabase();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int cursoId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge(); // not authenticated

            var curso = await _context.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cursoId && c.Activo);
            if (curso == null)
            {
                TempData["Error"] = "Curso no encontrado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Save last visited course in Redis (TTL 120 seconds)
            var userKey = $"user:{userId}";
            var lastKey = $"{LastCoursePrefix}{userKey}";
            var payload = JsonSerializer.Serialize(new { courseId = curso.Id, courseName = curso.Nombre });
            await _db.StringSetAsync(lastKey, payload, TimeSpan.FromSeconds(LastCourseTtlSeconds));

            // Duplicate check
            if (await _context.Matriculas.AnyAsync(m => m.CursoId == cursoId && m.UsuarioId == userId))
            {
                TempData["Error"] = "Ya se encuentra una matrícula para este curso.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            // Transaction for consistency
            await using var tx = await _context.Database.BeginTransactionAsync();

            var cursoForUpdate = await _context.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId);
            if (cursoForUpdate == null)
            {
                TempData["Error"] = "Curso no encontrado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

            var inscritos = await _context.Matriculas.CountAsync(m => m.CursoId == cursoId && m.Estado == EstadoMatricula.Confirmada);
            if (inscritos >= cursoForUpdate.CupoMaximo)
            {
                TempData["Error"] = "Cupo máximo alcanzado.";
                return RedirectToAction("Details", "Cursos", new { id = cursoId });
            }

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

        // Endpoint to return active courses using Redis cache (60 seconds)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ActiveCourses()
        {
            var cached = await _db.StringGetAsync(ActiveCoursesKey);
            if (cached.HasValue)
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<IEnumerable<CourseShortDto>>(cached);
                    if (dto != null) return Json(dto);
                }
                catch
                {
                    // fallback to DB
                }
            }

            var items = await _context.Cursos
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => new CourseShortDto { Id = c.Id, Nombre = c.Nombre })
                .ToListAsync();

            var serialized = JsonSerializer.Serialize(items);
            await _db.StringSetAsync(ActiveCoursesKey, serialized, TimeSpan.FromSeconds(ActiveCoursesTtlSeconds));

            return Json(items);
        }

        // Endpoint for layout to get last visited course
        [HttpGet]
        public async Task<IActionResult> LastVisited()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var key = userId != null ? $"user:{userId}" : $"anon:{HttpContext.TraceIdentifier}";
            // antiguo: var userKey = $"user:{userId}"; var lastKey = $"{LastCoursePrefix}{userKey}";
var lastKey = userId != null ? $"lastcourse:user:{userId}" : $"lastcourse:anon:{HttpContext.TraceIdentifier}";


            var val = await _db.StringGetAsync(lastKey);
            if (!val.HasValue) return NoContent();

            try
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(val);
                var id = doc.GetProperty("courseId").GetInt32();
                var name = doc.GetProperty("courseName").GetString();
                return Json(new { id, nombre = name });
            }
            catch
            {
                return NoContent();
            }
        }

        // Method to invalidate active courses cache
        [NonAction]
        public async Task InvalidateActiveCoursesCache()
        {
            await _db.KeyDeleteAsync(ActiveCoursesKey);
        }

        private class CourseShortDto
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
        }
    }
}
