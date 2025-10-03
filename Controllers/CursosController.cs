using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExParcial.Data;
using ExParcial.Models;

public class CursosController : Controller
{
    private readonly ApplicationDbContext _context;
    public CursosController(ApplicationDbContext context) => _context = context;

    // GET: /Cursos
    public async Task<IActionResult> Index([FromQuery] CursoFilters filters)
    {
        var q = _context.Cursos.AsNoTracking().Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(filters.Nombre))
            q = q.Where(c => EF.Functions.Like(c.Nombre, $"%{filters.Nombre}%"));

        if (filters.CreditosMin.HasValue)
            q = q.Where(c => c.Creditos >= filters.CreditosMin.Value);

        if (filters.CreditosMax.HasValue)
            q = q.Where(c => c.Creditos <= filters.CreditosMax.Value);

        if (filters.HorarioInicio.HasValue && filters.HorarioFin.HasValue)
        {
            // seleccionar cursos cuyo intervalo se solapa con el rango buscado
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

        var list = await q.OrderBy(c => c.Codigo).ToListAsync();
        ViewData["Filters"] = filters;
        return View(list);
    }

    // GET: /Cursos/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var curso = await _context.Cursos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Activo);
        if (curso == null) return NotFound();
        return View(curso);
    }
}
