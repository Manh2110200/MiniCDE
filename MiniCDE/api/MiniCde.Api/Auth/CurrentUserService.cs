using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniCde.Api.Data;
using MiniCde.Api.Domain;

namespace MiniCde.Api.Auth;

/// Lấy hồ sơ người dùng từ token; lần đầu gọi API thì tự tạo User (+ Organization theo domain email).
public sealed class CurrentUserService(IHttpContextAccessor accessor, AppDbContext db)
{
    private User? _cached;

    public async Task<User> GetOrCreateAsync(CancellationToken ct)
    {
        if (_cached is not null) return _cached;

        var principal = accessor.HttpContext!.User;
        var sub = principal.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("Token thiếu claim 'sub'.");
        var id = Guid.Parse(sub);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var user = await db.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is not null) return _cached = user;

            var email = (principal.FindFirstValue("email") ?? $"{id}@unknown.local").ToLowerInvariant();
            var domain = email[(email.IndexOf('@') + 1)..];

            var org = await db.Organizations.FirstOrDefaultAsync(o => o.Name == domain, ct)
                      ?? new Organization { Id = Guid.NewGuid(), Name = domain };

            db.Users.Add(new User
            {
                Id = id,
                Email = email,
                DisplayName = principal.FindFirstValue("name")
                              ?? principal.FindFirstValue("preferred_username") ?? email,
                Organization = org
            });

            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Hai request đầu tiên chạy song song -> trùng khóa; xóa tracker và đọc lại.
                db.ChangeTracker.Clear();
            }
        }

        _cached = await db.Users.Include(u => u.Organization).FirstAsync(u => u.Id == id, ct);
        return _cached;
    }
}
