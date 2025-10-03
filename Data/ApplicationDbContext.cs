using ExParcial.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExParcial.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    { }
    public DbSet<Curso> Cursos { get; set; }
    public DbSet<Matricula> Matriculas { get; set; }      
    protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Curso>()
                .HasIndex(c => c.Codigo)
                .IsUnique();

           builder.Entity<Curso>(entity =>
                {
                    entity.HasIndex(c => c.Codigo).IsUnique();

                    // Check constraint HorarioInicio < HorarioFin usando ToTable
                    entity.ToTable(tb => tb.HasCheckConstraint("CK_Curso_Horario", "HorarioInicio < HorarioFin"));
                });

            builder.Entity<Matricula>()
                .HasIndex(m => new { m.CursoId, m.UsuarioId })
                .IsUnique();

            builder.Entity<Matricula>()
                .Property(m => m.Estado)
                .HasConversion<string>();

            builder.Entity<Curso>().HasData(
                new Curso { Id = 1, Codigo = "CS101", Nombre = "Intro a Programación", Creditos = 3, CupoMaximo = 30, HorarioInicio = TimeSpan.Parse("08:00:00"), HorarioFin = TimeSpan.Parse("10:00:00"), Activo = true },
                new Curso { Id = 2, Codigo = "MA101", Nombre = "Matemáticas I", Creditos = 4, CupoMaximo = 40, HorarioInicio = TimeSpan.Parse("10:30:00"), HorarioFin = TimeSpan.Parse("12:30:00"), Activo = true },
                new Curso { Id = 3, Codigo = "FI101", Nombre = "Física I", Creditos = 4, CupoMaximo = 35, HorarioInicio = TimeSpan.Parse("13:00:00"), HorarioFin = TimeSpan.Parse("15:00:00"), Activo = true }
            );
        }
    }
