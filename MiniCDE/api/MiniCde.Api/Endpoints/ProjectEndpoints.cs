using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MiniCde.Api.Auth;
using MiniCde.Api.Data;
using MiniCde.Api.Domain;

namespace MiniCde.Api.Endpoints;

public static class ProjectEndpoints
{
    public record CreateProjectRequest(string? Code, string? Name);
    public record AddMemberRequest(string? Email, ProjectRole Role);
    public record ProjectDto(Guid Id, string Code, string Name, ProjectRole MyRole, DateTime CreatedAt);

    private static readonly Regex CodeRegex = new("^[A-Z0-9]{2,10}$", RegexOptions.Compiled);

    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projects").RequireAuthorization();
        g.MapPost("/", Create);
        g.MapGet("/", List);
        g.MapPost("/{id:guid}/members", AddMember);
        g.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMember);
    }

    private static async Task<IResult> Create(CreateProjectRequest req, AppDbContext db,
        CurrentUserService current, CancellationToken ct)
    {
        var me = await current.GetOrCreateAsync(ct);
        var code = (req.Code ?? "").Trim().ToUpperInvariant();

        if (!CodeRegex.IsMatch(code))
            return Results.Problem(title: "Mã dự án không hợp lệ",
                detail: "Mã gồm 2-10 ký tự chữ hoa hoặc số.", statusCode: 400);
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.Problem(title: "Thiếu tên dự án", statusCode: 400);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = me.OrganizationId,
            Code = code,
            Name = req.Name.Trim(),
            CreatedByUserId = me.Id
        };
        project.Members.Add(new ProjectMember { UserId = me.Id, Role = ProjectRole.ProjectAdmin });
        db.Projects.Add(project);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(title: "Mã dự án đã tồn tại trong tổ chức", statusCode: 409);
        }

        return Results.Created($"/api/projects/{project.Id}",
            new ProjectDto(project.Id, project.Code, project.Name, ProjectRole.ProjectAdmin, project.CreatedAt));
    }

    private static async Task<IResult> List(AppDbContext db, CurrentUserService current, CancellationToken ct)
    {
        var me = await current.GetOrCreateAsync(ct);
        var items = await db.ProjectMembers
            .Where(m => m.UserId == me.Id && m.Project.OrganizationId == me.OrganizationId)
            .OrderBy(m => m.Project.Code)
            .Select(m => new ProjectDto(m.ProjectId, m.Project.Code, m.Project.Name, m.Role, m.Project.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> AddMember(Guid id, AddMemberRequest req, AppDbContext db,
        CurrentUserService current, CancellationToken ct)
    {
        var me = await current.GetOrCreateAsync(ct);
        var denied = await RequireAdmin(db, id, me.Id, ct);
        if (denied is not null) return denied;

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var target = await db.Users.FirstOrDefaultAsync(
            u => u.OrganizationId == me.OrganizationId && u.Email == email, ct);
        if (target is null)
            return Results.Problem(title: "Không tìm thấy người dùng",
                detail: "Người dùng phải đã đăng nhập ít nhất một lần và thuộc cùng tổ chức.", statusCode: 404);

        if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == target.Id, ct))
            return Results.Problem(title: "Người dùng đã là thành viên dự án", statusCode: 409);

        db.ProjectMembers.Add(new ProjectMember { ProjectId = id, UserId = target.Id, Role = req.Role });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/projects/{id}/members/{target.Id}",
            new { UserId = target.Id, target.Email, req.Role });
    }

    private static async Task<IResult> RemoveMember(Guid id, Guid userId, AppDbContext db,
        CurrentUserService current, CancellationToken ct)
    {
        var me = await current.GetOrCreateAsync(ct);
        var denied = await RequireAdmin(db, id, me.Id, ct);
        if (denied is not null) return denied;

        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == userId, ct);
        if (member is null) return Results.NotFound();

        if (member.Role == ProjectRole.ProjectAdmin &&
            await db.ProjectMembers.CountAsync(m => m.ProjectId == id && m.Role == ProjectRole.ProjectAdmin, ct) == 1)
            return Results.Problem(title: "Không thể gỡ ProjectAdmin cuối cùng", statusCode: 409);

        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// null = được phép. Không phải thành viên -> 404 (không lộ sự tồn tại); thành viên nhưng thiếu quyền -> 403.
    private static async Task<IResult?> RequireAdmin(AppDbContext db, Guid projectId, Guid userId, CancellationToken ct)
    {
        var membership = await db.ProjectMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);
        if (membership is null) return Results.NotFound();
        if (membership.Role != ProjectRole.ProjectAdmin)
            return Results.Problem(title: "Không đủ quyền", detail: "Cần vai trò ProjectAdmin.", statusCode: 403);
        return null;
    }
}
