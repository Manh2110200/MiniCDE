namespace MiniCde.Api.Domain;

public enum ProjectRole { ProjectAdmin, Approver, Author, Viewer }

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<User> Users { get; set; } = [];
}

public class User
{
    public Guid Id { get; set; }              // = claim "sub" của Keycloak
    public string Email { get; set; } = "";   // luôn lưu chữ thường
    public string DisplayName { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Project
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ProjectMember> Members { get; set; } = [];
}

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public ProjectRole Role { get; set; }
}
