using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExParcial.Data;
using ExParcial.Models;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

public class CursosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _muxer;
    private readonly IDatabase _db;
    private const int ActiveCoursesTtlSeconds = 60;
    private const int LastCourseTtlSeconds = 120;
    private const string ActiveCoursesKey = "activecourses:list";
    private const string LastCoursePrefix = "lastcourse:user:";

    public CursosController(ApplicationDbContext context, IConnectionMultiplexer muxer)
    {
        _context = context;
        _muxer = muxer;
        _db = _muxer.GetDatabase();
    }

    // GET: /Cursos
    public async Task<IActionResult> Index([FromQuery] CursoFilters filters)
    {
        // Decide if we can use the cached full active list
        bool noFilters =
            string.IsNullOrWhiteSpace(filters?.Nombre)
            && filters?.CreditosMin == null
            && filters?.CreditosMax == null
            && filters?.HorarioInicio == null
            && filters?.HorarioFin == null;

        List<Curso> list;

        if (noFilters)
        {
            var cached = await _db.StringGetAsync(ActiveCoursesKey);
            if (cached.HasValue)
            {
                try
                {
                    // Deserialize into list of Curso; map navigations not required for listing
                    list = JsonSerializer.Deserialize<List<Curso>>(cached) ?? new List<Curso>();
                    ViewData["Filters"] = filters;
                    return View(list.OrderBy(c => c.Codigo).ToList());
                }
                catch
                {
                    // fall through to DB fetch on deserialization error
                }
            }

            list = await _context.Cursos
                .AsNoTracking()
                .Where(c => c.Activo)
                .ToListAsync();

            var serialized = JsonSerializer.Serialize(list);
            await _db.StringSetAsync(ActiveCoursesKey, serialized, TimeSpan.FromSeconds(ActiveCoursesTtlSeconds));

            ViewData["Filters"] = filters;
            return View(list.OrderBy(c => c.Codigo).ToList());
        }

        // Filters present -> query DB directly to respect filters
        var q = _context.Cursos.AsNoTracking().Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(filters.Nombre))
            q = q.Where(c => EF.Functions.Like(c.Nombre, $"%{filters.Nombre}%"));

        if (filters.CreditosMin.HasValue)
            q = q.Where(c => c.Creditos >= filters.CreditosMin.Value);

        if (filters.CreditosMax.HasValue)
            q = q.Where(c => c.Creditos <= filters.CreditosMax.Value);

        if (filters.HorarioInicio.HasValue && filters.HorarioFin.HasValue)
        {
            var hi = filters.HorarioInicio.Value;
            var hf = filters.HorarioFin.Value;
            q = q.Where(c => !(c.HorarioFin <= hi || c.HorarioInicio >= hf));
        }
        else if (filters.HorarioInicio.HasValue)
        {
            var hi = filters.HorarioInicio.Value;
            q = q.Where(c => c.HorarioFin > hi);
        }
        else if (filters.HorarioFin.HasValue)
        {
            var hf = filters.HorarioFin.Value;
            q = q.Where(c => c.HorarioInicio < hf);
        }

        list = await q.OrderBy(c => c.Codigo).ToListAsync();
        ViewData["Filters"] = filters;
        return View(list);
    }

    // GET: /Cursos/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var curso = await _context.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Activo);
        if (curso == null) return NotFound();

        // Save last visited course in Redis for the user to show in layout
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
var lastKey = userId != null ? $"lastcourse:user:{userId}" : $"lastcourse:anon:{HttpContext.TraceIdentifier}";


        try
        {
            var payload = JsonSerializer.Serialize(new { courseId = curso.Id, courseName = curso.Nombre });
            await _db.StringSetAsync(lastKey, payload, TimeSpan.FromSeconds(LastCourseTtlSeconds));
        }
        catch
        {
            // Do not block rendering if Redis fails
        }

        return View(curso);
    }

    // Public method to invalidate active courses cache after create or edit
    [NonAction]
    public async Task InvalidateActiveCoursesCache()
    {
        await _db.KeyDeleteAsync(ActiveCoursesKey);
    }
}
