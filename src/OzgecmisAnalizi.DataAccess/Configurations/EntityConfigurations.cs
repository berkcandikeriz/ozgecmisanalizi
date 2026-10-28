using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OzgecmisAnalizi.Entities.Concrete;

namespace OzgecmisAnalizi.DataAccess.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> b)
    {
        b.ToTable("Resumes");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Template).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OriginalFileName).HasMaxLength(260);
        b.Property(x => x.RawText).HasColumnType("nvarchar(max)");
        b.Property(x => x.Content).HasJsonConversion();

        b.HasOne(x => x.User).WithMany(x => x.Resumes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TargetProfession).WithMany().HasForeignKey(x => x.TargetProfessionId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class ResumeAnalysisConfiguration : IEntityTypeConfiguration<ResumeAnalysis>
{
    public void Configure(EntityTypeBuilder<ResumeAnalysis> b)
    {
        b.ToTable("ResumeAnalyses", t =>
        {
            t.HasCheckConstraint("CK_ResumeAnalyses_Overall", "[OverallScore] BETWEEN 0 AND 100");
        });
        b.Property(x => x.Label).HasMaxLength(40);
        b.Property(x => x.Engine).HasMaxLength(100);
        b.Property(x => x.Details).HasJsonConversion();

        b.HasOne(x => x.Resume).WithMany(x => x.Analyses).HasForeignKey(x => x.ResumeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Profession).WithMany().HasForeignKey(x => x.ProfessionId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.ResumeId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class ProfessionConfiguration : IEntityTypeConfiguration<Profession>
{
    public void Configure(EntityTypeBuilder<Profession> b)
    {
        b.ToTable("Professions");
        b.Property(x => x.Code).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Category).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.SearchKeywords).HasMaxLength(500);
        b.Property(x => x.CareerTitles).HasMaxLength(500);
        b.Property(x => x.JuniorSalaryFactor).HasPrecision(6, 2);
        b.Property(x => x.SeniorSalaryFactor).HasPrecision(6, 2);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Category);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class ProfessionSkillConfiguration : IEntityTypeConfiguration<ProfessionSkill>
{
    public void Configure(EntityTypeBuilder<ProfessionSkill> b)
    {
        b.ToTable("ProfessionSkills");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Aliases).HasMaxLength(500);
        b.Property(x => x.LearningHint).HasMaxLength(500);
        b.Property(x => x.Importance).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Profession).WithMany(x => x.Skills).HasForeignKey(x => x.ProfessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.ProfessionId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("JobApplications");
        b.Property(x => x.JobTitle).HasMaxLength(200).IsRequired();
        b.Property(x => x.Company).HasMaxLength(200).IsRequired();
        b.Property(x => x.Location).HasMaxLength(150);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.Source).HasMaxLength(60);
        b.Property(x => x.Description).HasColumnType("nvarchar(max)");
        b.Property(x => x.Notes).HasMaxLength(4000);
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.User).WithMany(x => x.Applications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Resume).WithMany().HasForeignKey(x => x.ResumeId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.UserId, x.Stage });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class CoverLetterConfiguration : IEntityTypeConfiguration<CoverLetter>
{
    public void Configure(EntityTypeBuilder<CoverLetter> b)
    {
        b.ToTable("CoverLetters");
        b.Property(x => x.JobTitle).HasMaxLength(200).IsRequired();
        b.Property(x => x.Company).HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.Engine).HasMaxLength(100);
        b.HasOne(x => x.Resume).WithMany().HasForeignKey(x => x.ResumeId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
