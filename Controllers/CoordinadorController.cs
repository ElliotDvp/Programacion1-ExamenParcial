using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExParcial.Data;
using ExParcial.Models;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

[Authorize(Roles = "Coordinador")]
public class CoordinadorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _muxer;
    private readonly IDatabase? _db;
    private const string ActiveCoursesKey = "activecourses:list";

public CoordinadorController(ApplicationDbContext context, IConnectionMultiplexer muxer)
{
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _muxer = muxer ?? throw new ArgumentNullException(nameof(muxer));
    _db = _muxer.GetDatabase();
}

    public async Task<IActionResult> Index()
    {
        var cursos = await _context.Cursos.OrderBy(c => c.Codigo).ToListAsync();
        return View(cursos);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Curso curso)
    {
        if (!ModelState.IsValid) return View(curso);

        _context.Cursos.Add(curso);
        await _context.SaveChangesAsync();

        if (_db != null) await _db.KeyDeleteAsync(ActiveCoursesKey);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound();
        return View(curso);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Curso curso)
    {
        if (id != curso.Id) return BadRequest();
        if (!ModelState.IsValid) return View(curso);

        try
        {
            _context.Update(curso);
            await _context.SaveChangesAsync();
            if (_db != null) await _db.KeyDeleteAsync(ActiveCoursesKey);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Cursos.AnyAsync(c => c.Id == id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActivo(int id)
    {
        var curso = await _context.Cursos.FindAsync(id);
        if (curso == null) return NotFound();

        curso.Activo = !curso.Activo;
        await _context.SaveChangesAsync();

        if (_db != null) await _db.KeyDeleteAsync(ActiveCoursesKey);

        return RedirectToAction(nameof(Index));
    }

    // Lista matrículas del curso (no usa navegación Usuario)
    public async Task<IActionResult> Matriculas(int id)
    {
        var curso = await _context.Cursos.FirstOrDefaultAsync(c => c.Id == id);
        if (curso == null) return NotFound();

        var matriculas = await _context.Matriculas
            .Where(m => m.CursoId == id)
            .OrderByDescending(m => m.FechaRegistro)
            .ToListAsync();

        var vm = new CoordinadorMatriculasViewModel
        {
            Curso = curso,
            Matriculas = matriculas
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarMatricula(int matriculaId)
    {
        var m = await _context.Matriculas.FindAsync(matriculaId);
        if (m == null) return NotFound();

        m.Estado = EstadoMatricula.Confirmada;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { id = m.CursoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarMatricula(int matriculaId)
    {
        var m = await _context.Matriculas.FindAsync(matriculaId);
        if (m == null) return NotFound();

        m.Estado = EstadoMatricula.Cancelada;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { id = m.CursoId });
    }
}

